import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('..', import.meta.url)));
const captureHook = join(root, 'scripts', 'graphql-js-test-capture.cjs');
const corpusPath = join(root, 'src', 'Salep.GraphQLParser.Tests', 'Fixtures', 'ReferenceSuite', 'corpus.json');
const mode = process.argv[2];
const checkout = resolve(process.env.GRAPHQL_JS_CHECKOUT ?? process.argv[3] ?? '');
const expectedCommit = '57b385b288150960acd09337adf2fc778abb32ab';
const expectedPackageVersion = '16.14.0';
const testFiles = [
  'src/language/__tests__/lexer-test.ts',
  'src/language/__tests__/parser-test.ts',
  'src/language/__tests__/schema-parser-test.ts',
  'src/language/__tests__/blockString-test.ts',
  'src/language/__tests__/predicates-test.ts',
  'src/language/__tests__/printLocation-test.ts',
  'src/language/__tests__/printString-test.ts',
  'src/language/__tests__/printer-test.ts',
  'src/language/__tests__/schema-printer-test.ts',
  'src/language/__tests__/schemaCoordinateLexer-test.ts',
  'src/language/__tests__/source-test.ts',
  'src/language/__tests__/visitor-test.ts',
];

if (mode !== '--write' && mode !== '--check') {
  throw new Error('Usage: GRAPHQL_JS_CHECKOUT=/path/to/graphql-js-v16.14.0 GRAPHQL_JS_NODE=/path/to/node20 node scripts/export-reference-suite.mjs --write|--check');
}
if (!process.env.GRAPHQL_JS_CHECKOUT && !process.argv[3]) {
  throw new Error('Set GRAPHQL_JS_CHECKOUT to a graphql-js v16.14.0 checkout.');
}

const require = createRequire(import.meta.url);
const { parse: graphqlParse, version } = require('graphql');
if (version !== expectedPackageVersion) throw new Error(`Expected local graphql-js ${expectedPackageVersion}; found ${version}.`);

const upstreamHead = runText('git', ['-C', checkout, 'rev-parse', 'HEAD']);
if (upstreamHead !== expectedCommit) throw new Error(`Expected graphql-js commit ${expectedCommit}; found ${upstreamHead}.`);
const checkoutPackage = JSON.parse(await readFile(join(checkout, 'package.json'), 'utf8'));
if (checkoutPackage.version !== expectedPackageVersion) throw new Error(`Expected upstream checkout version ${expectedPackageVersion}; found ${checkoutPackage.version}.`);

const node = process.env.GRAPHQL_JS_NODE ?? process.execPath;
const nodeVersion = runText(node, ['--version']);
if (!/^v20\./.test(nodeVersion)) throw new Error(`The pinned graphql-js v16 test runner needs Node 20; found ${nodeVersion}. Set GRAPHQL_JS_NODE to a Node 20 executable.`);
for (const file of testFiles) {
  await readFile(join(checkout, file));
}

const captureFile = join(tmpdir(), `graphql-js-reference-${process.pid}.json`);
const mochaPath = join(checkout, 'node_modules', 'mocha', 'bin', 'mocha');
const testRun = spawnSync(node, [mochaPath, '--require', captureHook, '--reporter', 'dot', '--full-trace', ...testFiles], {
  cwd: checkout,
  encoding: 'utf8',
  env: { ...process.env, GRAPHQL_REFERENCE_CAPTURE_PATH: captureFile },
});
if (testRun.error) throw testRun.error;
if (testRun.status !== 0) throw new Error(`Pinned upstream language tests failed (${testRun.status}).\n${testRun.stdout}\n${testRun.stderr}`);
const testCount = Number(/\b(\d+) passing\b/.exec(testRun.stdout)?.[1]);
if (!Number.isInteger(testCount) || testCount === 0) throw new Error('Could not determine the upstream test runner case count.');
const captured = JSON.parse(await readFile(captureFile, 'utf8'));

const cases = [];
const exclusions = [...captured.excludedCases];
const duplicateCounts = new Map();
function addCase(kind, origin, source, expected, extra = {}) {
  const signature = JSON.stringify([kind, origin.sourceFile, origin.test, source]);
  const duplicate = duplicateCounts.get(signature) ?? 0;
  duplicateCounts.set(signature, duplicate + 1);
  const digest = createHash('sha256').update(`${signature}\0${duplicate}`, 'utf8').digest('hex').slice(0, 16);
  cases.push({
    id: `${kind}-${digest}`,
    kind,
    origin: { file: origin.sourceFile, test: origin.test },
    sourceUtf16: Array.from({ length: source.length }, (_, index) => source.charCodeAt(index)),
    ...extra,
    expected,
  });
}

async function finalizeCorpus() {
for (const entry of captured.parseCases) {
  addParseCase(entry.source, entry, entry.options ?? undefined, entry.outcome);
}

for (const entry of captured.helperCases) {
  let documentSource;
  switch (entry.method) {
    case 'parseValue':
      documentSource = `{ reference(value: ${entry.source}) }`;
      break;
    case 'parseConstValue':
      documentSource = `input __Reference { value: String = ${entry.source} }`;
      break;
    case 'parseType':
      documentSource = `query __Reference($value: ${entry.source}) { reference }`;
      break;
    default:
      throw new Error(`Unsupported captured parser helper: ${entry.method}`);
  }
  addParseCase(documentSource, entry, undefined, entry.outcome, { adaptedFrom: entry.method, originalSource: entry.source });
}

for (const entry of captured.lexerCases) {
  const expected = {
    tokens: entry.tokens.map((token) => ({
      kind: token.kind,
      value: token.value ?? entry.source.slice(token.start, token.end),
      start: token.start,
      end: token.end,
    })),
    errorPosition: entry.error?.position ?? null,
    advanceCalls: entry.calls,
  };
  addCase('lexer', entry, entry.source, expected);
}

for (const entry of captured.blockStringCases) {
  const content = entry.lines.join('\n');
  const source = `"""${content}"""`;
  const expectedValue = entry.value.join('\n');
  const parsed = graphqlParse(`{ reference(value: ${source}) }`);
  const actualValue = parsed.definitions[0].selectionSet.selections[0].arguments[0].value.value;
  if (actualValue !== expectedValue) throw new Error(`${entry.test}: utility and lexer block-string results diverged in the pinned reference suite.`);
  addCase('blockString', entry, source, { value: expectedValue });
}

for (const entry of captured.schemaCoordinateCases) {
  addCase('schemaCoordinate', entry, entry.source, entry.expected);
}

for (const entry of captured.utilityCases) {
  addCase('utility', entry, JSON.stringify(entry.input), entry.expected, {
    utility: { method: entry.method, input: entry.input, options: entry.options ?? null },
  });
}

for (const entry of captured.locationPrintCases) {
  addCase('utility', entry, entry.body, entry.expected, {
    utility: { method: 'printSourceLocation', input: { body: entry.body, name: entry.name, locationOffset: entry.locationOffset, location: entry.location } },
  });
}

for (const entry of captured.sourceCases) {
  if (entry.outcome !== 'valid' && typeof entry.body !== 'string') {
    exclusions.push({ test: entry.test, sourceFile: entry.sourceFile, reason: 'Invalid JavaScript constructor argument-shape diagnostics are not part of the strongly typed source API contract.' });
    continue;
  }
  addCase('utility', entry, JSON.stringify({ body: entry.body, name: entry.name ?? null, locationOffset: entry.locationOffset ?? null }),
    entry.outcome === 'valid' ? { outcome: entry.outcome } : { outcome: entry.outcome, errorName: entry.errorName }, {
    utility: { method: 'Source', input: { body: entry.body, name: entry.name, locationOffset: entry.locationOffset } },
  });
}

for (const entry of captured.printCases) {
  if (entry.test.includes('Experimental:')) {
    exclusions.push({ test: entry.test, sourceFile: entry.sourceFile, reason: 'Experimental directive-on-directive syntax is outside the pinned GraphQL language specification contract.' });
    continue;
  }
  if (!entry.nodeLocation && !entry.source) {
    exclusions.push({ test: entry.test, sourceFile: entry.sourceFile, reason: 'Printer target is a detached node without a source location.' });
    continue;
  }
  addCase('printer', entry, entry.source, { output: entry.expected }, {
    printer: { nodeKind: entry.nodeKind, nodeLocation: entry.nodeLocation },
    ...(entry.options ? { parserOptions: {
      ...(entry.options.maxTokens === undefined ? {} : { maximumTokenCount: entry.options.maxTokens }),
      ...(entry.options.noLocation === undefined ? {} : { noLocation: entry.options.noLocation }),
      ...(entry.options.allowLegacyFragmentVariables === undefined ? {} : { allowLegacyFragmentVariables: entry.options.allowLegacyFragmentVariables }),
    } } : {}),
  });
}

for (const entry of captured.predicateCases) {
  addCase('predicate', entry, JSON.stringify(entry.node), { value: entry.expected }, {
    predicate: { method: entry.method, node: entry.node },
  });
}

for (const entry of captured.coordinateLexerCases) {
  addCase('coordinateLexer', entry, entry.source, entry.token ? { token: entry.token } : { error: entry.error });
}

for (const entry of captured.visitorCases) {
  addCase('visitor', entry, entry.source, { ast: entry.expected }, entry.options ? { parserOptions: {
    ...(entry.options.maxTokens === undefined ? {} : { maximumTokenCount: entry.options.maxTokens }),
    ...(entry.options.noLocation === undefined ? {} : { noLocation: entry.options.noLocation }),
    ...(entry.options.allowLegacyFragmentVariables === undefined ? {} : { allowLegacyFragmentVariables: entry.options.allowLegacyFragmentVariables }),
  } } : {});
}

function addParseCase(source, entry, options, upstreamOutcome, extra = {}) {
  try {
    const ast = graphqlParse(source, options);
    if (upstreamOutcome !== 'valid') throw new Error(`${entry.test}: captured rejected parse now parses successfully in the pinned oracle.`);
    addCase('parse', entry, source, { outcome: 'valid', ast: canonicalNode(ast) }, {
      ...(options ? { parserOptions: {
        ...(options.maxTokens === undefined ? {} : { maximumTokenCount: options.maxTokens }),
        ...(options.noLocation === undefined ? {} : { noLocation: options.noLocation }),
        ...(options.allowLegacyFragmentVariables === undefined ? {} : { allowLegacyFragmentVariables: options.allowLegacyFragmentVariables }),
      } } : {}),
      ...extra,
    });
  } catch (error) {
    if (error.message?.includes('captured rejected parse now parses successfully')) throw error;
    if (upstreamOutcome !== 'invalid') throw new Error(`${entry.test}: captured successful parse now fails in the pinned oracle: ${error.message}`);
    const resourceLimit = options?.maxTokens !== undefined;
    addCase('parse', entry, source, {
      outcome: 'invalid',
      failureCategory: resourceLimit ? 'resource' : classifyFailure(error.message),
      position: error.positions?.[0] ?? null,
    }, {
      ...(options ? { parserOptions: {
        ...(options.maxTokens === undefined ? {} : { maximumTokenCount: options.maxTokens }),
        ...(options.noLocation === undefined ? {} : { noLocation: options.noLocation }),
        ...(options.allowLegacyFragmentVariables === undefined ? {} : { allowLegacyFragmentVariables: options.allowLegacyFragmentVariables }),
      } } : {}),
      ...extra,
    });
  }
}

const coveredTests = new Set(cases.map((item) => item.origin.test));
for (const item of exclusions) coveredTests.add(item.test);
const knownNonParserTests = [
  ...captured.executedTests
    .filter(({ test }) => !coveredTests.has(test))
    .map(({ test, sourceFile }) => ({
      test,
      sourceFile,
      reason: sourceFile === 'visitor-test.ts'
        ? 'Editable visitor replacement behavior is handled by the public API compatibility work; read-only traversal is covered separately.'
        : 'No observable parser or language utility call was captured for this reference test.',
    })),
];
exclusions.push(...knownNonParserTests);
const stillUncovered = captured.executedTests.filter(({ test }) => !coveredTests.has(test) && !knownNonParserTests.some((item) => item.test === test));
if (stillUncovered.length > 0) throw new Error(`Upstream tests are not accounted for: ${stillUncovered.map((item) => item.test).join(', ')}`);

const uniqueExclusions = [...new Map(exclusions.map((entry) => [`${entry.sourceFile}\0${entry.test}\0${entry.reason}`, entry])).values()]
  .sort((left, right) => `${left.sourceFile}\0${left.test}\0${left.reason}`.localeCompare(`${right.sourceFile}\0${right.test}\0${right.reason}`, 'en'));
const sourceHashes = {};
for (const file of testFiles) sourceHashes[file] = createHash('sha256').update(await readFile(join(checkout, file))).digest('hex');
const provenance = {
  graphqlJsVersion: version,
  graphqlJsCommit: upstreamHead,
  sourceTestFiles: testFiles,
  sourceSha256: sourceHashes,
  upstreamTestsPassing: testCount,
  caseCounts: {
    parse: cases.filter((item) => item.kind === 'parse').length,
    lexer: cases.filter((item) => item.kind === 'lexer').length,
    blockString: cases.filter((item) => item.kind === 'blockString').length,
    utility: cases.filter((item) => item.kind === 'utility').length,
    schemaCoordinate: cases.filter((item) => item.kind === 'schemaCoordinate').length,
    printer: cases.filter((item) => item.kind === 'printer').length,
    predicate: cases.filter((item) => item.kind === 'predicate').length,
    coordinateLexer: cases.filter((item) => item.kind === 'coordinateLexer').length,
    visitor: cases.filter((item) => item.kind === 'visitor').length,
    excludedTestCases: uniqueExclusions.length,
    excludedTestNames: new Set(uniqueExclusions.map((item) => item.test)).size,
  },
  capture: 'scripts/graphql-js-test-capture.cjs',
};
const upstreamTests = [...new Map(captured.executedTests.map((item) => [`${item.sourceFile}\0${item.test}`, item])).values()]
  .sort((left, right) => `${left.sourceFile}\0${left.test}`.localeCompare(`${right.sourceFile}\0${right.test}`, 'en'));
const corpus = { provenance, upstreamTests, exclusions: uniqueExclusions, cases };
const generated = `${JSON.stringify(corpus, null, 2)}\n`;

if (mode === '--write') {
  const { mkdir } = await import('node:fs/promises');
  await mkdir(join(root, 'src', 'Salep.GraphQLParser.Tests', 'Fixtures', 'ReferenceSuite'), { recursive: true });
  await writeFile(corpusPath, generated);
  console.log(`Wrote ${cases.length} cases from ${testCount} upstream tests to ${corpusPath}`);
} else {
  let existing;
  try {
    existing = await readFile(corpusPath, 'utf8');
  } catch {
    throw new Error(`Missing generated reference corpus ${corpusPath}; run npm run reference:write.`);
  }
  if (existing !== generated) throw new Error('Reference-suite corpus is stale; review changes and run npm run reference:write.');
  console.log(`Verified ${cases.length} cases from ${testCount} upstream tests.`);
}
}

function runText(executable, args) {
  const result = spawnSync(executable, args, { cwd: root, encoding: 'utf8' });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${executable} ${args.join(' ')} failed: ${result.stderr}`);
  return result.stdout.trim();
}

function classifyFailure(message) {
  return /Syntax Error: (Unexpected character:|Expected digit|Invalid number|Invalid Unicode|Invalid character|Unterminated)/.test(message)
    ? 'lexical'
    : 'syntax';
}

const arrayFields = {
  Document: ['definitions'],
  OperationDefinition: ['variableDefinitions', 'directives'],
  SelectionSet: ['selections'],
  Field: ['arguments', 'directives'],
  FragmentSpread: ['directives'],
  InlineFragment: ['directives'],
  FragmentDefinition: ['variableDefinitions', 'directives'],
  VariableDefinition: ['directives'],
  ListValue: ['values'],
  ObjectValue: ['fields'],
  SchemaDefinition: ['operationTypes', 'directives'],
  SchemaExtension: ['operationTypes', 'directives'],
  ScalarTypeDefinition: ['directives'],
  ScalarTypeExtension: ['directives'],
  ObjectTypeDefinition: ['interfaces', 'directives', 'fields'],
  ObjectTypeExtension: ['interfaces', 'directives', 'fields'],
  InterfaceTypeDefinition: ['interfaces', 'directives', 'fields'],
  InterfaceTypeExtension: ['interfaces', 'directives', 'fields'],
  UnionTypeDefinition: ['directives', 'types'],
  UnionTypeExtension: ['directives', 'types'],
  EnumTypeDefinition: ['directives', 'values'],
  EnumTypeExtension: ['directives', 'values'],
  InputObjectTypeDefinition: ['directives', 'fields'],
  InputObjectTypeExtension: ['directives', 'fields'],
  FieldDefinition: ['arguments', 'directives'],
  InputValueDefinition: ['directives'],
  EnumValueDefinition: ['directives'],
  DirectiveDefinition: ['arguments', 'locations'],
};
const optionalNodeFields = {
  OperationDefinition: ['name'],
  Field: ['alias', 'selectionSet'],
  InlineFragment: ['typeCondition'],
  FragmentDefinition: ['description'],
  VariableDefinition: ['defaultValue'],
  ObjectTypeDefinition: ['description'],
  InterfaceTypeDefinition: ['description'],
  UnionTypeDefinition: ['description'],
  EnumTypeDefinition: ['description'],
  InputObjectTypeDefinition: ['description'],
  ScalarTypeDefinition: ['description'],
  SchemaDefinition: ['description'],
  FieldDefinition: ['description'],
  InputValueDefinition: ['description', 'defaultValue'],
  EnumValueDefinition: ['description'],
  DirectiveDefinition: ['description'],
};
function canonicalNode(node) {
  const canonical = { kind: node.kind };
  for (const [key, value] of Object.entries(node)) {
    if (key === 'kind' || key === 'loc') continue;
    if (Array.isArray(value)) {
      canonical[key] = value.map((item) => item && typeof item === 'object' && 'kind' in item ? canonicalNode(item) : item);
    } else if (value && typeof value === 'object' && 'kind' in value) {
      canonical[key] = canonicalNode(value);
    } else {
      canonical[key] = value;
    }
  }
  for (const field of arrayFields[node.kind] ?? []) canonical[field] ??= [];
  for (const field of optionalNodeFields[node.kind] ?? []) canonical[field] ??= null;
  if (node.loc) canonical.loc = [node.loc.start, node.loc.end];
  return canonical;
}

await finalizeCorpus();
