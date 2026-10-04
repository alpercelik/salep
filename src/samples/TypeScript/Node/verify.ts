import { GraphQLClient, GraphQLRequestError } from './Generated/gql/client.js';
import { ReadBookDocument, SearchCatalogDocument, RenameBookDocument, ConditionalBookDocument, LookupEntryDocument } from './Generated/gql/graphql.js';
import type { ReadBookResult, LookupEntryResult } from './Generated/gql/graphql.js';

function check(condition: unknown, message: string): asserts condition {
  if (!condition) throw new Error(message);
}

function describeEntry(entry: LookupEntryResult['entry']): string {
  if (entry?.__typename === 'Book') {
    // @ts-expect-error interface branch does not include another branch field
    entry.name;
    return entry.title;
  }
  if (entry?.__typename === 'Author') return entry.name;
  return 'absent';
}
check(describeEntry({ __typename: 'Book', id: 'book-1', title: 'Typed interface' }) === 'Typed interface', 'interface narrowing');

let requests = 0;
const transport: typeof fetch = async (_url, init) => {
  requests++;
  const body = JSON.parse(String(init?.body)) as { query: string; operationName: string; variables: Record<string, unknown> };
  check(new Headers(init?.headers).get('Authorization') === 'Bearer sample', 'header propagation');
  check(body.query.includes('fragment BookSummary'), 'fragment closure');
  check(body.variables.id === 'book-1', 'variable encoding');
  return new Response(JSON.stringify({ data: { selected: { id: 'book-1', title: 'Original sample', published: null, authors: ['Writer'] } } }), { status: 200 });
};
const client = new GraphQLClient('https://example.test/graphql', transport, { Authorization: 'Bearer sample' });
const result = await client.execute(ReadBookDocument, { id: 'book-1' });
check(result.selected?.title === 'Original sample', 'typed response');
check(requests === 1, 'one request');

// These checks must fail type checking; tsc also rejects unused @ts-expect-error comments.
if (false) {
  // @ts-expect-error required variable missing
  await client.execute(ReadBookDocument, {});
  // @ts-expect-error GraphQL ID requires string
  await client.execute(ReadBookDocument, { id: 123 });
  // @ts-expect-error mutation requires title
  await client.execute(RenameBookDocument, { id: 'book-1' });
  // @ts-expect-error enum is a literal union
  await client.execute(SearchCatalogDocument, { filter: { category: 'UNKNOWN' } });
  const selected: NonNullable<ReadBookResult['selected']> = { id: 'x', title: 'x', published: null, authors: [] };
  // @ts-expect-error schema fields not selected by this operation are absent
  selected.category;
  const conditional = await client.execute(ConditionalBookDocument, { id: "book-1", show: true });
  // @ts-expect-error a conditional selection may be absent
  const requiredTitle: string = conditional.book!.title;
  const search = await client.execute(SearchCatalogDocument, {});
  if (search.search[0]?.__typename === "Book") {
    // @ts-expect-error schema authors field was not selected
    search.search[0].authors;
  }
}
const errors = new GraphQLClient('https://example.test/graphql', async () => new Response(JSON.stringify({ errors: [{ message: 'Rejected', path: ['book'] }] })));
try {
  await errors.execute(ReadBookDocument, { id: 'book-1' });
  throw new Error('Expected GraphQL error');
} catch (error) {
  check(error instanceof GraphQLRequestError && error.errors[0]?.message === 'Rejected', 'GraphQL errors');
}
console.log('TypeScript client runtime and compile-time checks passed');


// The conditional duplicate parent must not promise title when show is false.
function verifyMerged(value: import('./Generated/gql/graphql.js').ParityChecksResult): void {
  if (value.book) {
    const id: string = value.book.id;
    const title: string | undefined = value.book.title;
    // @ts-expect-error title is absent when the conditional parent is skipped
    const requiredTitle: string = value.book.title;
    void [id, title, requiredTitle];
  }
  if (value.guaranteed) {
    const title: string = value.guaranteed.title;
    // @ts-expect-error a constant excluded field is not in the result type
    value.guaranteed.hidden;
    void title;
  }
}
verifyMerged({ book: { id: 'book-1' }, guaranteed: { title: 'always' } });

const writerFragment: import('./Generated/gql/graphql.js').SearchKeysFragment = { __typename: 'Author', name: 'Writer' };
check(writerFragment.name === 'Writer', 'extended union fragment branch');

const choice: import('./Generated/gql/graphql.js').BookChoice = { id: 'book-1' };
// @ts-expect-error OneOf rejects two selected keys
const bothChoices: import('./Generated/gql/graphql.js').BookChoice = { id: 'book-1', title: 'Title' };
// @ts-expect-error OneOf rejects a null member
const nullChoice: import('./Generated/gql/graphql.js').BookChoice = { id: null };
// @ts-expect-error OneOf rejects an empty input
const emptyChoice: import('./Generated/gql/graphql.js').BookChoice = {};
void [choice, bothChoices, nullChoice, emptyChoice];

// Resolver contracts preserve application context and argument types.
import type { LocalResolvers } from './Generated/gql/resolvers.js';
const localResolvers: LocalResolvers<{ prefix: string }> = {
  Book: { label: async (parent, args, context) => context.prefix + (args.prefix ?? '') + parent.id },
  Query: { localTitle: (_parent, _args, context) => context.prefix }
};
new GraphQLClient('unused', fetch, {}, localResolvers, { prefix: '#' });
const invalidResolvers: LocalResolvers = {
  Book: {
    // @ts-expect-error non-null string output does not allow null
    label: () => null
  }
};
void invalidResolvers;
