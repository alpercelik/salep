import { resolveLocalSelections } from './dist/Generated/gql/local-execution.js';
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
