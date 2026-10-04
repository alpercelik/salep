import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { GraphQLClient as StandardClient } from 'graphql-request';
import { buildSchema, graphql, print } from 'graphql';
import { GraphQLClient, GraphQLRequestError } from './dist/Generated/gql/client.js';
import { LocalBookDocument, LocalOnlyDocument, InspectSchemaDocument, LookupChoiceDocument, ParityChecksDocument, ParityFragmentsDocument, ReadBookDocument, SearchCatalogDocument, RenameBookDocument, ConditionalBookDocument, LookupEntryDocument } from './dist/Generated/gql/graphql.js';

const schema = buildSchema(await readFile(new URL('./schema.graphql', import.meta.url), 'utf8'));
const book = { id: 'book-1', title: 'First title', published: null, authors: ['Sample author'], __typename: 'Book' };
const rootValue = {
  bookBy: ({ choice }) => choice.id === book.id || choice.title === book.title ? book : null,
  entry: ({ id }) => id === book.id ? book : null,
  book: ({ id }) => id === book.id ? book : null,
  search: () => [book, { __typename: 'Author', id: 'author-1', name: 'Sample author' }],
  renameBook: ({ id, title }) => { if (id !== book.id) throw new Error('Unknown book'); book.title = title; return book; }
};
let requestCount = 0;
const server = createServer(async (request, response) => {
  try {
    requestCount++;
    assert.equal(request.headers.authorization, 'Bearer test');
    let body = '';
    for await (const chunk of request) body += chunk;
    const { query: source, operationName, variables: variableValues } = JSON.parse(body);
    assert.ok(!source.includes('@client') && !source.includes('label('));
    const result = await graphql({ schema, source, operationName, variableValues, rootValue });
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify(result));
  } catch (error) { response.writeHead(500); response.end(String(error)); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
try {
  const standardClient = new StandardClient(`http://127.0.0.1:${server.address().port}/graphql`, { headers: { Authorization: 'Bearer test' } });
  assert.equal(ReadBookDocument.kind, 'Document');
  assert.ok(print(ReadBookDocument).includes('fragment BookSummary'));
  assert.equal((await standardClient.request(ReadBookDocument, { id: book.id })).selected.title, 'First title');
  const client = new GraphQLClient(`http://127.0.0.1:${server.address().port}/graphql`, fetch, { Authorization: 'Bearer test' }, { Book: { label: async (parent, args) => args.prefix + parent.id }, Query: { localTitle: () => 'local title' } });
  assert.equal((await client.execute(ReadBookDocument, { id: book.id })).selected.title, 'First title');
  assert.deepEqual((await client.execute(ConditionalBookDocument, { id: book.id, show: false })).book, { id: book.id });
  assert.equal((await client.execute(ConditionalBookDocument, { id: book.id, show: true })).book.title, "First title");
  assert.equal((await client.execute(LookupEntryDocument, { id: book.id })).entry.__typename, "Book");
  assert.equal((await client.execute(LookupChoiceDocument, { choice: { id: book.id } })).bookBy.id, book.id);
  assert.equal((await client.execute(LocalBookDocument, {})).book.display, '#book-1');
  const beforeLocal = requestCount;
  assert.equal((await client.execute(LocalOnlyDocument, {})).localTitle, 'local title');
  assert.equal(requestCount, beforeLocal);
  const inventory = JSON.parse(await readFile(new URL('./Generated/operations.json', import.meta.url), 'utf8'));
  const id = LocalBookDocument.__meta__.operationId;
  assert.equal(id, createHash('sha256').update(LocalBookDocument.__meta__.localExecution.query.replaceAll('\r\n', '\n')).digest('hex'));
  assert.equal(inventory.operations[id].query, LocalBookDocument.__meta__.localExecution.query.replaceAll('\r\n', '\n'));
  assert.ok(!Object.values(inventory.operations).some(entry => entry.name === 'LocalOnly'));
  assert.equal(LocalOnlyDocument.__meta__.operationId, null);
  const introspection = await client.execute(InspectSchemaDocument, { name: 'Book' });
  assert.equal(introspection.__schema.queryType.name, 'Query');
  assert.equal(introspection.__schema.queryType.kind, 'OBJECT');
  assert.equal(introspection.__type.name, 'Book');
  assert.ok(introspection.__type.fields.some(field => field.name === 'title'));
  const parity = await client.execute(ParityChecksDocument, { id: book.id, show: false });
  assert.deepEqual(parity.book, { id: book.id });
  assert.deepEqual(parity.guaranteed, { title: 'First title' });
  assert.equal((await client.execute(ParityChecksDocument, { id: book.id, show: true })).book.title, 'First title');
  const fragments = await standardClient.request(ParityFragmentsDocument, {});
  assert.deepEqual(fragments.search, [{ __typename: 'Book', id: book.id }, { __typename: 'Author', name: 'Sample author' }]);
  const results = await client.execute(SearchCatalogDocument, {});
  assert.deepEqual(results.search.map(item => item.__typename), ['Book', 'Author']);
  assert.equal((await client.execute(RenameBookDocument, { id: book.id, title: 'Changed title' })).renameBook.title, 'Changed title');
  await assert.rejects(client.execute(RenameBookDocument, { id: 'missing', title: 'No change' }), GraphQLRequestError);
  const controller = new AbortController(); controller.abort();
  await assert.rejects(client.execute(ReadBookDocument, { id: book.id }, { signal: controller.signal }), error => error.name === 'AbortError');
  console.log('Generated TypeScript client passed real HTTP GraphQL query, mutation, union, error and cancellation checks');
} finally { await new Promise(resolve => server.close(resolve)); }
