using Salep.GraphQLParser;

namespace Salep.ClientGenerator.Model;

/// <summary>The standard introspection type contract, independent of a user's schema definitions.</summary>
public static class GraphQlIntrospection
{
    private static readonly Lazy<GraphQlSchemaModel> Model = new(() => GraphQlModelFactory.CreateSchema(
        global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(Definition.AsMemory()))));
    public static IReadOnlyList<GraphQlSchemaType> Types => Model.Value.Types;
    public static IReadOnlyList<GraphQlFieldDefinition> QueryFields => Model.Value.ObjectTypes.Single(type => type.Name == "IntrospectionRoot").Fields;
    private const string Definition = """
        type IntrospectionRoot { __schema: __Schema! __type(name: String!): __Type }
        type __Schema {
          description: String
          types: [__Type!]!
          queryType: __Type!
          mutationType: __Type
          subscriptionType: __Type
          directives: [__Directive!]!
        }
        type __Type {
          kind: __TypeKind!
          name: String
          description: String
          specifiedByURL: String
          fields(includeDeprecated: Boolean! = false): [__Field!]
          interfaces: [__Type!]
          possibleTypes: [__Type!]
          enumValues(includeDeprecated: Boolean! = false): [__EnumValue!]
          inputFields(includeDeprecated: Boolean! = false): [__InputValue!]
          ofType: __Type
          isOneOf: Boolean
        }
        type __Field { name: String! description: String args(includeDeprecated: Boolean! = false): [__InputValue!]! type: __Type! isDeprecated: Boolean! deprecationReason: String }
        type __InputValue { name: String! description: String type: __Type! defaultValue: String isDeprecated: Boolean! deprecationReason: String }
        type __EnumValue { name: String! description: String isDeprecated: Boolean! deprecationReason: String }
        type __Directive { name: String! description: String isRepeatable: Boolean! locations: [__DirectiveLocation!]! args(includeDeprecated: Boolean! = false): [__InputValue!]! }
        enum __TypeKind { SCALAR OBJECT INTERFACE UNION ENUM INPUT_OBJECT LIST NON_NULL }
        enum __DirectiveLocation { QUERY MUTATION SUBSCRIPTION FIELD FRAGMENT_DEFINITION FRAGMENT_SPREAD INLINE_FRAGMENT VARIABLE_DEFINITION SCHEMA SCALAR OBJECT FIELD_DEFINITION ARGUMENT_DEFINITION INTERFACE UNION ENUM ENUM_VALUE INPUT_OBJECT INPUT_FIELD_DEFINITION }
        """;
}
