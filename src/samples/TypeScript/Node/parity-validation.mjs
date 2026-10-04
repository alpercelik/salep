import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildSchema, parse, validate, validateSchema, specifiedRules, NoUnusedFragmentsRule } from 'graphql';

// Original Salep inputs. graphql-js provides an independent validation oracle.
const schema = `
  directive @label(value: String!) repeatable on FIELD
  type Query { item(id: ID!): Item! text: String! count: Int options(value: Options!): String choice(value: Choice): String }
  type Item { id: ID! name: String! child: Item }
  enum Tag { NOVEL HISTORY }
  input Options { tag: Tag! limit: Int = 10 }
  input Choice @oneOf { id: ID name: String }
`;
const cases = [
  ['OneOf valid literal', 'query Check { choice(value: {id: "1"}) }'],
  ['OneOf empty literal', 'query Check { choice(value: {}) }'],
  ['OneOf multiple members', 'query Check { choice(value: {id: "1", name: "x"}) }'],
  ['OneOf null member', 'query Check { choice(value: {id: null}) }'],
  ['OneOf required member variable', 'query Check($id: ID!) { choice(value: {id: $id}) }'],
  ['OneOf nullable member variable', 'query Check($id: ID) { choice(value: {id: $id}) }'],
  ['OneOf nullable member variable default', 'query Check($id: ID = "1") { choice(value: {id: $id}) }'],
  ['OneOf nullable object variable', 'query Check($value: Choice) { choice(value: $value) }'],
  ['required argument', 'query Check { item { id } }'],
  ['undefined variable', 'query Check { item(id: $id) { id } }'],
  ['missing object selection', 'query Check { item(id: "1") }'],
  ['scalar subselection', 'query Check { text { id } }'],
  ['alias conflict', 'query Check { item(id: "1") { value: id value: name } }'],
  ['wrong variable type', 'query Check($id: Int!) { item(id: $id) { id } }'],
  ['nullable variable', 'query Check($id: ID) { item(id: $id) { id } }'],
  ['default variable', 'query Check($id: ID = "1") { item(id: $id) { id } }'],
  ['required variable', 'query Check($id: ID!) { item(id: $id) { id } }'],
  ['null required argument', 'query Check { item(id: null) { id } }'],
  ['duplicate argument', 'query Check { item(id: "1", id: "2") { id } }'],
  ['unknown argument', 'query Check { item(id: "1", extra: true) { id } }'],
  ['unused variable', 'query Check($id: ID) { text }'],
  ['duplicate variable', 'query Check($id: ID, $id: ID) { item(id: $id) { id } }'],
  ['invalid directive value', 'query Check { text @include(if: 1) }'],
  ['missing directive argument', 'query Check { text @include }'],
  ['directive location', 'query Check @include(if: true) { text }'],
  ['duplicate nonrepeatable directive', 'query Check { text @include(if: true) @include(if: false) }'],
  ['repeatable directive', 'query Check { text @label(value: "a") @label(value: "b") }'],
  ['unknown directive', 'query Check { text @unknown }'],
  ['required input field', 'query Check { options(value: {}) }'],
  ['enum literal', 'query Check { options(value: {tag: NOVEL}) }'],
  ['invalid enum literal', 'query Check { options(value: {tag: OTHER}) }'],
  ['unknown input field', 'query Check { options(value: {tag: NOVEL, extra: 1}) }'],
  ['duplicate input field', 'query Check { options(value: {tag: NOVEL, tag: HISTORY}) }'],
  ['integer boundary', 'query Check { options(value: {tag: NOVEL, limit: 2147483647}) }'],
  ['integer overflow', 'query Check { options(value: {tag: NOVEL, limit: 2147483648}) }'],
  ['scalar fragment', 'query Check { text } fragment Bad on String { x }'],
  ['impossible fragment', 'query Check { item(id: "1") { ... on Query { text } } }'],
  ['unused fragment library', 'query Check { text } fragment Library on Item { id }'],
  ['nested merge condition', 'query Check($show: Boolean!) { item(id: "1") { id } item(id: "1") @include(if: $show) { name } }'],
  ['literal inclusion', 'query Check { text @include(if: true) }'],
  ['literal exclusion', 'query Check { text @skip(if: true) }'],
  ['conflicting arguments', 'query Check { item(id: "1") { id } item(id: "2") { id } }'],
  ['undefined fragment', 'query Check { item(id: "1") { ...Missing } }'],
  ['nested fragment cycle', 'query Check { item(id: "1") { ...Loop } } fragment Loop on Item { child { ...Loop } }'],
  ['fragment cycle', 'query Check { item(id: "1") { ...A } } fragment A on Item { ...B } fragment B on Item { ...A }']
];
const dll = process.env.SALEP_TYPESCRIPT_CLI ?? fileURLToPath(new URL('../../../../artifacts/bin/Salep.ClientGenerator.TypeScript.Cli/debug_net10.0/salep-typescript.dll', import.meta.url));
const directory = realpathSync(mkdtempSync(join(tmpdir(), 'salep-validation-parity-')));
try {
  writeFileSync(join(directory, 'schema.graphql'), schema);
  writeFileSync(join(directory, 'salep.json'), JSON.stringify({ version: 1, target: 'typescript', schema: 'schema.graphql', documents: 'operation.graphql', output: 'Generated' }));
  const contract = buildSchema(schema);
  assert.deepEqual(validateSchema(contract), []);
  const rules = specifiedRules.filter(rule => rule !== NoUnusedFragmentsRule);
  for (const [name, operation] of cases) {
    writeFileSync(join(directory, 'operation.graphql'), operation);
    const expected = validate(contract, parse(operation), rules).length === 0;
    const result = spawnSync('dotnet', [dll, 'validate', '--config', join(directory, 'salep.json')], { encoding: 'utf8', timeout: 20_000 });
    if (result.error) throw result.error;
    assert.equal(result.status === 0, expected, `${name}: validation differs from graphql-js\n${result.stderr}`);
  }
  console.log(`Salep validation matched graphql-js for ${cases.length} original positive, negative and boundary scenarios`);
} finally {
  rmSync(directory, { recursive: true, force: true });
}
