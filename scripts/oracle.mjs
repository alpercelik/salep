import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { createHash } from 'node:crypto';
import { parse, Source, version } from 'graphql';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const corpusDir = join(root, 'tests', 'Fixtures', 'Oracle');
const expectedDir = join(corpusDir, 'expected');
const mode = process.argv[2];
if (mode !== '--write' && mode !== '--check') {
  throw new Error('Usage: node scripts/oracle.mjs --write|--check');
}
if (mode === '--write') await mkdir(expectedDir, { recursive: true });

const packageJson = JSON.parse(await readFile(join(root, 'node_modules', 'graphql', 'package.json'), 'utf8'));
if (packageJson.version !== '16.14.0' || version !== '16.14.0') {
  throw new Error(`Expected graphql 16.14.0, found ${packageJson.version} (runtime ${version})`);
}

const manifest = JSON.parse(await readFile(join(corpusDir, 'manifest.json'), 'utf8'));
const ids = new Set();
const files = new Set();
for (const item of manifest) {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(item.id) || ids.has(item.id)) {
    throw new Error(`Invalid or duplicate fixture id: ${item.id}`);
  }
  if (files.has(item.file) || item.file !== `${item.id}.graphql`) {
    throw new Error(`Invalid or duplicate fixture path for ${item.id}: ${item.file}`);
  }
  if (item.expect !== 'valid' && item.expect !== 'invalid') {
    throw new Error(`Invalid expected outcome for ${item.id}: ${item.expect}`);
  }
  ids.add(item.id);
  files.add(item.file);
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
      canonical[key] = value.map((item) => item && typeof item === 'object' && 'kind' in item
        ? canonicalNode(item)
        : item);
    } else if (value && typeof value === 'object' && 'kind' in value) {
      canonical[key] = canonicalNode(value);
    } else {
      canonical[key] = value;
    }
  }
  for (const field of arrayFields[node.kind] ?? []) {
    canonical[field] ??= [];
  }
  for (const field of optionalNodeFields[node.kind] ?? []) {
    canonical[field] ??= null;
  }
  if (node.loc) canonical.loc = [node.loc.start, node.loc.end];
  return canonical;
}

function canonicalError(error) {
  return {
    message: error.message,
    locations: (error.locations ?? []).map(({ line, column }) => ({ line, column })),
    positions: error.positions ?? [],
  };
}

const sourceHashes = {};
let failed = false;
for (const item of [...manifest].sort((left, right) => left.id.localeCompare(right.id, 'en'))) {
  const source = await readFile(join(corpusDir, item.file), 'utf8');
  sourceHashes[item.id] = createHash('sha256').update(source, 'utf8').digest('hex');
  const graphQLSource = new Source(source, item.id);
  let generated;
  try {
    const ast = parse(graphQLSource);
    generated = { fixtureId: item.id, graphqlJsVersion: version, outcome: 'valid', ast: canonicalNode(ast) };
  } catch (error) {
    if (error.name !== 'GraphQLError') throw error;
    generated = { fixtureId: item.id, graphqlJsVersion: version, outcome: 'invalid', errors: [canonicalError(error)] };
  }
  if (generated.outcome !== item.expect) {
    throw new Error(`${item.id}: expected ${item.expect} but graphql-js returned ${generated.outcome}`);
  }
  const contents = `${JSON.stringify(generated, null, 2)}\n`;
  const outputPath = join(expectedDir, `${item.id}.json`);
  if (mode === '--write') {
    await writeFile(outputPath, contents);
    process.stdout.write(`wrote ${item.id}\n`);
  } else {
    let existing;
    try {
      existing = await readFile(outputPath, 'utf8');
    } catch {
      failed = true;
      process.stderr.write(`${item.id}: missing oracle snapshot ${outputPath}\n`);
      continue;
    }
    if (existing !== contents) {
      failed = true;
      process.stderr.write(`${item.id}: oracle snapshot mismatch; run npm run oracle:write and review the change\n`);
    } else {
      process.stdout.write(`ok ${item.id}\n`);
    }
  }
}

if (mode === '--check') {
  let expectedFiles = [];
  try {
    expectedFiles = (await readdir(expectedDir)).filter((name) => name.endsWith('.json') && name !== 'provenance.json').sort();
  } catch {
    // Per-fixture checks above report each missing expected artifact with its fixture ID.
  }
  const expectedByManifest = [...ids].map((id) => `${id}.json`).sort();
  if (JSON.stringify(expectedFiles) !== JSON.stringify(expectedByManifest)) {
    failed = true;
    process.stderr.write('manifest: stale or missing oracle snapshots; expected snapshot files must match manifest fixture IDs\n');
  }
}

const provenance = {
  graphqlJsVersion: version,
  graphqlPackageIntegrity: JSON.parse(await readFile(join(root, 'package-lock.json'), 'utf8')).packages['node_modules/graphql'].integrity,
  sourceSha256: sourceHashes,
  generator: 'scripts/oracle.mjs',
};
const provenanceContents = `${JSON.stringify(provenance, null, 2)}\n`;
const provenancePath = join(expectedDir, 'provenance.json');
if (mode === '--write') {
  await writeFile(provenancePath, provenanceContents);
} else {
  let existing;
  try {
    existing = await readFile(provenancePath, 'utf8');
  } catch {
    failed = true;
    process.stderr.write(`provenance: missing oracle provenance ${provenancePath}\n`);
  }
  if (existing !== undefined && existing !== provenanceContents) {
    failed = true;
    process.stderr.write('provenance: source hashes or graphql-js package identity mismatch; run npm run oracle:write and review the change\n');
  }
}

if (failed) process.exitCode = 1;
