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
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for", "function", "if", "import", "in", "instanceof", "new", "null", "return", "super", "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while", "with", "implements", "interface", "let", "package", "private", "protected", "public", "static", "yield", "await",
        "any", "boolean", "never", "number", "object", "string", "symbol", "undefined", "unknown", "bigint", "intrinsic", "type"
    };
    private static readonly string[] RuntimeSymbols =
    [
        "Readonly", "ReadonlyArray", "Record", "Promise", "Scalars", "TypedDocument", "TypedDocumentNode", "GraphQLClient", "GraphQLRequestError", "GraphQLError", "RequestOptions", "FragmentType", "FragmentReference", "getFragmentData", "graphql",
        "ResolverParentTypes", "LocalResolvers", "LocalSelection", "LocalValue", "LocalType", "LocalInputDefinitions", "ResolverFunction", "ResolverMap", "resolveLocalSelections", "localValue", "coerceLocalInput"
    ];
    private static string PreferredName(string graphQlName) => Keywords.Contains(graphQlName) ? "Gql" + graphQlName : graphQlName;
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
        var occupied = new HashSet<string>(RuntimeSymbols, StringComparer.Ordinal);
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
