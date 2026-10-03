using Scriban.Runtime;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Templates;

namespace Salep.ClientGenerator.Targets;

/// <summary>Generates C# source from shared GraphQL models and customizable Scriban templates.</summary>
public sealed class ScribanCSharpTemplateGenerator(ScribanTemplateEngine? engine = null)
{
    private const string SchemaTemplateResource = "Salep.ClientGenerator.Templates.CSharpSchema.scriban-cs";
    private const string OperationsTemplateResource = "Salep.ClientGenerator.Templates.CSharpOperations.scriban-cs";
    private const string UnionConvertersTemplateResource = "Salep.ClientGenerator.Templates.CSharpUnionConverters.scriban-cs";
    private const string SharedTypesTemplateResource = "Salep.ClientGenerator.Templates.CSharpSharedTypes.scriban-cs";
    private const string ClientTemplateResource = "Salep.ClientGenerator.Templates.CSharpClient.scriban-cs";
    private const string OperationsSampleTemplateResource = "Salep.ClientGenerator.Templates.CSharpOperationsSample.scriban-cs";
    private const string TestHttpHandlerTemplateResource = "Salep.ClientGenerator.Templates.CSharpTestHttpHandler.scriban-cs";
    private const string OperationMetadataTestsTemplateResource = "Salep.ClientGenerator.Templates.CSharpOperationMetadataTests.scriban-cs";
    private const string OperationResponseTestsTemplateResource = "Salep.ClientGenerator.Templates.CSharpOperationResponseTests.scriban-cs";
    private const string TransportTestsTemplateResource = "Salep.ClientGenerator.Templates.CSharpTransportTests.scriban-cs";
    private const string SampleTestsTemplateResource = "Salep.ClientGenerator.Templates.CSharpOperationsSampleTests.scriban-cs";
    private const string UnionTestsTemplateResource = "Salep.ClientGenerator.Templates.CSharpUnionConverterTests.scriban-cs";
    private const string ClientAgentInstructionsTemplateResource = "Salep.ClientGenerator.Templates.CSharpClientAgentInstructions.scriban";
    private const string TestsAgentInstructionsTemplateResource = "Salep.ClientGenerator.Templates.CSharpTestsAgentInstructions.scriban";
    private readonly ScribanTemplateEngine templateEngine = engine ?? new ScribanTemplateEngine();

    /// <summary>Renders a complete minimal set of C# client source files from schema and operation models.</summary>
    public IReadOnlyList<CSharpGeneratedFile> GenerateClientSources(
        GraphQlSchemaModel schema,
        GraphQlExecutableDocument executable,
        CSharpCodeGenerationTarget target,
        CSharpClientGenerationOptions? clientOptions = null,
        GraphQlOperationDocumentOptions? documentOptions = null,
        bool includeSharedTypes = true,
        IReadOnlySet<string>? localTypeNames = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(target);
        clientOptions ??= new CSharpClientGenerationOptions();
        var interfaceName = clientOptions.OperationInterfaceTypeName;
        var files = new List<CSharpGeneratedFile>();
        if (localTypeNames is null ? schema.Types.Count > 0 : localTypeNames.Count > 0)
        {
            files.Add(new("SchemaTypes.cs", GenerateSchema(schema, target,
                template: GetOverride(templateOverrides, ScribanTemplateNames.Schema), includedTypeNames: localTypeNames, templateOverrides: templateOverrides)));
        }

        files.AddRange(
        [
            new("Operations.cs", GenerateOperations(schema, executable, target, documentOptions,
                template: GetOverride(templateOverrides, ScribanTemplateNames.Operations),
                operationInterfaceTypeName: interfaceName, clientClassName: clientOptions.ClientClassName, templateOverrides: templateOverrides)),
            new("GraphQLClient.cs", GenerateClient(target, clientOptions,
                GetOverride(templateOverrides, ScribanTemplateNames.Client), templateOverrides)),
            new("UnionJsonConverters.cs", GenerateUnionConverters(schema, target,
                template: GetOverride(templateOverrides, ScribanTemplateNames.UnionConverters), includedTypeNames: localTypeNames, templateOverrides: templateOverrides))
        ]);
        if (includeSharedTypes)
        {
            files.Add(new("GraphQLSharedTypes.cs", GenerateSharedTypes(target, operationInterfaceTypeName: interfaceName,
                template: GetOverride(templateOverrides, ScribanTemplateNames.SharedTypes), templateOverrides: templateOverrides)));
        }

        if (clientOptions.EmitOperationSample)
        {
            files.Add(new("Operations.Sample.cs", GenerateOperationSample(schema, executable, target, clientOptions, documentOptions,
                GetOverride(templateOverrides, ScribanTemplateNames.OperationSample), templateOverrides)));
        }

        if (clientOptions.EmitAgentInstructions)
        {
            files.Add(new("agents.md", GenerateClientAgentInstructions(schema, target, clientOptions,
                GetOverride(templateOverrides, ScribanTemplateNames.ClientAgentInstructions), templateOverrides)));
        }

        return files;
    }

    /// <summary>Renders schema declarations; a supplied template replaces the embedded default.</summary>
    public string GenerateSchema(
        GraphQlSchemaModel schema,
        CSharpCodeGenerationTarget target,
        string? template = null,
        IReadOnlySet<string>? includedTypeNames = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(target);
        template ??= GetOverride(templateOverrides, ScribanTemplateNames.Schema);
        var source = template ?? ReadTemplate(SchemaTemplateResource);
        var model = CSharpSchemaTemplateModelFactory.Create(schema, target, includedTypeNames: includedTypeNames);
        return RenderCSharp(source, model, template is null ? "CSharpSchema.scriban-cs" : "custom-schema.scriban", template is not null, templateOverrides);
    }

    /// <summary>Renders operation contracts, variables, response projections, and complete GraphQL documents.</summary>
    public string GenerateOperations(
        GraphQlSchemaModel schema,
        GraphQlExecutableDocument executable,
        CSharpCodeGenerationTarget target,
        GraphQlOperationDocumentOptions? documentOptions = null,
        string? template = null,
        string operationInterfaceTypeName = "IGraphQLOperation",
        string clientClassName = "GraphQLClient",
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(target);
        template ??= GetOverride(templateOverrides, ScribanTemplateNames.Operations);
        var source = template ?? ReadTemplate(OperationsTemplateResource);
        var model = CSharpOperationTemplateModelFactory.Create(schema, executable, target, documentOptions, operationInterfaceTypeName, clientClassName);
        return RenderCSharp(source, model, template is null ? "CSharpOperations.scriban-cs" : "custom-operations.scriban", template is not null, templateOverrides);
    }

    /// <summary>Renders an optional executable sample that constructs schema-aware operation variables.</summary>
    public string GenerateOperationSample(
        GraphQlSchemaModel schema,
        GraphQlExecutableDocument executable,
        CSharpCodeGenerationTarget target,
        CSharpClientGenerationOptions? clientOptions = null,
        GraphQlOperationDocumentOptions? documentOptions = null,
        string? template = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(target);
        clientOptions ??= new CSharpClientGenerationOptions();
        var operationsBaseName = clientOptions.ClientClassName.EndsWith("Client", StringComparison.Ordinal)
            ? clientOptions.ClientClassName[..^"Client".Length]
            : clientOptions.ClientClassName;
        var formatter = new GraphQlOperationDocumentFormatter(schema, executable);
        var samples = new CSharpSampleValueFactory(schema, target);
        var operationViews = new List<ScriptObject>();
        var operationIndex = 0;
        foreach (var operation in executable.Operations.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            operationIndex++;
            var normalized = formatter.NormalizeVariables(operation, documentOptions);
            var variables = new ScriptArray(normalized.Variables.Select(variable =>
            {
                var view = new ScriptObject();
                view.Add("property_name", target.PropertyName(variable.Name));
                view.Add("sample_expression", samples.Create(variable.Type));
                return view;
            }));
            var view = new ScriptObject();
            view.Add("variables_type", target.TypeName(operation.Name!) + "Variables");
            view.Add("method_name", target.TypeName(operation.Name!) + "Async");
            view.Add("local_name", "sampleOperation" + operationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            view.Add("has_variables", variables.Count > 0);
            view.Add("variables", variables);
            operationViews.Add(view);
        }

        var settings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["client_name"] = clientOptions.ClientClassName,
            ["operations_class"] = operationsBaseName + "Operations"
        };
        var model = ScribanTemplateModelFactory.Create(schema, target, settings: settings);
        model.Add("csharp_operations", new ScriptArray(operationViews));
        template ??= GetOverride(templateOverrides, ScribanTemplateNames.OperationSample);
        var source = template ?? ReadTemplate(OperationsSampleTemplateResource);
        return RenderCSharp(source, model, template is null ? "CSharpOperationsSample.scriban-cs" : "custom-operation-sample.scriban", template is not null, templateOverrides);
    }

    /// <summary>Renders selected test suites and optional agent guidance as named source files.</summary>
    public IReadOnlyList<CSharpGeneratedFile> GenerateTestSources(
        GraphQlSchemaModel schema,
        GraphQlExecutableDocument executable,
        CSharpCodeGenerationTarget target,
        CSharpClientGenerationOptions? clientOptions = null,
        CSharpTestGenerationOptions? testOptions = null,
        GraphQlOperationDocumentOptions? documentOptions = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(target);
        clientOptions ??= new CSharpClientGenerationOptions();
        testOptions ??= new CSharpTestGenerationOptions();
        ValidateTestOptions(testOptions);
        var testNamespace = testOptions.Namespace ?? target.DefaultNamespace + ".Tests";
        if (clientOptions.MaxGetUrlLength < 1) throw new ArgumentOutOfRangeException(nameof(clientOptions), "Maximum GET URL length must be positive.");
        var settings = TestSettings(testOptions, testNamespace, clientOptions, target);
        settings["native_unions"] = target.UseNativeUnions;
        var jsonLiterals = (ScriptObject)settings["json_literals"]!;
        foreach (var type in schema.Types.Where(type => type is GraphQlUnionType or GraphQlInterfaceType))
        {
            var members = type switch
            {
                GraphQlUnionType union => union.Members,
                GraphQlInterfaceType contract => schema.ObjectTypes.Where(item => ImplementsInterface(schema, item.Name, contract.Name)).Select(item => item.Name).ToArray(),
                _ => []
            };
            foreach (var member in members.Distinct(StringComparer.Ordinal))
            {
                var json = "{\"__typename\":" + System.Text.Json.JsonSerializer.Serialize(member) + "}";
                if (!jsonLiterals.ContainsKey(member))
                {
                    jsonLiterals.Add(member, testOptions.UseRawJsonLiterals ? RawStringLiteral(json) : target.StringLiteral(json));
                }
            }
        }
        var model = CSharpOperationTemplateModelFactory.Create(schema, executable, target, documentOptions,
            clientOptions.OperationInterfaceTypeName, clientOptions.ClientClassName);
        var responseSamples = new CSharpResponseSampleJsonFactory(schema, target);
        foreach (var operation in (ScriptArray)model["csharp_operations"]!)
        {
            var operationModel = (ScriptObject)operation!;
            var operationName = operationModel.GetSafeValue<string>("name")!;
            var definition = executable.Operations.First(item => item.Name == operationName);
            var json = "{\"data\":" + responseSamples.Create(definition) + "}";
            operationModel.Add("response_json_literal", testOptions.UseRawJsonLiterals ? RawStringLiteral(json) : target.StringLiteral(json));
        }
        model.Add("settings", ScribanTemplateModelFactory.ImportDictionary(settings));
        var outputs = new List<CSharpGeneratedFile>();
        var suites = testOptions.Suites;
        if (suites.Contains("transport") || suites.Contains("operations") || suites.Contains("samples"))
        {
            outputs.Add(new("TestHttpMessageHandler.cs", RenderCSharp(ResolveTemplate(ScribanTemplateNames.TestHttpHandler, TestHttpHandlerTemplateResource, templateOverrides), model, "CSharpTestHttpHandler.scriban-cs", templateOverrides?.ContainsKey(ScribanTemplateNames.TestHttpHandler) == true, templateOverrides)));
        }

        if (suites.Contains("transport"))
        {
            outputs.Add(new("GraphQLClientPayloadTests.cs", RenderCSharp(ResolveTemplate(ScribanTemplateNames.TransportTests, TransportTestsTemplateResource, templateOverrides), model, "CSharpTransportTests.scriban-cs", templateOverrides?.ContainsKey(ScribanTemplateNames.TransportTests) == true, templateOverrides)));
        }

        if (suites.Contains("operations"))
        {
            outputs.Add(new("OperationsMetadataTests.cs", RenderCSharp(ResolveTemplate(ScribanTemplateNames.OperationMetadataTests, OperationMetadataTestsTemplateResource, templateOverrides), model, "CSharpOperationMetadataTests.scriban-cs", templateOverrides?.ContainsKey(ScribanTemplateNames.OperationMetadataTests) == true, templateOverrides)));
            outputs.Add(new("OperationsResponseTests.cs", RenderCSharp(ResolveTemplate(ScribanTemplateNames.OperationResponseTests, OperationResponseTestsTemplateResource, templateOverrides), model, "CSharpOperationResponseTests.scriban-cs", templateOverrides?.ContainsKey(ScribanTemplateNames.OperationResponseTests) == true, templateOverrides)));
        }

        if (suites.Contains("unions"))
        {
            var unionModel = CSharpSchemaTemplateModelFactory.Create(schema, target, executable);
            unionModel.Add("settings", ScribanTemplateModelFactory.ImportDictionary(settings));
            outputs.Add(new("UnionConverterTests.cs", RenderCSharp(ResolveTemplate(ScribanTemplateNames.UnionConverterTests, UnionTestsTemplateResource, templateOverrides), unionModel, "CSharpUnionConverterTests.scriban-cs", templateOverrides?.ContainsKey(ScribanTemplateNames.UnionConverterTests) == true, templateOverrides)));
        }

        if (suites.Contains("samples"))
        {
            outputs.Add(new("OperationsSampleTests.cs", RenderCSharp(ResolveTemplate(ScribanTemplateNames.OperationsSampleTests, SampleTestsTemplateResource, templateOverrides), model, "CSharpOperationsSampleTests.scriban-cs", templateOverrides?.ContainsKey(ScribanTemplateNames.OperationsSampleTests) == true, templateOverrides)));
        }

        if (testOptions.EmitAgentInstructions)
        {
            var guidance = new ScriptObject();
            guidance.Add("settings", ScribanTemplateModelFactory.ImportDictionary(settings));
            var schemaModel = CSharpSchemaTemplateModelFactory.Create(schema, target);
            guidance.Add("target", schemaModel["target"]);
            guidance.Add("csharp", schemaModel["csharp"]);
            outputs.Add(new("agents.md", templateEngine.Render(ResolveTemplate(ScribanTemplateNames.TestAgentInstructions, TestsAgentInstructionsTemplateResource, templateOverrides), guidance, "CSharpTestsAgentInstructions.scriban", templateOverrides)));
        }

        return outputs;
    }

    /// <summary>Renders client agent guidance from the configured C# target and transport options.</summary>
    public string GenerateClientAgentInstructions(
        GraphQlSchemaModel schema,
        CSharpCodeGenerationTarget target,
        CSharpClientGenerationOptions? clientOptions = null,
        string? template = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(target);
        clientOptions ??= new CSharpClientGenerationOptions();
        var settings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["client_name"] = clientOptions.ClientClassName,
            ["use_http_get"] = clientOptions.UseHttpGet,
            ["enable_batching"] = clientOptions.EnableBatching
        };
        var model = CSharpSchemaTemplateModelFactory.Create(schema, target);
        model.Add("settings", ScribanTemplateModelFactory.ImportDictionary(settings));
        template ??= GetOverride(templateOverrides, ScribanTemplateNames.ClientAgentInstructions);
        var source = template ?? ReadTemplate(ClientAgentInstructionsTemplateResource);
        return templateEngine.Render(source, model, template is null ? "CSharpClientAgentInstructions.scriban" : "custom-client-agent-instructions.scriban", templateOverrides);
    }

    /// <summary>Renders converter registration and JSON converters for unions and interface result types.</summary>
    public string GenerateUnionConverters(
        GraphQlSchemaModel schema,
        CSharpCodeGenerationTarget target,
        string? template = null,
        IReadOnlySet<string>? includedTypeNames = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(target);
        template ??= GetOverride(templateOverrides, ScribanTemplateNames.UnionConverters);
        var source = template ?? ReadTemplate(UnionConvertersTemplateResource);
        var model = CSharpSchemaTemplateModelFactory.Create(schema, target, includedTypeNames: includedTypeNames);
        return RenderCSharp(source, model, template is null ? "CSharpUnionConverters.scriban-cs" : "custom-union-converters.scriban", template is not null, templateOverrides);
    }

    /// <summary>Renders the operation contract and core GraphQL transport data types.</summary>
    public string GenerateSharedTypes(
        CSharpCodeGenerationTarget target,
        bool includeOperationInterface = true,
        bool includeCoreTypes = true,
        string operationInterfaceTypeName = "IGraphQLOperation",
        string? template = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationInterfaceTypeName);
        var settings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["emit_operation_interface"] = includeOperationInterface,
            ["emit_core_types"] = includeCoreTypes,
            ["operation_interface"] = operationInterfaceTypeName
        };
        var model = ScribanTemplateModelFactory.Create(new GraphQlSchemaModel([], [], []), target, settings: settings);
        template ??= GetOverride(templateOverrides, ScribanTemplateNames.SharedTypes);
        var source = template ?? ReadTemplate(SharedTypesTemplateResource);
        return RenderCSharp(source, model, template is null ? "CSharpSharedTypes.scriban-cs" : "custom-shared-types.scriban", template is not null, templateOverrides);
    }

    /// <summary>Renders the C# HTTP transport client using explicit consumer-facing options.</summary>
    public string GenerateClient(
        CSharpCodeGenerationTarget target,
        CSharpClientGenerationOptions? options = null,
        string? template = null,
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        options ??= new CSharpClientGenerationOptions();
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientClassName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OperationInterfaceTypeName);
        ArgumentNullException.ThrowIfNull(options.ConverterRegistries);
        ArgumentNullException.ThrowIfNull(options.SerializerConfigurationStatements);
        if (options.MaxGetUrlLength < 1) throw new ArgumentOutOfRangeException(nameof(options), "Maximum GET URL length must be positive.");
        var settings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["client_name"] = options.ClientClassName,
            ["operation_interface"] = options.OperationInterfaceTypeName,
            ["use_http_get"] = options.UseHttpGet,
            ["enable_batching"] = options.EnableBatching,
            ["max_get_url_length"] = options.MaxGetUrlLength,
            ["register_union_converters"] = options.RegisterUnionConverters,
            ["converter_registries"] = options.ConverterRegistries,
            ["serializer_configuration_statements"] = options.SerializerConfigurationStatements
        };
        var model = ScribanTemplateModelFactory.Create(new GraphQlSchemaModel([], [], []), target, settings: settings);
        template ??= GetOverride(templateOverrides, ScribanTemplateNames.Client);
        var source = template ?? ReadTemplate(ClientTemplateResource);
        return RenderCSharp(source, model, template is null ? "CSharpClient.scriban-cs" : "custom-client.scriban", template is not null, templateOverrides);
    }

    private string RenderCSharp(string template, global::Scriban.Runtime.ScriptObject model, string sourceName, bool custom, IReadOnlyDictionary<string, string>? templateOverrides)
    {
        var rendered = templateEngine.Render(template, model, sourceName, templateOverrides);
        return (custom ? rendered : "// <auto-generated/>\n#nullable enable\n" + rendered.TrimEnd() + "\n").ReplaceLineEndings(Environment.NewLine);
    }

    private static string ReadTemplate(string resourceName)
    {
        var name = ScribanTemplateCatalog.GetDefaultTemplateFiles()
            .Single(template => resourceName.EndsWith("." + template.Value, StringComparison.Ordinal)).Key;
        return ScribanTemplateCatalog.ReadDefault(name);
    }

    private static string ResolveTemplate(string key, string resourceName, IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null) return ReadTemplate(resourceName);
        if (overrides.TryGetValue(key, out var value))
        {
            ArgumentNullException.ThrowIfNull(value);
            return value;
        }

        return ReadTemplate(resourceName);
    }

    private static string? GetOverride(IReadOnlyDictionary<string, string>? overrides, string key)
        => overrides is not null && overrides.TryGetValue(key, out var template) ? template : null;

    private static Dictionary<string, object?> TestSettings(CSharpTestGenerationOptions options, string testNamespace, CSharpClientGenerationOptions clientOptions, CSharpCodeGenerationTarget target)
    {
        var jsonLiterals = new ScriptObject(StringComparer.Ordinal);
        var getUri = new UriBuilder("https://example.test/graphql")
        {
            Query = "query=" + System.Net.WebUtility.UrlEncode("query TestOp { __typename }") + "&operationName=TestOp"
        }.Uri;
        string JsonLiteral(string value) => options.UseRawJsonLiterals ? RawStringLiteral(value) : target.StringLiteral(value);
        return new(StringComparer.Ordinal)
        {
            ["test_namespace"] = testNamespace,
            ["additional_imports"] = options.AdditionalImports,
            ["client_name"] = clientOptions.ClientClassName,
            ["operation_interface"] = clientOptions.OperationInterfaceTypeName,
            ["converter_registries"] = clientOptions.ConverterRegistries,
            ["suites"] = options.Suites.Order(StringComparer.Ordinal).ToArray(),
            ["suites_text"] = string.Join(", ", options.Suites.Order(StringComparer.Ordinal)),
            ["raw_json"] = options.UseRawJsonLiterals,
            ["sample_response_literal"] = JsonLiteral("{\"data\":null}"),
            ["transport_response_literal"] = JsonLiteral("{\"data\":{}}"),
            ["error_response_literal"] = JsonLiteral("{\"errors\":[{\"message\":\"fixture error\"}],\"data\":null}"),
            ["error_details_literal"] = JsonLiteral("{\"errors\":[{\"message\":\"bad\",\"locations\":[{\"line\":2,\"column\":4}],\"path\":[\"users\",0,\"name\"],\"extensions\":{\"code\":\"BAD\"}}]}"),
            ["null_list_item_literal"] = JsonLiteral("{\"data\":{\"posts\":[null]}}"),
            ["null_field_errors_literal"] = JsonLiteral("{\"data\":{\"users\":null},\"errors\":[{\"message\":\"Non-null violation\"}]}"),
            ["null_data_errors_literal"] = JsonLiteral("{\"data\":null,\"errors\":[{\"message\":\"Failure\"}]}"),
            ["batch_response_literal"] = JsonLiteral("[{\"data\":null},{\"data\":null}]"),
            ["null_data_json_literal"] = JsonLiteral("{\"data\":null}"),
            ["use_http_get"] = clientOptions.UseHttpGet,
            ["enable_batching"] = clientOptions.EnableBatching,
            ["get_fits"] = getUri.AbsoluteUri.Length <= clientOptions.MaxGetUrlLength,
            ["get_test_name"] = getUri.AbsoluteUri.Length <= clientOptions.MaxGetUrlLength ? "ExecuteAsync_Uses_Get_For_Query" : "ExecuteAsync_Falls_Back_To_Post_For_Long_Query_Url",
            ["json_literals"] = jsonLiterals
        };
    }

    private static void ValidateTestOptions(CSharpTestGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Suites);
        if (options.Namespace is { } value) ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var allowed = new HashSet<string>(["transport", "operations", "unions", "samples"], StringComparer.Ordinal);
        if (options.Suites.Any(suite => !allowed.Contains(suite))) throw new ArgumentException("Test suite names must be transport, operations, unions, or samples.", nameof(options));
    }

    private static string RawStringLiteral(string value)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in value)
        {
            currentRun = character == '"' ? currentRun + 1 : 0;
            longestRun = Math.Max(longestRun, currentRun);
        }

        var delimiter = new string('"', Math.Max(3, longestRun + 1));
        return delimiter + value + delimiter;
    }

    private static bool ImplementsInterface(GraphQlSchemaModel schema, string objectName, string targetInterface, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visited.Add(objectName)) return false;
        var interfaces = schema.Types.OfType<GraphQlObjectType>().Where(item => item.Name == objectName).SelectMany(item => item.Interfaces)
            .Concat(schema.Types.OfType<GraphQlInterfaceType>().Where(item => item.Name == objectName).SelectMany(item => item.Interfaces));
        foreach (var interfaceName in interfaces)
        {
            if (interfaceName == targetInterface || ImplementsInterface(schema, interfaceName, targetInterface, visited)) return true;
        }

        return false;
    }
}
