using System.Text.Json;
using Salep.ClientGenerator.Targets;
using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.TypeScript.Targets;

/// <summary>Input/output mappings for a custom GraphQL scalar.</summary>
public sealed record TypeScriptScalar(string Input, string Output);

/// <summary>TypeScript policy independent of GraphQL syntax adapters and C# projections.</summary>
public sealed class TypeScriptCodeGenerationTarget(IReadOnlyDictionary<string, TypeScriptScalar>? scalars = null) : ICodeGenerationTarget
{
    public string Id => "typescript";
    public string DefaultNamespace => "graphql";
    private static string PreferredName(string graphQlName) => graphQlName is "string" or "number" or "boolean" or "unknown" or "any" or "never" or "object" or "null" or "undefined" or "type" or "interface" or "class" or "default" or "export" or "import" or "const" or "let" or "var" or "function" or "enum" or "GraphQLClient" or "TypedDocument" or "TypedDocumentNode" or "GraphQLError" or "RequestOptions" or "FragmentType" or "Scalars"
        ? "Gql" + graphQlName : graphQlName;
    private readonly Dictionary<string, string> typeNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> operationNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> fragmentNames = new(StringComparer.Ordinal);
    private bool namesPrepared;
    public string TypeName(string graphQlName) => typeNames.GetValueOrDefault(graphQlName, PreferredName(graphQlName));
    public string OperationName(string name) => operationNames.GetValueOrDefault(name, name);
    public string FragmentName(string name) => fragmentNames.GetValueOrDefault(name, name + "Fragment");

    public void PrepareNames(GraphQlSchemaModel schema, GraphQlExecutableDocument document)
    {
        if (namesPrepared) return;
        var occupied = new HashSet<string>(["ReadonlyArray", "Record", "Scalars", "TypedDocument", "TypedDocumentNode", "GraphQLClient", "GraphQLRequestError", "GraphQLError", "RequestOptions", "FragmentType", "FragmentReference", "getFragmentData", "graphql"], StringComparer.Ordinal);
        string Allocate(string preferred, Func<string, string[]> symbols)
        {
            var candidate = preferred;
            for (var suffix = 2; symbols(candidate).Any(occupied.Contains); suffix++) candidate = "Gql" + preferred + (suffix == 2 ? "" : suffix.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var symbol in symbols(candidate)) occupied.Add(symbol);
            return candidate;
        }
        foreach (var operation in document.Operations.OrderBy(operation => operation.Name, StringComparer.Ordinal))
        {
            var kind = char.ToUpperInvariant(operation.OperationType[0]) + operation.OperationType[1..];
            operationNames.Add(operation.Name!, Allocate(PreferredName(operation.Name!), stem => [stem + "Result", stem + "Variables", stem + "Document", stem + kind, stem + kind + "Variables"]));
        }
        foreach (var fragment in document.Fragments.OrderBy(fragment => fragment.Name, StringComparer.Ordinal))
            fragmentNames.Add(fragment.Name, Allocate(PreferredName(fragment.Name) + "Fragment", name => [name, name + "Document"]));
        foreach (var name in schema.Types.Concat(GraphQlIntrospection.Types.OfType<GraphQlEnumType>()).Where(type => type is GraphQlEnumType or GraphQlInputObjectType).Select(type => type.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            typeNames.Add(name, Allocate(PreferredName(name), candidate => [candidate]));
        namesPrepared = true;
    }
    public string InterfaceName(string graphQlName) => TypeName(graphQlName);
    public string PropertyName(string graphQlName) => StringLiteral(graphQlName);
    public string EnumValueName(string graphQlName) => StringLiteral(graphQlName);
    public string MethodName(string graphQlName) => graphQlName;
    public string ParameterName(string graphQlName) => graphQlName;
    public string StringLiteral(string value) => JsonSerializer.Serialize(value);
    public bool IsValueType(GraphQlTypeReference type, GraphQlSchemaModel schema) => false;
    public IReadOnlyList<string> GetImports(GraphQlSchemaModel schema) => [];
    public string RenderType(GraphQlTypeReference type, GraphQlSchemaModel schema) => Render(type, schema, false);

    public string Render(GraphQlTypeReference type, GraphQlSchemaModel schema, bool input, string? selectedType = null)
        => type is GraphQlNonNullTypeReference required
            ? NonNull(required.NullableType, schema, input, selectedType)
            : NonNull(type, schema, input, selectedType) + " | null";

    private string NonNull(GraphQlTypeReference type, GraphQlSchemaModel schema, bool input, string? selectedType)
        => type switch
        {
            GraphQlNonNullTypeReference required => NonNull(required.NullableType, schema, input, selectedType),
            GraphQlListTypeReference list => "ReadonlyArray<" + Render(list.ElementType, schema, input, selectedType) + ">",
            GraphQlNamedTypeReference named => selectedType ?? Named(named.Name, schema, input),
            _ => throw new InvalidDataException("Unsupported GraphQL type reference.")
        };

    private string Named(string name, GraphQlSchemaModel schema, bool input)
    {
        if (scalars is not null && scalars.TryGetValue(name, out var mapped)) return input ? mapped.Input : mapped.Output;
        return name switch
        {
            "ID" or "String" => "string",
            "Int" or "Float" => "number",
            "Boolean" => "boolean",
            _ when schema.ScalarTypes.Any(type => type.Name == name) => "unknown",
            _ => TypeName(name)
        };
    }
}
