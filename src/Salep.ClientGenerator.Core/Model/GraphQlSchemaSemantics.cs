namespace Salep.ClientGenerator.Model;

/// <summary>Shared type relationships over definitions and extensions.</summary>
public sealed class GraphQlSchemaSemantics(GraphQlSchemaModel schema)
{
    public GraphQlSchemaType? Type(string name) => schema.Types.FirstOrDefault(type => type.Name == name && !type.IsExtension) ?? GraphQlIntrospection.Types.FirstOrDefault(type => type.Name == name && name.StartsWith("__", StringComparison.Ordinal));
    public static string NamedType(GraphQlTypeReference type) => type switch
    {
        GraphQlNamedTypeReference named => named.Name,
        GraphQlListTypeReference list => NamedType(list.ElementType),
        GraphQlNonNullTypeReference required => NamedType(required.NullableType),
        _ => throw new InvalidDataException("Unknown type wrapper.")
    };
    public IEnumerable<GraphQlFieldDefinition> Fields(string name) => schema.Types.Concat(name.StartsWith("__", StringComparison.Ordinal) ? GraphQlIntrospection.Types : []).Where(type => type.Name == name).SelectMany(type => type switch
    {
        GraphQlObjectType obj => obj.Fields,
        GraphQlInterfaceType face => face.Fields,
        _ => Array.Empty<GraphQlFieldDefinition>()
    }).Concat(name == Root("query") ? GraphQlIntrospection.QueryFields : []);
    public bool IsOneOf(string name) => schema.Types.OfType<GraphQlInputObjectType>().Where(type => type.Name == name).Any(type => type.Directives.Any(directive => directive.Name == "oneOf"));
    public IEnumerable<GraphQlInputValueDefinition> Inputs(string name) => schema.Types.OfType<GraphQlInputObjectType>().Where(type => type.Name == name).SelectMany(type => type.Fields);
    public IEnumerable<string> Interfaces(string name) => schema.Types.Where(type => type.Name == name).SelectMany(type => type switch
    {
        GraphQlObjectType obj => obj.Interfaces,
        GraphQlInterfaceType face => face.Interfaces,
        _ => Array.Empty<string>()
    });
    public bool Implements(string name, string face, HashSet<string>? path = null)
    {
        path ??= new(StringComparer.Ordinal);
        if (!path.Add(name)) return false;
        try { return Interfaces(name).Any(item => item == face || Implements(item, face, path)); }
        finally { path.Remove(name); }
    }
    public IReadOnlyList<string> PossibleTypes(string name) => Type(name) switch
    {
        GraphQlObjectType => [name],
        GraphQlInterfaceType => schema.ObjectTypes.Where(obj => Implements(obj.Name, name)).Select(obj => obj.Name).ToArray(),
        GraphQlUnionType => schema.Types.OfType<GraphQlUnionType>().Where(type => type.Name == name).SelectMany(type => type.Members).Distinct(StringComparer.Ordinal).ToArray(),
        _ => []
    };
    public bool Applies(string? condition, string concrete) => condition is null || PossibleTypes(condition).Contains(concrete, StringComparer.Ordinal);
    public string Root(string operation)
    {
        var configured = schema.SchemaDefinitions.SelectMany(definition => definition.RootOperations).FirstOrDefault(root => root.Operation == operation)?.TypeName;
        if (configured is not null) return configured;
        if (schema.SchemaDefinitions.Any(definition => !definition.IsExtension)) return string.Empty;
        return operation switch { "query" => "Query", "mutation" => "Mutation", "subscription" => "Subscription", _ => throw new InvalidDataException("Unknown operation kind.") };
    }
}

/// <summary>Conservative presence evaluation shared by response projection and template selection.</summary>
public static class GraphQlSelectionConditions
{
    public static bool Excluded(IReadOnlyList<GraphQlDirective> directives) => directives.Any(directive =>
        directive.Name is "include" or "skip" && directive.Arguments.FirstOrDefault(argument => argument.Name == "if")?.Value is { Kind: GraphQlValueKind.BooleanLiteral } value
        && (directive.Name == "include" ? value.Boolean == false : value.Boolean == true));
    public static bool Conditional(IReadOnlyList<GraphQlDirective> directives) => directives.Any(directive =>
        directive.Name is "include" or "skip" && directive.Arguments.FirstOrDefault(argument => argument.Name == "if")?.Value.Kind == GraphQlValueKind.Variable);
}
