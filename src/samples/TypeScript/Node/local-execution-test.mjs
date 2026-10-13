import { resolveLocalSelections, coerceLocalInput, localValue } from './dist/Generated/gql/local-execution.js';
const data = { books: [{ __typename: 'Book', id: 'one' }] };
const resolvers = { Book: { label: async (parent, args, context) => context.prefix + parent.id + args.suffix } };
const result = await resolveLocalSelections(data, [{ name: 'books', parentType: 'Query', children: [
  { name: 'label', responseName: 'display', parentType: 'Book', local: true, arguments: { suffix: { kind: 'variable', name: 'suffix' } }, include: [{ kind: 'variable', name: 'show' }] }
]}], { suffix: '!', show: true }, resolvers, { prefix: '#' });
if (result.books[0]?.display !== '#one!') throw new Error('Resolver result missing');
if ('display' in data.books[0]) throw new Error('Input was mutated');
const omitted = await resolveLocalSelections({}, [{ name: 'label', parentType: 'Book', local: true, skip: [{ kind: 'literal', value: true }] }], {}, {}, {});
if ('label' in omitted) throw new Error('Skipped selection was resolved');
let missing = false;
try { await resolveLocalSelections({}, [{ name: 'label', parentType: 'Book', local: true }], {}, {}, {}); } catch { missing = true; }
if (!missing) throw new Error('Missing resolver was accepted');
console.log('Local execution passed async, aliases, lists, arguments, conditions, immutability and missing-resolver checks');

const special = await resolveLocalSelections({}, [{ name: 'label', responseName: '__proto__', parentType: 'Book', local: true }], {}, { Book: { label: () => ({ safe: true }) } }, {});
if (!Object.hasOwn(special, '__proto__') || Object.getPrototypeOf(special) !== Object.prototype) throw new Error('Special alias changed object prototype');
let inherited = false;
try { await resolveLocalSelections({}, [{ name: 'toString', parentType: 'Book', local: true }], {}, { Book: {} }, {}); } catch { inherited = true; }
if (!inherited) throw new Error('Inherited property was treated as a resolver');

// Active repeated fields merge across fragments; aliases remain separate response keys.
let mergedCalls = 0;
const label = { name: 'label', parentType: 'Book', local: true };
const merged = await resolveLocalSelections({}, [label, { parentType: 'Book', typeCondition: 'Book', children: [label] },
  { ...label, include: [{ kind: 'variable', name: 'show' }] }, { ...label, responseName: 'other' }],
  { show: false }, { Book: { label: async () => { mergedCalls++; return 'merged'; } } }, {});
if (mergedCalls !== 2 || merged.label !== 'merged' || merged.other !== 'merged') throw new Error('Repeated fields did not merge by response key');
let parentCalls = 0;
const object = { name: 'book', parentType: 'Query', local: true };
const nested = await resolveLocalSelections({}, [{ ...object, children: [{ name: 'id', parentType: 'Book' }] },
  { ...object, children: [{ name: '__typename', responseName: 'kind', parentType: 'Book', local: true }, { name: 'title', parentType: 'Book' }] }], {},
  { Query: { book: async () => { parentCalls++; return { id: 'one', title: 'Title' }; } } }, {});
if (parentCalls !== 1 || nested.book.id !== 'one' || nested.book.title !== 'Title' || nested.book.kind !== 'Book') throw new Error('Merged child selections or synthesized typename failed');
const abstract = await resolveLocalSelections({ __typename: 'Book' }, [{ name: '__typename', parentType: 'Entry', parentTypeIsAbstract: true }], {}, {}, {});
if (abstract.__typename !== 'Book') throw new Error('Concrete discriminator was replaced with abstract type');
let absentType = false;
try { await resolveLocalSelections({}, [{ name: '__typename', parentType: 'Entry', parentTypeIsAbstract: true }], {}, {}, {}); } catch { absentType = true; }
if (!absentType) throw new Error('Abstract typename was guessed');
console.log('Local execution passed merged resolver, nested selection and typename regressions');

// Alias output must not become the source parent for sibling selections.
const aliasParent = await resolveLocalSelections({ id: 'original' }, [
  { name: 'label', responseName: 'id', parentType: 'Book', local: true },
  { name: 'copy', parentType: 'Book', local: true },
  { name: 'id', responseName: 'originalId', parentType: 'Book' }
], {}, { Book: { label: () => 'replacement', copy: parent => parent.id } }, {});
if (aliasParent.copy !== 'original' || aliasParent.originalId !== 'original') throw new Error('Alias polluted resolver or default field parent');

const named = name => ({ kind: 'named', name });
const required = ofType => ({ kind: 'non-null', ofType });
const list = ofType => ({ kind: 'list', ofType });
const complete = (value, type, definitions = {}) => resolveLocalSelections({}, [
  { name: 'value', parentType: 'Query', local: true, outputType: type }
], {}, { Query: { value: () => value } }, {}, definitions).then(result => result.value);
const reject = async (action, label) => {
  let failed = false;
  try { await action(); } catch { failed = true; }
  if (!failed) throw new Error(`Accepted invalid ${label}`);
};
if (await complete(undefined, named('String')) !== null) throw new Error('Nullable undefined was not completed as null');
const nullableItems = await complete([undefined, 'ok'], list(named('String')));
if (nullableItems[0] !== null || nullableItems[1] !== 'ok') throw new Error('Nullable list completion failed');
await reject(() => complete(undefined, required(named('String'))), 'non-null output');
await reject(() => complete([undefined], list(required(named('String')))), 'non-null output list member');
for (const [type, value, expected] of [['Int', '42', 42], ['Float', true, 1], ['String', 42, '42'], ['Boolean', 0, false], ['ID', 42, '42']])
  if (await complete(value, named(type)) !== expected) throw new Error(`${type} output serialization failed`);
for (const [type, value] of [['Int', 'bad'], ['Int', 2147483648], ['Int', 1.5], ['Float', Infinity], ['String', {}], ['Boolean', 'false'], ['ID', 1.5]])
  await reject(() => complete(value, named(type)), `${type} output`);
const enums = { Color: { kind: 'enum', values: ['RED'] } };
if (await complete('RED', named('Color'), enums) !== 'RED') throw new Error('Enum output changed');
await reject(() => complete('BLUE', named('Color'), enums), 'enum output');
const entryType = { ...named('Entry'), abstract: true, possibleTypes: ['Book'] };
await reject(() => complete({}, entryType), 'missing abstract discriminator');
await reject(() => complete({ __typename: 'Other' }, entryType), 'impossible abstract discriminator');
if ((await complete({ __typename: 'Book' }, entryType)).__typename !== 'Book') throw new Error('Valid abstract output changed');

const exactInteger = localValue({ kind: 'integer', value: '9007199254740993' }, {});
if (coerceLocalInput(exactInteger, named('ID'), {}) !== '9007199254740993') throw new Error('Integer ID precision was lost');
await reject(() => coerceLocalInput(exactInteger, named('Int'), {}), 'out-of-range integer literal');
if (coerceLocalInput(localValue({ kind: 'integer', value: '42' }, {}), named('Int'), {}) !== 42) throw new Error('Int literal coercion failed');
if (coerceLocalInput(exactInteger, named('Float'), {}) !== Number('9007199254740993')) throw new Error('Float integer coercion failed');
const listInput = coerceLocalInput([undefined, 'ok'], list(named('String')), {});
if (listInput[0] !== null) throw new Error('Nullable input list member was not null');
const sparseInput = coerceLocalInput(Array(1), list(named('String')), {});
if (!Object.hasOwn(sparseInput, 0) || sparseInput[0] !== null) throw new Error('Sparse nullable list member was not completed as null');
await reject(() => coerceLocalInput(Array(1), list(required(named('String'))), {}), 'sparse non-null input list member');
await reject(() => coerceLocalInput([undefined], list(required(named('String'))), {}), 'undefined non-null input list member');
const defaulted = coerceLocalInput({ value: undefined }, named('Options'), {
  Options: { kind: 'object', fields: { value: { type: named('String'), defaultValue: { kind: 'literal', value: 'default' } } } }
});
if (defaulted.value !== 'default') throw new Error('Object omission failed to retain defaults');
for (const scalar of ['constructor', 'toString'])
  if (coerceLocalInput(localValue({ kind: 'integer', value: '42' }, {}), named(scalar), {}) !== 42) throw new Error(`Inherited definition affected custom scalar ${scalar}`);
const customLiteral = localValue({ kind: 'object', fields: { values: { kind: 'list', items: [{ kind: 'integer', value: '42' }] } } }, {});
const custom = coerceLocalInput(customLiteral, named('JSON'), {});
if (custom.values[0] !== 42 || Object.getOwnPropertySymbols(custom.values[0]).length) throw new Error('Private integer wrapper escaped custom scalar coercion');
console.log('Local execution passed parent isolation, output completion, abstract validation and exact integer input regressions');

const synchronousAbort = new AbortController();
let unhandled;
const observeUnhandled = reason => { unhandled = reason; };
process.on('unhandledRejection', observeUnhandled);
try {
  await reject(() => resolveLocalSelections({}, [{ name: 'value', parentType: 'Query', local: true }], {}, {
    Query: { value: () => { synchronousAbort.abort(new Error('cancelled')); return Promise.reject(new Error('resolver rejected')); } }
  }, {}, {}, synchronousAbort.signal), 'synchronous cancellation');
  await new Promise(resolve => setImmediate(resolve));
  if (unhandled) throw new Error('Cancelled resolver rejection was unhandled');
} finally { process.off('unhandledRejection', observeUnhandled); }
console.log('Local execution passed synchronous abort with rejecting resolver regression');

const rawObject = await resolveLocalSelections({}, [{ name: 'book', parentType: 'Query', local: true, children: [
  { name: 'title', responseName: 'id', parentType: 'Book' },
  { name: 'nested', parentType: 'Book', children: [{ name: 'title', responseName: 'id', parentType: 'Book' }] }
] }], {}, { Query: { book: () => ({ id: 'source-id', title: 'source-title', secret: 'unselected', nested: { id: 'nested-id', title: 'nested-title', secret: 'unselected' } }) } }, {});
if (rawObject.book.id !== 'source-title' || rawObject.book.nested.id !== 'nested-title') throw new Error('Raw local object alias resolved response key instead of source field');
if (Object.keys(rawObject.book).sort().join(',') !== 'id,nested' || Object.keys(rawObject.book.nested).join(',') !== 'id') throw new Error('Raw local object retained unselected fields');
const projectedRemote = await resolveLocalSelections({ book: { id: 'projected-title', __typename: 'Book' } }, [{
  name: 'book', parentType: 'Query', children: [{ name: 'title', responseName: 'id', parentType: 'Book' }]
}], {}, {}, {});
if (projectedRemote.book.id !== 'projected-title') throw new Error('Remote response alias was resolved as a raw source field');
console.log('Local execution passed raw object projection and remote alias preservation regressions');

let wrongConcreteCalls = 0;
const concreteIdentity = await resolveLocalSelections({}, [{ name: 'book', parentType: 'Query', local: true,
  outputType: { ...named('Book'), abstract: false, possibleTypes: ['Book'] }, children: [
    { name: '__typename', parentType: 'Book', parentTypeIsAbstract: false },
    { parentType: 'Book', typeCondition: 'Book', possibleTypes: ['Book'], children: [
      { name: 'label', parentType: 'Book', local: true, outputType: named('String') }
    ] },
    { name: 'nested', parentType: 'Book', outputType: { ...named('Book'), abstract: false, possibleTypes: ['Book'] }, children: [
      { name: '__typename', parentType: 'Book', parentTypeIsAbstract: false },
      { parentType: 'Book', typeCondition: 'Book', possibleTypes: ['Book'], children: [{ name: 'title', responseName: 'id', parentType: 'Book' }] }
    ] }
] }], {}, {
  Query: { book: () => ({ __typename: 'Author', nested: { __typename: 'Author', id: 'wrong', title: 'nested-title', secret: 'hidden' } }) },
  Book: { label: () => 'book-label' }, Author: { label: () => { wrongConcreteCalls++; return 'author-label'; } }
}, {});
if (wrongConcreteCalls || concreteIdentity.book.__typename !== 'Book' || concreteIdentity.book.label !== 'book-label' || concreteIdentity.book.nested.__typename !== 'Book' || concreteIdentity.book.nested.id !== 'nested-title' || 'secret' in concreteIdentity.book.nested)
  throw new Error('Stale raw typename controlled concrete identity, fragments, resolver dispatch or projection');
console.log('Local execution passed concrete local identity regression');
