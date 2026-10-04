using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.Targets;

/// <summary>Provides target-language naming, type rendering, and source conventions.</summary>
public interface ICodeGenerationTarget
{
    /// <summary>Stable identifier for this output target (for example, <c>csharp</c>).</summary>
    string Id { get; }

    /// <summary>Default namespace or module name used by generated source files.</summary>
    string DefaultNamespace { get; }

    /// <summary>Maps a GraphQL type name to a target-language declaration name.</summary>
    string TypeName(string graphQlName);

    /// <summary>Maps a GraphQL interface name to a target-language interface declaration name.</summary>
    string InterfaceName(string graphQlName);

    /// <summary>Maps GraphQL member names to target-language member identifiers.</summary>
    string PropertyName(string graphQlName);

    /// <summary>Maps GraphQL enum values to target-language enum member identifiers.</summary>
    string EnumValueName(string graphQlName);

    /// <summary>Maps GraphQL operation names to target-language method identifiers.</summary>
    string MethodName(string graphQlName);

    /// <summary>Maps GraphQL variable names to target-language parameter identifiers.</summary>
    string ParameterName(string graphQlName);

    /// <summary>Encodes a literal string in the target programming language.</summary>
    string StringLiteral(string value);

    /// <summary>Renders a structured GraphQL type reference in this target language.</summary>
    string RenderType(GraphQlTypeReference type, GraphQlSchemaModel schema);

    /// <summary>Returns whether the named GraphQL value maps to a target-language value type.</summary>
    bool IsValueType(GraphQlTypeReference type, GraphQlSchemaModel schema);

    /// <summary>Returns target-language imports required by the given schema.</summary>
    IReadOnlyList<string> GetImports(GraphQlSchemaModel schema);
}
