import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import { join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compareInventory, compareSourcePaths, hasInventoryChanges, stableTestIdentities } from './reference-inventory-core.mjs';

const root = resolve(fileURLToPath(new URL('..', import.meta.url)));
const manifestPath = join(root, 'tests', 'Fixtures', 'ReferenceSuite', 'test-inventory.json');
const mode = process.argv[2];
const checkoutValue = process.env.GRAPHQL_JS_CHECKOUT;
const expectedCommit = '57b385b288150960acd09337adf2fc778abb32ab';
const expectedVersion = '16.14.0';
const testPattern = 'src/**/__tests__/**/*-test.ts';

if (!['--write', '--check'].includes(mode) || !checkoutValue) {
  throw new Error('Usage: GRAPHQL_JS_CHECKOUT=/path/to/pinned-checkout GRAPHQL_JS_NODE=/path/to/node20 node scripts/export-reference-inventory.mjs --write|--check');
}

const checkout = resolve(checkoutValue);
const node = process.env.GRAPHQL_JS_NODE ?? process.execPath;
const nodeVersion = runText(node, ['--version']);
if (!/^v20\./.test(nodeVersion)) {
  throw new Error(`The pinned suite inventory requires Node 20; found ${nodeVersion}. Set GRAPHQL_JS_NODE to a Node 20 executable.`);
}

const upstreamHead = runText('git', ['-C', checkout, 'rev-parse', 'HEAD']);
if (upstreamHead !== expectedCommit) throw new Error(`Expected pinned commit ${expectedCommit}; found ${upstreamHead}.`);
const packageJson = JSON.parse(await readFile(join(checkout, 'package.json'), 'utf8'));
const graphqlPackageJson = JSON.parse(await readFile(join(root, 'node_modules', 'graphql', 'package.json'), 'utf8'));
if (packageJson.version !== expectedVersion || graphqlPackageJson.version !== expectedVersion) {
  throw new Error(`Expected pinned package version ${expectedVersion}; found repository=${packageJson.version}, installed=${graphqlPackageJson.version}.`);
}
if (packageJson.scripts?.testonly !== `mocha --full-trace ${testPattern}`
    || !packageJson.scripts?.test?.includes('npm run testonly')
    || !packageJson.scripts?.['check:integrations']?.includes('integrationTests/*-test.js')) {
  throw new Error('The pinned standard test commands no longer match the inventory enumerator. Review the suite definitions before updating this tool.');
}
const mochaPackageJson = JSON.parse(await readFile(join(checkout, 'node_modules', 'mocha', 'package.json'), 'utf8'));
const lockFile = JSON.parse(await readFile(join(checkout, 'package-lock.json'), 'utf8'));
const lockedMochaVersion = lockFile.packages?.['node_modules/mocha']?.version;
if (mochaPackageJson.version !== '9.2.2' || lockedMochaVersion !== mochaPackageJson.version) {
  throw new Error(`Expected pinned Mocha 9.2.2; found installed=${mochaPackageJson.version}, lock=${lockedMochaVersion}.`);
}

const mochaPath = join(checkout, 'node_modules', 'mocha', 'bin', 'mocha');
const mocha = spawnSync(node, [mochaPath, '--dry-run', '--reporter', 'json', testPattern], {
  cwd: checkout,
  encoding: 'utf8',
  maxBuffer: 32 * 1024 * 1024,
  env: { PATH: process.env.PATH ?? '' },
});
if (mocha.error) throw mocha.error;
if (mocha.status !== 0) throw new Error(`Could not enumerate pinned tests (${mocha.status}).\n${mocha.stdout}\n${mocha.stderr}`);
const report = JSON.parse(mocha.stdout);
if (!Array.isArray(report.tests) || report.tests.length === 0) throw new Error('Mocha did not report any test identities.');
if (report.stats?.tests !== report.tests.length) {
  throw new Error(`Mocha reported ${report.stats?.tests} tests but serialized ${report.tests.length} identities.`);
}
if (report.tests.some((test) => !test.file || !test.fullTitle || !test.title)) {
  throw new Error('Every enumerated test must include its source file, full title, and title.');
}

const sourceFiles = new Set();
sourceFiles.add('package.json');
sourceFiles.add('package-lock.json');
sourceFiles.add('.mocharc.yml');
sourceFiles.add('resources/ts-register.js');
const rawTests = report.tests.map((test) => ({
  file: normalizePath(relative(checkout, test.file)),
  fullTitle: test.fullTitle,
  title: test.title,
}));
for (const test of rawTests) sourceFiles.add(test.file);

// npm test runs these integration cases after building a package archive.
// Load their identities from source metadata instead of running a mutating build.
const integrationFile = 'integrationTests/integration-test.js';
const integrationSource = await readFile(join(checkout, integrationFile), 'utf8');
const integrationProjects = [...integrationSource.matchAll(/testOnNodeProject\('([^']+)'\)/g)].map((match) => match[1]);
if (integrationProjects.length === 0) throw new Error('Could not enumerate package integration projects.');
sourceFiles.add(integrationFile);
for (const project of integrationProjects) {
  const packagePath = `integrationTests/${project}/package.json`;
  sourceFiles.add(packagePath);
  const fixturePackage = JSON.parse(await readFile(join(checkout, packagePath), 'utf8'));
  if (typeof fixturePackage.description !== 'string' || fixturePackage.description.length === 0) {
    throw new Error(`Integration project ${project} has no test description.`);
  }
  rawTests.push({
    file: integrationFile,
    fullTitle: `Integration Tests ${fixturePackage.description}`,
    title: fixturePackage.description,
  });
}

const identities = stableTestIdentities(rawTests).map((test) => ({
  ...test,
  ...classifyTestFile(test.file),
}));
if (identities.length !== report.tests.length + integrationProjects.length) {
  throw new Error('Some discovered test identities were not classified.');
}
if (new Set(identities.map((test) => test.id)).size !== identities.length) {
  throw new Error('Duplicate stable test identities were generated.');
}

const auxiliaryTestFiles = await findFiles(join(checkout, 'src'), (file) => /-fuzz\.ts$/.test(file));
for (const file of auxiliaryTestFiles) sourceFiles.add(normalizePath(relative(checkout, file)));
for (const file of await findFiles(join(checkout, 'src'), (candidate) => candidate.endsWith('.ts'))) {
  sourceFiles.add(normalizePath(relative(checkout, file)));
}
await verifyPinnedSourceTree(checkout, expectedCommit, sourceFiles);
const sourceSha256 = {};
for (const file of [...sourceFiles].sort()) {
  sourceSha256[file] = createHash('sha256').update(await readFile(join(checkout, file))).digest('hex');
}

const classificationCounts = Object.fromEntries(
  [...new Set(identities.map((test) => test.applicability))].sort().map((applicability) => [
    applicability,
    identities.filter((test) => test.applicability === applicability).length,
  ]),
);
const manifest = {
  schemaVersion: 1,
  provenance: {
    project: 'graphql-js',
    version: expectedVersion,
    commit: upstreamHead,
    enumeration: `Mocha 9.2.2 dry-run --reporter json ${testPattern}; pinned config, loader, package scripts, and all src TypeScript inputs are verified`,
    integrationEnumeration: 'Source declarations and fixture package descriptions; integration suites not executed.',
    nodeMajorVersion: 20,
    mochaVersion: mochaPackageJson.version,
    coreTestFileCount: new Set(rawTests.slice(0, report.tests.length).map((test) => test.file)).size,
    coreTestIdentityCount: report.tests.length,
    corePendingTestCount: report.stats.pending ?? 0,
    integrationTestIdentityCount: integrationProjects.length,
    totalTestIdentityCount: identities.length,
    auxiliaryFuzzFiles: auxiliaryTestFiles.map((file) => ({
      file: normalizePath(relative(checkout, file)),
      applicability: file.includes('/language/') ? 'applicable' : 'contract-review',
      invocation: 'npm run fuzzonly',
      reason: file.includes('/language/')
        ? 'Exercises GraphQL block-string language behavior through generated inputs.'
        : 'Exercises an AST utility that requires review against the public parser API contract.',
    })).sort((left, right) => left.file < right.file ? -1 : left.file > right.file ? 1 : 0),
    classificationCounts,
    sourceSha256,
  },
  tests: identities,
};

const generated = `${JSON.stringify(manifest, null, 2)}\n`;
if (mode === '--write') {
  const { mkdir } = await import('node:fs/promises');
  await mkdir(join(root, 'tests', 'Fixtures', 'ReferenceSuite'), { recursive: true });
  await writeFile(manifestPath, generated);
  console.log(`Wrote ${identities.length} classified identities (${report.tests.length} core, ${integrationProjects.length} integration) from ${Object.keys(sourceSha256).length} pinned files.`);
} else {
  const existingText = await readFile(manifestPath, 'utf8').catch(() => null);
  if (existingText === null) throw new Error(`Missing inventory ${manifestPath}; run npm run reference:inventory:write.`);
  const existing = JSON.parse(existingText);
  const changes = compareInventory(existing, manifest);
  if (hasInventoryChanges(changes) || existingText !== generated) {
    throw new Error(`Pinned test inventory is stale. Identity changes: +${changes.added.length}/-${changes.removed.length}; source changes: +${changes.addedSources.length}/-${changes.removedSources.length}/~${changes.changedSources.length}. Review and regenerate with npm run reference:inventory:write.`);
  }
  console.log(`Verified ${identities.length} classified test identities from ${Object.keys(sourceSha256).length} pinned files.`);
}

function classifyTestFile(file) {
  if (file.startsWith('src/language/__tests__/')) {
    return {
      area: 'language',
      applicability: 'applicable',
      reason: 'Exercises GraphQL lexical, syntactic, AST, source, printing, traversal, or language-helper behavior.',
    };
  }
  if (file === 'src/error/__tests__/GraphQLError-test.ts'
      || (file.startsWith('src/utilities/__tests__/')
        && /(?:concatAST|getOperationAST|separateOperations|sortValueNode|stripIgnoredCharacters)-test\.ts$/.test(file))) {
    return {
      area: file.startsWith('src/error/') ? 'error-api' : 'ast-utility-api',
      applicability: 'contract-review',
      reason: 'May be part of the parser package public surface; resolve against the project-owned API contract before including it in conformance claims.',
    };
  }
  if (file.startsWith('integrationTests/')) {
    return {
      area: 'package-integration',
      applicability: 'out-of-scope',
      reason: 'Exercises package installation in downstream runtime projects, outside the selected GraphQL language and parser API boundary.',
    };
  }
  if (file.startsWith('src/validation/')) {
    return {
      area: 'validation',
      applicability: 'out-of-scope',
      reason: 'Exercises schema-dependent GraphQL validation rules.',
    };
  }
  if (file.startsWith('src/execution/')) {
    return {
      area: 'execution',
      applicability: 'out-of-scope',
      reason: 'Exercises request execution, value coercion, subscriptions, or response behavior.',
    };
  }
  if (file.startsWith('src/type/')) {
    return {
      area: 'schema-model',
      applicability: 'out-of-scope',
      reason: 'Exercises semantic schema construction and type-system behavior rather than SDL syntax parsing.',
    };
  }
  if (file.startsWith('src/utilities/')) {
    return {
      area: 'schema-or-runtime-utility',
      applicability: 'out-of-scope',
      reason: 'Exercises schema construction, validation, coercion, execution preparation, or runtime utilities outside the parser API boundary.',
    };
  }
  if (file === 'src/error/__tests__/locatedError-test.ts') {
    return {
      area: 'execution-error',
      applicability: 'out-of-scope',
      reason: 'Exercises resolver error propagation and response-path formatting.',
    };
  }
  if (file.startsWith('src/jsutils/')) {
    return {
      area: 'internal-runtime-utility',
      applicability: 'out-of-scope',
      reason: 'Exercises internal JavaScript runtime support helpers rather than public GraphQL syntax behavior.',
    };
  }
  if (file.startsWith('src/__testUtils__/')) {
    return {
      area: 'test-infrastructure',
      applicability: 'out-of-scope',
      reason: 'Exercises upstream test-only helper functions.',
    };
  }
  if (file.startsWith('src/__tests__/')) {
    return {
      area: 'end-to-end-runtime',
      applicability: 'out-of-scope',
      reason: 'Exercises end-to-end schema, validation, introspection, or execution behavior.',
    };
  }
  throw new Error(`No classification rule exists for pinned test file: ${file}`);
}

function normalizePath(file) {
  return file.split(sep).join('/');
}

async function verifyPinnedSourceTree(sourceRoot, commit, discoveredFiles) {
  const tree = runBuffer('git', ['-C', sourceRoot, 'ls-tree', '-r', '--name-only', '-z', commit, '--', 'src', 'integrationTests', 'resources', '.mocharc.yml', 'package.json', 'package-lock.json'])
    .toString('utf8')
    .split('\0')
    .filter(Boolean);
  const pinnedFiles = tree.filter((file) =>
    (/^src\/.*\.ts$/.test(file))
    || file === 'integrationTests/integration-test.js'
    || /^integrationTests\/[^/]+\/package\.json$/.test(file)
    || file === '.mocharc.yml'
    || file === 'resources/ts-register.js'
    || file === 'package.json'
    || file === 'package-lock.json');
  const expected = [...new Set(discoveredFiles)].sort();
  const tracked = pinnedFiles.sort();
  const differences = compareSourcePaths(tracked, expected);
  if (differences.added.length || differences.removed.length) {
    throw new Error(`The discovered suite files do not match the pinned Git tree. Untracked/additional: ${differences.added.join(', ') || 'none'}; missing from checkout: ${differences.removed.join(', ') || 'none'}.`);
  }

  for (const file of expected) {
    const workingTree = await readFile(join(sourceRoot, file));
    const committed = runBuffer('git', ['-C', sourceRoot, 'show', `${commit}:${file}`]);
    if (!workingTree.equals(committed)) {
      throw new Error(`Pinned suite source differs from ${commit}:${file}; restore the checkout before inventory generation.`);
    }
  }
}

async function findFiles(directory, predicate) {
  const found = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const fullPath = join(directory, entry.name);
    if (entry.isDirectory()) found.push(...await findFiles(fullPath, predicate));
    else if (predicate(fullPath)) found.push(fullPath);
  }
  return found;
}

function runText(executable, args) {
  const result = spawnSync(executable, args, { cwd: root, encoding: 'utf8', env: { PATH: process.env.PATH ?? '' } });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${executable} ${args.join(' ')} failed: ${result.stderr}`);
  return result.stdout.trim();
}

function runBuffer(executable, args) {
  const result = spawnSync(executable, args, { cwd: root, maxBuffer: 32 * 1024 * 1024, env: { PATH: process.env.PATH ?? '' } });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${executable} ${args.join(' ')} failed: ${result.stderr?.toString('utf8')}`);
  return result.stdout;
}
