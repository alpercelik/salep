namespace Salep.ClientGenerator.Targets;

/// <summary>Configures generated C# HTTP client runtime code.</summary>
public sealed record CSharpClientGenerationOptions
{
    /// <summary>Generated client class name.</summary>
    public string ClientClassName { get; init; } = "GraphQLClient";

    /// <summary>Generated operation contract interface name.</summary>
    public string OperationInterfaceTypeName { get; init; } = "IGraphQLOperation";

    /// <summary>Enables HTTP GET for query operations when the URL fits the configured bound.</summary>
    public bool UseHttpGet { get; init; }

    /// <summary>Enables batched POST requests.</summary>
    public bool EnableBatching { get; init; }

    /// <summary>Maximum absolute URI length accepted for GET requests.</summary>
    public int MaxGetUrlLength { get; init; } = 2048;

    /// <summary>Registers generated union converters in the serializer options.</summary>
    public bool RegisterUnionConverters { get; init; } = true;

    /// <summary>Names of converter registries called by the generated client.</summary>
    public IReadOnlyList<string> ConverterRegistries { get; init; } = ["UnionJsonConverters"];

    /// <summary>C# statements that configure custom scalar and JSON converter dependencies.</summary>
    public IReadOnlyList<string> SerializerConfigurationStatements { get; init; } = [];

    /// <summary>Includes the executable operation sample in the generated source bundle.</summary>
    public bool EmitOperationSample { get; init; }

    /// <summary>Includes client agent guidance in the generated source bundle.</summary>
    public bool EmitAgentInstructions { get; init; }
}
