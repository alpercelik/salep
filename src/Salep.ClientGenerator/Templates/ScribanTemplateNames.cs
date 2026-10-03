namespace Salep.ClientGenerator.Templates;

/// <summary>Stable keys used to identify built-in templates in consumer overrides.</summary>
public static class ScribanTemplateNames
{
    public const string Schema = "schema";
    public const string Operations = "operations";
    public const string UnionConverters = "union-converters";
    public const string SharedTypes = "shared-types";
    public const string Client = "client";
    public const string OperationSample = "operation-sample";
    public const string ClientAgentInstructions = "client-agent-instructions";
    public const string TestHttpHandler = "test-http-handler";
    public const string TransportTests = "transport-tests";
    public const string OperationMetadataTests = "operation-metadata-tests";
    public const string OperationResponseTests = "operation-response-tests";
    public const string OperationsSampleTests = "operations-sample-tests";
    public const string UnionConverterTests = "union-converter-tests";
    public const string TestAgentInstructions = "test-agent-instructions";

    public const string ClientAnnotations = "client.annotations";
    public const string ClientMembers = "client.members";
    public const string ClientConstructorBody = "client.constructor-body";
    public const string ClientBeforeSend = "client.before-send";
    public const string ClientAfterResponse = "client.after-response";
    public const string ClientFields = "client.fields";
    public const string ClientConstructor = "client.constructor";
    public const string ClientExecute = "client.execute";
    public const string ClientBatch = "client.batch";
    public const string ClientIncremental = "client.incremental";
    public const string ClientPost = "client.post";
    public const string ClientReadResponse = "client.read-response";
    public const string ClientGetHelpers = "client.get-helpers";
    public const string ClientMultipartHelpers = "client.multipart-helpers";
    public const string SchemaObject = "schema.object";
    public const string SchemaInput = "schema.input";
    public const string SchemaInterface = "schema.interface";
    public const string SchemaEnum = "schema.enum";
    public const string SchemaUnion = "schema.union";
    public const string SchemaObjectProperty = "schema.object-property";
    public const string SchemaObjectAnnotations = "schema.object-annotations";
    public const string SchemaObjectMembers = "schema.object-members";
    public const string SchemaInputProperty = "schema.input-property";
    public const string SchemaInputAnnotations = "schema.input-annotations";
    public const string SchemaInputMembers = "schema.input-members";
    public const string SchemaInterfaceAnnotations = "schema.interface-annotations";
    public const string SchemaInterfaceMembers = "schema.interface-members";
    public const string SchemaEnumAnnotations = "schema.enum-annotations";
    public const string OperationsContract = "operations.contract";
    public const string OperationsVariables = "operations.variables";
    public const string OperationsResponseConverter = "operations.response-converter";
    public const string OperationsResponseObject = "operations.response-object";
    public const string OperationsFacade = "operations.facade";
    public const string OperationsContractAnnotations = "operations.contract-annotations";
    public const string OperationsContractMembers = "operations.contract-members";
    public const string OperationsVariablesAnnotations = "operations.variables-annotations";
    public const string OperationsVariablesMembers = "operations.variables-members";
    public const string OperationsVariablesProperty = "operations.variables-property";
    public const string OperationsResponseObjectAnnotations = "operations.response-object-annotations";
    public const string OperationsResponseObjectMembers = "operations.response-object-members";
    public const string OperationsResponseObjectProperty = "operations.response-object-property";
    public const string SharedOperationInterface = "shared.operation-interface";
    public const string SharedRequest = "shared.request";
    public const string SharedResponse = "shared.response";
    public const string SharedErrors = "shared.errors";

    public const string ConvertersRegistry = "converters.registry";
    public const string ConvertersDeclaration = "converters.declaration";
    public const string ConvertersRead = "converters.read";
    public const string ConvertersWrite = "converters.write";
    public const string TestsOperationResponseCase = "tests.operation-response-case";
    public const string TestsOperationMetadataCase = "tests.operation-metadata-case";
    public const string TestsTransportPost = "tests.transport-post";
    public const string TestsTransportNullVariables = "tests.transport-null-variables";
    public const string TestsTransportGet = "tests.transport-get";
    public const string TestsTransportBatch = "tests.transport-batch";
    public const string TestsTransportIncremental = "tests.transport-incremental";
    public const string TestsTransportErrors = "tests.transport-errors";
    public const string TestsTransportErrorDetails = "tests.transport-error-details";
    public const string TestsTransportNullResponses = "tests.transport-null-responses";

    internal static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Schema,
        Operations,
        UnionConverters,
        SharedTypes,
        Client,
        OperationSample,
        ClientAgentInstructions,
        TestHttpHandler,
        TransportTests,
        OperationMetadataTests,
        OperationResponseTests,
        OperationsSampleTests,
        UnionConverterTests,
        TestAgentInstructions,
        ClientAnnotations,
        ClientMembers,
        ClientConstructorBody,
        ClientBeforeSend,
        ClientAfterResponse,
        ClientFields,
        ClientConstructor,
        ClientExecute,
        ClientBatch,
        ClientIncremental,
        ClientPost,
        ClientReadResponse,
        ClientGetHelpers,
        ClientMultipartHelpers,
        SchemaObject,
        SchemaInput,
        SchemaInterface,
        SchemaEnum,
        SchemaUnion,
        SchemaObjectProperty,
        SchemaObjectAnnotations,
        SchemaObjectMembers,
        SchemaInputProperty,
        SchemaInputAnnotations,
        SchemaInputMembers,
        SchemaInterfaceAnnotations,
        SchemaInterfaceMembers,
        SchemaEnumAnnotations,
        OperationsContract,
        OperationsVariables,
        OperationsResponseConverter,
        OperationsResponseObject,
        OperationsFacade,
        OperationsContractAnnotations,
        OperationsContractMembers,
        OperationsVariablesAnnotations,
        OperationsVariablesMembers,
        OperationsVariablesProperty,
        OperationsResponseObjectAnnotations,
        OperationsResponseObjectMembers,
        OperationsResponseObjectProperty,
        SharedOperationInterface,
        SharedRequest,
        SharedResponse,
        SharedErrors,
        ConvertersRegistry,
        ConvertersDeclaration,
        ConvertersRead,
        ConvertersWrite,
        TestsOperationResponseCase,
        TestsOperationMetadataCase,
        TestsTransportPost,
        TestsTransportNullVariables,
        TestsTransportGet,
        TestsTransportBatch,
        TestsTransportIncremental,
        TestsTransportErrors,
        TestsTransportErrorDetails,
        TestsTransportNullResponses
    };
}
