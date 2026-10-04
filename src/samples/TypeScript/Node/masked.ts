import { ReadBookDocument, BookSummaryFragmentDocument } from './MaskedGenerated/gql/graphql.js';
import type { ReadBookResult } from './MaskedGenerated/gql/graphql.js';
import { getFragmentData } from './MaskedGenerated/gql/fragment-masking.js';
import type { FragmentType } from './MaskedGenerated/gql/fragment-masking.js';

function bookTitle(book: FragmentType<typeof BookSummaryFragmentDocument>): string {
  return getFragmentData(BookSummaryFragmentDocument, book).title;
}
function describeResponse(result: ReadBookResult): string | null {
  if (!result.selected) return null;
  // @ts-expect-error named fragment selections are masked until the helper is used
  result.selected.title;
  return bookTitle(result.selected);
}
const payload = { selected: { id: 'book-1', title: 'Masked title', published: null, authors: [] } } as unknown as ReadBookResult;
if (describeResponse(payload) !== 'Masked title') throw new Error('Fragment masking helper failed');
if (ReadBookDocument.kind !== 'Document') throw new Error('Expected a standard typed GraphQL AST');
console.log('Fragment masking type and runtime checks passed');
