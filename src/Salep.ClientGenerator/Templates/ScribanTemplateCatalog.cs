using System.Reflection;
using Salep.ClientGenerator.Generation;

namespace Salep.ClientGenerator.Templates;

/// <summary>Provides the templates embedded in the Scriban generator package.</summary>
public static class ScribanTemplateCatalog
{
    private static readonly IReadOnlyDictionary<string, string> ResourceNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ScribanTemplateNames.Schema] = "CSharpSchema.scriban-cs",
        [ScribanTemplateNames.Operations] = "CSharpOperations.scriban-cs",
        [ScribanTemplateNames.UnionConverters] = "CSharpUnionConverters.scriban-cs",
        [ScribanTemplateNames.SharedTypes] = "CSharpSharedTypes.scriban-cs",
        [ScribanTemplateNames.Client] = "CSharpClient.scriban-cs",
        [ScribanTemplateNames.OperationSample] = "CSharpOperationsSample.scriban-cs",
        [ScribanTemplateNames.ClientAgentInstructions] = "CSharpClientAgentInstructions.scriban",
        [ScribanTemplateNames.TestHttpHandler] = "CSharpTestHttpHandler.scriban-cs",
        [ScribanTemplateNames.TransportTests] = "CSharpTransportTests.scriban-cs",
        [ScribanTemplateNames.OperationMetadataTests] = "CSharpOperationMetadataTests.scriban-cs",
        [ScribanTemplateNames.OperationResponseTests] = "CSharpOperationResponseTests.scriban-cs",
        [ScribanTemplateNames.OperationsSampleTests] = "CSharpOperationsSampleTests.scriban-cs",
        [ScribanTemplateNames.UnionConverterTests] = "CSharpUnionConverterTests.scriban-cs",
        [ScribanTemplateNames.TestAgentInstructions] = "CSharpTestsAgentInstructions.scriban",
        [ScribanTemplateNames.ClientAnnotations] = "CSharpClientAnnotations.scriban-cs",
        [ScribanTemplateNames.ClientMembers] = "CSharpClientMembers.scriban-cs",
        [ScribanTemplateNames.ClientConstructorBody] = "CSharpClientConstructorBody.scriban-cs",
        [ScribanTemplateNames.ClientBeforeSend] = "CSharpClientBeforeSend.scriban-cs",
        [ScribanTemplateNames.ClientAfterResponse] = "CSharpClientAfterResponse.scriban-cs",
        [ScribanTemplateNames.ClientFields] = "CSharpClientFields.scriban-cs",
        [ScribanTemplateNames.ClientConstructor] = "CSharpClientConstructor.scriban-cs",
        [ScribanTemplateNames.ClientExecute] = "CSharpClientExecute.scriban-cs",
        [ScribanTemplateNames.ClientBatch] = "CSharpClientBatch.scriban-cs",
        [ScribanTemplateNames.ClientIncremental] = "CSharpClientIncremental.scriban-cs",
        [ScribanTemplateNames.ClientPost] = "CSharpClientPost.scriban-cs",
        [ScribanTemplateNames.ClientReadResponse] = "CSharpClientReadResponse.scriban-cs",
        [ScribanTemplateNames.ClientGetHelpers] = "CSharpClientGetHelpers.scriban-cs",
        [ScribanTemplateNames.ClientMultipartHelpers] = "CSharpClientMultipartHelpers.scriban-cs",
        [ScribanTemplateNames.SchemaObject] = "CSharpSchemaObject.scriban-cs",
        [ScribanTemplateNames.SchemaInput] = "CSharpSchemaInput.scriban-cs",
        [ScribanTemplateNames.SchemaInterface] = "CSharpSchemaInterface.scriban-cs",
        [ScribanTemplateNames.SchemaEnum] = "CSharpSchemaEnum.scriban-cs",
        [ScribanTemplateNames.SchemaUnion] = "CSharpSchemaUnion.scriban-cs",
        [ScribanTemplateNames.SchemaObjectProperty] = "CSharpSchemaObjectProperty.scriban-cs",
        [ScribanTemplateNames.SchemaObjectAnnotations] = "CSharpSchemaObjectAnnotations.scriban-cs",
        [ScribanTemplateNames.SchemaObjectMembers] = "CSharpSchemaObjectMembers.scriban-cs",
        [ScribanTemplateNames.SchemaInputProperty] = "CSharpSchemaInputProperty.scriban-cs",
        [ScribanTemplateNames.SchemaInputAnnotations] = "CSharpSchemaInputAnnotations.scriban-cs",
        [ScribanTemplateNames.SchemaInputMembers] = "CSharpSchemaInputMembers.scriban-cs",
        [ScribanTemplateNames.SchemaInterfaceAnnotations] = "CSharpSchemaInterfaceAnnotations.scriban-cs",
        [ScribanTemplateNames.SchemaInterfaceMembers] = "CSharpSchemaInterfaceMembers.scriban-cs",
        [ScribanTemplateNames.SchemaEnumAnnotations] = "CSharpSchemaEnumAnnotations.scriban-cs",
        [ScribanTemplateNames.OperationsContract] = "CSharpOperationsContract.scriban-cs",
        [ScribanTemplateNames.OperationsVariables] = "CSharpOperationsVariables.scriban-cs",
        [ScribanTemplateNames.OperationsResponseConverter] = "CSharpOperationsResponseConverter.scriban-cs",
        [ScribanTemplateNames.OperationsResponseObject] = "CSharpOperationsResponseObject.scriban-cs",
        [ScribanTemplateNames.OperationsFacade] = "CSharpOperationsFacade.scriban-cs",
        [ScribanTemplateNames.OperationsContractAnnotations] = "CSharpOperationsContractAnnotations.scriban-cs",
        [ScribanTemplateNames.OperationsContractMembers] = "CSharpOperationsContractMembers.scriban-cs",
        [ScribanTemplateNames.OperationsVariablesAnnotations] = "CSharpOperationsVariablesAnnotations.scriban-cs",
        [ScribanTemplateNames.OperationsVariablesMembers] = "CSharpOperationsVariablesMembers.scriban-cs",
        [ScribanTemplateNames.OperationsVariablesProperty] = "CSharpOperationsVariablesProperty.scriban-cs",
        [ScribanTemplateNames.OperationsResponseObjectAnnotations] = "CSharpOperationsResponseObjectAnnotations.scriban-cs",
        [ScribanTemplateNames.OperationsResponseObjectMembers] = "CSharpOperationsResponseObjectMembers.scriban-cs",
        [ScribanTemplateNames.OperationsResponseObjectProperty] = "CSharpOperationsResponseObjectProperty.scriban-cs",
        [ScribanTemplateNames.SharedOperationInterface] = "CSharpSharedOperationInterface.scriban-cs",
        [ScribanTemplateNames.SharedRequest] = "CSharpSharedRequest.scriban-cs",
        [ScribanTemplateNames.SharedResponse] = "CSharpSharedResponse.scriban-cs",
        [ScribanTemplateNames.SharedErrors] = "CSharpSharedErrors.scriban-cs",
        [ScribanTemplateNames.ConvertersRegistry] = "CSharpConvertersRegistry.scriban-cs",
        [ScribanTemplateNames.ConvertersDeclaration] = "CSharpConvertersDeclaration.scriban-cs",
        [ScribanTemplateNames.ConvertersRead] = "CSharpConvertersRead.scriban-cs",
        [ScribanTemplateNames.ConvertersWrite] = "CSharpConvertersWrite.scriban-cs",
        [ScribanTemplateNames.TestsOperationResponseCase] = "CSharpTestsOperationResponseCase.scriban-cs",
        [ScribanTemplateNames.TestsOperationMetadataCase] = "CSharpTestsOperationMetadataCase.scriban-cs",
        [ScribanTemplateNames.TestsTransportPost] = "CSharpTestsTransportPost.scriban-cs",
        [ScribanTemplateNames.TestsTransportNullVariables] = "CSharpTestsTransportNullVariables.scriban-cs",
        [ScribanTemplateNames.TestsTransportGet] = "CSharpTestsTransportGet.scriban-cs",
        [ScribanTemplateNames.TestsTransportBatch] = "CSharpTestsTransportBatch.scriban-cs",
        [ScribanTemplateNames.TestsTransportIncremental] = "CSharpTestsTransportIncremental.scriban-cs",
        [ScribanTemplateNames.TestsTransportErrors] = "CSharpTestsTransportErrors.scriban-cs",
        [ScribanTemplateNames.TestsTransportErrorDetails] = "CSharpTestsTransportErrorDetails.scriban-cs",
        [ScribanTemplateNames.TestsTransportNullResponses] = "CSharpTestsTransportNullResponses.scriban-cs"
    };

    /// <summary>Returns template keys and their default filenames.</summary>
    public static IReadOnlyDictionary<string, string> GetDefaultTemplateFiles()
        => new Dictionary<string, string>(ResourceNames, StringComparer.Ordinal);

    /// <summary>Reads one embedded default template by its stable key.</summary>
    public static string ReadDefault(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!ResourceNames.TryGetValue(name, out var fileName))
            throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown Scriban template name.");

        var resourceName = "Salep.ClientGenerator.Templates." + fileName;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded Scriban template '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Writes all embedded defaults to a directory for consumer customization.</summary>
    public static IReadOnlyList<string> WriteDefaults(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var outputDirectory = Path.GetFullPath(directory);
        var paths = new GeneratorPathPolicy(outputDirectory);
        foreach (var file in ResourceNames.Values) paths.Write(Path.Combine(outputDirectory, file));
        Directory.CreateDirectory(outputDirectory);
        var written = new List<string>(ResourceNames.Count);
        foreach (var template in ResourceNames)
        {
            var path = Path.Combine(outputDirectory, template.Value);
            File.WriteAllText(path, ReadDefault(template.Key));
            written.Add(GeneratorPathPolicy.Normalize(path));
        }

        return written;
    }
}
