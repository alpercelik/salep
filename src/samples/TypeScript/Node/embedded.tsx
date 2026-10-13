// Static documents may live beside application code. The helper is application-owned.
import { graphql as request } from './Generated/gql/gql.js';
export const conditionalSource = request(`
  query ConditionalBook($id: ID!, $show: Boolean!) {
    book(id: $id) {
      id
      ... on Book @include(if: $show) { title }
    }
  }
`);
