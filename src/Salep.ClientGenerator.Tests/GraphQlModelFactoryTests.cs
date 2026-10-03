using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Templates;
using Salep.ClientGenerator.Targets;
using Salep.GraphQLParser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Scriban.Runtime;
using Scriban.Syntax;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GraphQlModelFactoryTests
{
    [Fact]
    public void CreateSchema_PreservesDefinitionsExtensionsAndSourceOrder()
    {
        const string source = """
            schema { query: Root }
            directive @tag(label: String = "x") repeatable on FIELD_DEFINITION
            interface Node { id: ID! }
            type Root implements Node { id: ID! item: [String!]! find(filter: Filter = { enabled: true }): Product }
            extend type Root @tag { extra: Boolean }
            extend schema { mutation: Root }
            extend interface Node { name: String }
            input Filter { values: [[Int!]!]! }
            extend input Filter { enabled: Boolean }
            enum State { ACTIVE @tag(label: "active") }
            extend enum State { INACTIVE }
            union Search = Root
            extend union Search = Product
            scalar Timestamp
            extend scalar Timestamp @tag(label: "updated")
            type Product { id: ID! }
            """;

        var model = GraphQlModelFactory.CreateSchema(Parse(source));

        Assert.Equal(new[] { "Node", "Root", "Root", "Node", "Filter", "Filter", "State", "State", "Search", "Search", "Timestamp", "Timestamp", "Product" },
            model.Types.Select(type => type.Name));
        Assert.Equal(2, model.SchemaDefinitions.Count);
        Assert.Single(model.DirectiveDefinitions);
        Assert.Equal("Root", model.SchemaDefinitions[0].RootOperations[0].TypeName);
        Assert.True(model.SchemaDefinitions[1].IsExtension);
        Assert.Equal("mutation", model.SchemaDefinitions[1].RootOperations[0].Operation);
        Assert.Equal("FIELD_DEFINITION", model.DirectiveDefinitions[0].Locations.Single());
        Assert.True(model.Types[2].IsExtension);
        Assert.Equal("tag", model.Types[2].Directives.Single().Name);
        Assert.Single(model.Types.OfType<GraphQlInterfaceType>(), type => type.IsExtension);
        Assert.Single(model.Types.OfType<GraphQlInputObjectType>(), type => type.IsExtension);
        Assert.Single(model.Types.OfType<GraphQlEnumType>(), type => type.IsExtension);
        Assert.Single(model.Types.OfType<GraphQlUnionType>(), type => type.IsExtension);
        Assert.Single(model.Types.OfType<GraphQlScalarType>(), type => type.IsExtension);
        Assert.Equal("Node", Assert.Single(model.ObjectTypes, type => type.Name == "Root").Interfaces.Single());
        Assert.Single(model.ObjectTypes.Single(type => type.Name == "Root").Fields.Single(field => field.Name == "find").Arguments);
        Assert.Equal("String", Assert.IsType<GraphQlNamedTypeReference>(
            model.DirectiveDefinitions[0].Arguments[0].Type).Name);
        Assert.True(model.Types[1].Source.HasLocation);
        Assert.True(model.Types[1].Source.Line > 0);
    }

    [Fact]
    public void CreateExecutable_PreservesOperationFragmentsValuesAliasesAndNestedSelections()
    {
        const string source = """
            query ProductById($id: ID!, $include: Boolean = true) @skip(if: false) {
              product: product(id: $id) {
                ...ProductFields
                ... on PhysicalProduct { weight }
                tags(values: [ACTIVE, "new", 3.5, null, { key: "value" }])
              }
            }
            fragment ProductFields on Product { id name: display_name }
            """;

        var model = GraphQlModelFactory.CreateExecutable([Parse(source)]);

        var operation = Assert.Single(model.Operations);
        Assert.Equal("query", operation.OperationType);
        Assert.Equal("ProductById", operation.Name);
        Assert.Equal("id", operation.Variables[0].Name);
        Assert.Equal(GraphQlValueKind.BooleanLiteral, operation.Variables[1].DefaultValue!.Kind);
        Assert.Equal("skip", operation.Directives.Single().Name);
        var product = Assert.IsType<GraphQlFieldSelection>(operation.SelectionSet.Selections.Single());
        Assert.Equal("product", product.ResponseName);
        Assert.Equal("id", product.Arguments.Single().Value.Text);
        Assert.IsType<GraphQlFragmentSpreadSelection>(product.SelectionSet!.Selections[0]);
        Assert.Equal("PhysicalProduct", Assert.IsType<GraphQlInlineFragmentSelection>(product.SelectionSet.Selections[1]).TypeCondition);
        var tags = Assert.IsType<GraphQlFieldSelection>(product.SelectionSet.Selections[2]);
        Assert.Collection(tags.Arguments.Single().Value.Items!,
            item => Assert.Equal(GraphQlValueKind.EnumLiteral, item.Kind),
            item => Assert.Equal(GraphQlValueKind.StringLiteral, item.Kind),
            item => Assert.Equal("3.5", item.Text),
            item => Assert.Equal(GraphQlValueKind.NullLiteral, item.Kind),
            item => Assert.Equal(GraphQlValueKind.ObjectLiteral, item.Kind));
        Assert.Equal("ProductFields", Assert.Single(model.Fragments).Name);
        Assert.Equal("display_name", Assert.IsType<GraphQlFieldSelection>(model.Fragments[0].SelectionSet.Selections[1]).Name);
    }

    [Fact]
    public void CreateExecutable_ExpandsFragmentsAndAbstractResponseVariants()
    {
        const string schemaSource = """
            schema { query: Root }
            interface Node { id: ID! }
            type Product implements Node { id: ID! name: String! }
            type Customer implements Node { id: ID! email: String! }
            type Root { node: Node! search: Search! }
            union Search = Product | Customer
            """;
        const string operationSource = """
            query Read {
              node {
                commonId: id
                ...NodeFields
                ... on Product { label: name }
              }
              search { ... on Customer { contact: email } }
            }
            fragment NodeFields on Node { id }
            """;
        var schema = GraphQlModelFactory.CreateSchema(Parse(schemaSource));

        var operation = Assert.Single(GraphQlModelFactory.CreateExecutable(schema, [Parse(operationSource)]).Operations);

        Assert.Equal("Root", operation.ResponseProjection!.GraphQlTypeName);
        var node = operation.ResponseProjection.Fields[0];
        Assert.Equal("Node", Assert.IsType<GraphQlNamedTypeReference>(Assert.IsType<GraphQlNonNullTypeReference>(node.Type).NullableType).Name);
        Assert.Equal(new[] { "Product", "Customer" }, node.Variants.Select(variant => variant.GraphQlTypeName));
        Assert.Equal(new[] { "commonId", "id", "label" }, node.Variants[0].Fields.Select(field => field.ResponseName));
        Assert.Equal(new[] { "commonId", "id" }, node.Variants[1].Fields.Select(field => field.ResponseName));
        var search = operation.ResponseProjection.Fields[1];
        Assert.Equal(new[] { "Product", "Customer" }, search.Variants.Select(variant => variant.GraphQlTypeName));
        Assert.Empty(search.Variants[0].Fields);
        Assert.Equal("contact", search.Variants[1].Fields.Single().ResponseName);
    }

    [Theory]
    [InlineData("String", "string?")]
    [InlineData("String!", "string")]
    [InlineData("[String!]", "List<string>?")]
    [InlineData("[String!]!", "List<string>")]
    [InlineData("[[Int!]!]!", "List<List<int>>")]
    public void CSharpTarget_RendersGraphQlTypeWrappers(string typeExpression, string expected)
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse($"type Query {{ value: {typeExpression} }}"));
        var fieldType = Assert.IsType<GraphQlObjectType>(schema.ObjectTypes.Single()).Fields.Single().Type;

        Assert.Equal(expected, new CSharpCodeGenerationTarget().RenderType(fieldType, schema));
    }

    [Fact]
    public void CSharpTarget_AppliesConsumerNamingScalarAndNamespacePolicies()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("scalar Instant\ntype Query { created: Instant! }"));
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
        {
            DefaultNamespace = "Example.Generated",
            TypeName = name => $"Gql{name}",
            PropertyName = name => $"P_{name}",
            ParameterName = name => $"arg_{name}",
            ScalarMappings = new Dictionary<string, CSharpScalarMapping>(StringComparer.Ordinal)
            {
                ["Instant"] = new("NodaTime.Instant", true)
            },
            AdditionalImports = ["NodaTime"]
        });
        var field = schema.ObjectTypes.Single().Fields.Single();

        Assert.Equal("csharp", target.Id);
        Assert.Equal("Example.Generated", target.DefaultNamespace);
        Assert.Equal("Gqlproduct_type", target.TypeName("product_type"));
        Assert.Equal("P_created", target.PropertyName("created"));
        Assert.Equal("arg_productId", target.ParameterName("productId"));
        Assert.Equal("NodaTime.Instant", target.RenderType(field.Type, schema));
        Assert.True(target.IsValueType(field.Type, schema));
        Assert.Equal("\"quoted\\\"value\"", target.StringLiteral("quoted\"value"));
        Assert.Contains("NodaTime", target.GetImports(schema));
    }

    [Fact]
    public void CSharpTarget_RendersCrossClientTypeAndInterfaceOwners()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("interface Node { id: ID! } type Product implements Node { id: ID! } type Query { node: Node product: Product! }"));
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
        {
            TypeOwners = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Product"] = "Shared.Entities",
                ["INode"] = "Shared.Contracts",
                ["NodeResult"] = "Shared.Contracts"
            }
        });

        var productType = GraphQlModelFactory.Type(Parse("type Item { value: Product! }").Definitions.OfType<ObjectTypeDefinitionNode>().Single().Fields.Single().Type);
        var interfaceType = GraphQlModelFactory.Type(Parse("type Item { value: Node }").Definitions.OfType<ObjectTypeDefinitionNode>().Single().Fields.Single().Type);
        var templateModel = CSharpSchemaTemplateModelFactory.Create(schema, target);
        var interfaceOwner = new ScribanTemplateEngine().Render("{{ csharp.interface_owner \"Node\" }}", templateModel);
        var generated = new ScribanCSharpTemplateGenerator().GenerateSchema(schema, target,
            includedTypeNames: new HashSet<string>(StringComparer.Ordinal) { "Product", "Query" });

        Assert.Equal("Shared.Contracts", interfaceOwner);
        Assert.Equal("global::Shared.Entities.Product", target.RenderType(productType, schema));
        Assert.Equal("global::Shared.Contracts.NodeResult?", target.RenderType(interfaceType, schema));
        Assert.Equal("public sealed record Product : global::Shared.Contracts.INode",
            generated.Split('\n').Select(line => line.Trim()).Single(line => line.StartsWith("public sealed record Product", StringComparison.Ordinal)));
        Assert.DoesNotContain("public interface INode", generated, StringComparison.Ordinal);
        Assert.Contains("global::Shared.Contracts.NodeResult? Node", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void CSharpTarget_MapsUnknownCustomScalarsToStringByDefault()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("scalar Json\ntype Query { payload: Json! }"));
        var fieldType = schema.ObjectTypes.Single().Fields.Single().Type;

        Assert.Equal("string", new CSharpCodeGenerationTarget().RenderType(fieldType, schema));
    }

    [Fact]
    public void ScribanEngine_RendersAnExplicitModel()
    {
        var model = new ScriptObject
        {
            ["schema"] = ScriptObject.From(new { TypeName = "Product" }),
            ["items"] = new ScriptArray(new[] { "id", "name" })
        };

        var result = new ScribanTemplateEngine().Render(
            "{{ schema.type_name }}:{{ for item in items }}{{ item }};{{ end }}", model, "models.scriban");

        Assert.Equal("Product:id;name;", result);
    }

    [Fact]
    public void TemplateModel_ExposesTargetNamingAndTypeFunctions()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Product { id: ID! }"));
        var target = new CSharpCodeGenerationTarget();
        var model = ScribanTemplateModelFactory.Create(schema, target);

        var result = new ScribanTemplateEngine().Render(
            "{{ target.type_name \"product_type\" }}:{{ target.type schema.object_types[0].fields[0].type }}", model);

        Assert.Equal("ProductType:string", result);
    }

    [Fact]
    public void CSharpTemplateModel_MergesExtensionsAndPreparesImplementationViews()
    {
        const string source = """
            interface Node { id: ID! }
            interface Named implements Node { id: ID! name: String! }
            type Product implements Named & Node { id: ID! name: String! }
            extend type Product { price: Decimal }
            type Customer implements Node { id: ID! }
            union Search = Product
            extend union Search = Customer
            """;
        var schema = GraphQlModelFactory.CreateSchema(Parse(source));
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
        {
            DefaultNamespace = "Generated",
            UseNativeUnions = true,
            TypeOwners = new Dictionary<string, string>(StringComparer.Ordinal) { ["Product"] = "Shared.Models" }
        });
        var model = CSharpSchemaTemplateModelFactory.Create(schema, target);

        var rendered = new ScribanTemplateEngine().Render("""
            {{ for type in schema_types }}{{ type.kind }}:{{ type.name }}={{ type.fields.size }};{{ end }}
            {{ for field in schema_types[1].fields }}{{ field.name }}={{ field.is_inherited }};{{ end }}
            {{ csharp.use_native_unions }}:{{ csharp.type_owner "Product" }}
            """, model);

        Assert.Contains("object:Product=3;", rendered, StringComparison.Ordinal);
        Assert.Contains("interface_result:Node=0;", rendered, StringComparison.Ordinal);
        Assert.Contains("interface_result:Named=0;", rendered, StringComparison.Ordinal);
        Assert.Contains("id=true;name=false;", rendered, StringComparison.Ordinal);
        Assert.Contains("true:Shared.Models", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void CSharpTemplateGenerator_RendersSchemaDeclarationsFromScriban()
    {
        const string source = """
            interface Node { id: ID! }
            type Product implements Node { id: ID! name: String }
            extend type Product { price: Decimal }
            input CreateProduct { name: String! }
            enum Status { ACTIVE }
            union SearchResult = Product
            """;
        var schema = GraphQlModelFactory.CreateSchema(Parse(source));
        var generated = new ScribanCSharpTemplateGenerator().GenerateSchema(schema, new CSharpCodeGenerationTarget());

        Assert.Contains("public sealed record Product : global::Salep.Generated.INode", generated, StringComparison.Ordinal);
        Assert.Contains("[JsonPropertyName(\"price\")]", generated, StringComparison.Ordinal);
        Assert.Contains("public string? Name", generated, StringComparison.Ordinal);
        Assert.Contains("public required string Name", generated, StringComparison.Ordinal);
        Assert.Contains("[JsonStringEnumMemberName(\"ACTIVE\")]", generated, StringComparison.Ordinal);
        Assert.Contains("[Union]", generated, StringComparison.Ordinal);
        Assert.Contains("using Dunet;", generated, StringComparison.Ordinal);
        var converters = new ScribanCSharpTemplateGenerator().GenerateUnionConverters(schema, new CSharpCodeGenerationTarget());
        Assert.Contains("public static class UnionJsonConverters", converters, StringComparison.Ordinal);
        Assert.Contains("SearchResultJsonConverter", converters, StringComparison.Ordinal);
        var localConverters = new ScribanCSharpTemplateGenerator().GenerateUnionConverters(schema, new CSharpCodeGenerationTarget(),
            includedTypeNames: new HashSet<string>(StringComparer.Ordinal) { "SearchResult" });
        Assert.Contains("SearchResultJsonConverter", localConverters, StringComparison.Ordinal);
        Assert.DoesNotContain("NodeResultJsonConverter", localConverters, StringComparison.Ordinal);

#if NET11_0_OR_GREATER
        var nativeTarget = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions { UseNativeUnions = true });
        var nativeSchema = new ScribanCSharpTemplateGenerator().GenerateSchema(schema, nativeTarget);
        var nativeConverters = new ScribanCSharpTemplateGenerator().GenerateUnionConverters(schema, nativeTarget);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("ScribanSchemaFixture",
            [CSharpSyntaxTree.ParseText("#nullable enable\n" + nativeSchema, parseOptions, cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + nativeConverters, parseOptions, cancellationToken: TestContext.Current.CancellationToken)],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        using var image = new MemoryStream();
        var emit = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(image.ToArray()));
        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        assembly.GetType("Salep.Generated.UnionJsonConverters", throwOnError: true)!.GetMethod("Register")!
            .Invoke(null, [serializerOptions]);
        var searchType = assembly.GetType("Salep.Generated.SearchResult", throwOnError: true)!;
        var result = JsonSerializer.Deserialize("{\"__typename\":\"Product\",\"id\":\"p-7\"}", searchType, serializerOptions)!;
        var product = result.GetType().GetProperty("Value")!.GetValue(result)!;
        Assert.Equal("p-7", product.GetType().GetProperty("Id")!.GetValue(product));
#endif
    }

    [Fact]
    public void CSharpTemplateGenerator_SupportsNativeUnionAndTemplateOverrides()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Item { id: ID! } union Result = Item"));
        var generator = new ScribanCSharpTemplateGenerator();
        var nativeOutput = generator.GenerateSchema(schema, new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions { UseNativeUnions = true }));
        var customOutput = generator.GenerateSchema(schema, new CSharpCodeGenerationTarget(), "custom {{ target.type_name \"item_type\" }}");

        Assert.Contains("public union Result(Item);", nativeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("using Dunet;", nativeOutput, StringComparison.Ordinal);
        Assert.Equal("custom ItemType", customOutput);
    }

    [Fact]
    public void CSharpTemplateGenerator_RendersOperationContractsVariablesAndAbstractResponseTypes()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("interface Node { id: ID! } type Product implements Node { id: ID! name: String } type Query { node: Node! product(id: ID!): Product }"));
        var executable = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Read($id: ID!) { node { id ... on Product { label: name } } product(id: $id) { name } }")]);

        var generated = new ScribanCSharpTemplateGenerator().GenerateOperations(schema, executable, new CSharpCodeGenerationTarget());

        Assert.Contains("ReadOperation : IGraphQLOperation<ReadResponse, ReadVariables>", generated, StringComparison.Ordinal);
        Assert.Contains("public interface IGraphQLOperations", generated, StringComparison.Ordinal);
        Assert.Contains("ReadAsync", generated, StringComparison.Ordinal);
        Assert.Contains("public string Id { get; init; } = default!;", generated, StringComparison.Ordinal);
        Assert.Contains("[JsonPropertyName(\"node\")]", generated, StringComparison.Ordinal);
        Assert.Contains("abstract record ReadResponseSelection1", generated, StringComparison.Ordinal);
        Assert.Contains("ReadResponseSelection1JsonConverter", generated, StringComparison.Ordinal);
        Assert.Contains("\"__typename\"", generated, StringComparison.Ordinal);
        Assert.Contains("\"Product\" => JsonSerializer.Deserialize<ReadResponseSelection1Product>", generated, StringComparison.Ordinal);
        Assert.Contains("JsonPropertyName(\"label\")", generated, StringComparison.Ordinal);

        var compilation = CSharpCompilation.Create("ScribanOperationFixture",
            [CSharpSyntaxTree.ParseText("public sealed record Product; public interface IGraphQLOperation<TResponse, TVariables> { string OperationName { get; } string Query { get; } TVariables? Variables { get; } } public sealed record GraphQLResponse<T>(T? Data, object[]? Errors); public sealed class GraphQLClient { public global::System.Threading.Tasks.Task<GraphQLResponse<T>> ExecuteAsync<T, V>(IGraphQLOperation<T, V> operation, global::System.Threading.CancellationToken cancellationToken = default) => throw new global::System.NotImplementedException(); }", cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + generated, cancellationToken: TestContext.Current.CancellationToken)],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void CSharpTemplateGenerator_RendersSchemaAwareOperationSampleAndVariablePolicies()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("scalar Timestamp input SearchInput { term: String! since: Timestamp } type Query { search(input: SearchInput): [String!]! }"));
        var executable = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Search($input: SearchInput!, $unused: Int, $limit: Int = 5) { search(input: $input) }")]);
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
        {
            ScalarMappings = new Dictionary<string, CSharpScalarMapping>(StringComparer.Ordinal)
            {
                ["Timestamp"] = new("DateTimeOffset", true, "DateTimeOffset.UnixEpoch", "\"1970-01-01T00:00:00Z\"")
            }
        });
        var generated = new ScribanCSharpTemplateGenerator().GenerateOperationSample(schema, executable, target,
            documentOptions: new GraphQlOperationDocumentOptions { OmitUnusedVariables = true, InlineDefaultVariables = true });

        Assert.Contains("public static async Task RunAsync(HttpClient httpClient, Uri endpoint)", generated, StringComparison.Ordinal);
        Assert.Contains("new SearchInput { Term = \"sample\", Since = DateTimeOffset.UnixEpoch }", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Unused", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Limit", generated, StringComparison.Ordinal);
        Assert.Contains("api.SearchAsync(sampleOperation1)", generated, StringComparison.Ordinal);

        var generator = new ScribanCSharpTemplateGenerator();
        var shared = generator.GenerateSharedTypes(target);
        var client = generator.GenerateClient(target);
        var schemaTypes = generator.GenerateSchema(schema, target);
        var unionConverters = generator.GenerateUnionConverters(schema, target);
        var operations = generator.GenerateOperations(schema, executable, target,
            new GraphQlOperationDocumentOptions { OmitUnusedVariables = true, InlineDefaultVariables = true });
        Assert.DoesNotContain("Unused", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("Limit", operations, StringComparison.Ordinal);
        var compilation = CSharpCompilation.Create("ScribanOperationSampleFixture",
            [CSharpSyntaxTree.ParseText("#nullable enable\n" + shared, cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + client, cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + schemaTypes, cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + unionConverters, cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + operations, cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + generated, cancellationToken: TestContext.Current.CancellationToken)],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task CSharpTemplateGenerator_RendersSelectableTestSuitesWithNamespaceAndRawJson()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { greeting(name: String!): String! }"));
        var executable = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Greeting($name: String!) { greeting(name: $name) }")]);
        var target = new CSharpCodeGenerationTarget();
        var generator = new ScribanCSharpTemplateGenerator();
        var options = new CSharpTestGenerationOptions
        {
            Namespace = "Fixture.GeneratedTests",
            Suites = new HashSet<string>(["transport", "operations", "samples"], StringComparer.Ordinal),
            UseRawJsonLiterals = true,
            EmitAgentInstructions = true
        };

        var tests = generator.GenerateTestSources(schema, executable, target, testOptions: options);

        Assert.Contains(tests, file => file.FileName == "TestHttpMessageHandler.cs");
        Assert.Contains(tests, file => file.FileName == "GraphQLClientPayloadTests.cs");
        Assert.Contains(tests, file => file.FileName == "OperationsMetadataTests.cs");
        Assert.Contains(tests, file => file.FileName == "OperationsSampleTests.cs");
        Assert.Contains(tests, file => file.FileName == "agents.md");
        Assert.Contains("namespace Fixture.GeneratedTests;", tests.Single(file => file.FileName == "OperationsMetadataTests.cs").Content, StringComparison.Ordinal);
        Assert.Contains("    [Fact]", tests.Single(file => file.FileName == "OperationsMetadataTests.cs").Content, StringComparison.Ordinal);
        Assert.Contains("\"\"\"{\"data\":{}}\"\"\"", tests.Single(file => file.FileName == "GraphQLClientPayloadTests.cs").Content, StringComparison.Ordinal);
        Assert.Contains("C# raw string literals", tests.Single(file => file.FileName == "agents.md").Content, StringComparison.Ordinal);

        var clientOptions = new CSharpClientGenerationOptions
        {
            UseHttpGet = true,
            EnableBatching = true,
            EmitOperationSample = true,
            EmitAgentInstructions = true
        };
        var sourceFiles = generator.GenerateClientSources(schema, executable, target, clientOptions)
            .Where(file => file.FileName.EndsWith(".cs", StringComparison.Ordinal)).Select(file => file.Content).ToList();
        sourceFiles.AddRange(tests.Where(file => file.FileName.EndsWith(".cs", StringComparison.Ordinal)).Select(file => file.Content));
        var compilation = CSharpCompilation.Create("ScribanGeneratedTestsFixture",
            sourceFiles.Select(source => CSharpSyntaxTree.ParseText("#nullable enable\n" + source, cancellationToken: TestContext.Current.CancellationToken)),
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var assemblyImage = new MemoryStream();
        var emit = compilation.Emit(assemblyImage, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(assemblyImage.ToArray()));
        foreach (var type in assembly.GetExportedTypes())
        {
            var testMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<FactAttribute>().Any());
            foreach (var method in testMethods)
            {
                var result = method.Invoke(Activator.CreateInstance(type), null);
                if (result is Task task) await task;
            }
        }
    }

    [Fact]
    public void CSharpTemplateGenerator_UsesCustomScalarJsonAndAbstractDiscriminatorsInResponseTests()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("schema { query: Query } scalar Timestamp type Query { search: Search! } union Search = Product | Customer type Product { createdAt: Timestamp! } type Customer { email: String! }"));
        var executable = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Search { search { ... on Product { createdAt } ... on Customer { email } } }")]);
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
        {
            ScalarMappings = new Dictionary<string, CSharpScalarMapping>(StringComparer.Ordinal)
            {
                ["Timestamp"] = new("DateTimeOffset", true, "DateTimeOffset.UnixEpoch", "\"2030-01-02T03:04:05Z\"")
            }
        });

        var tests = new ScribanCSharpTemplateGenerator().GenerateTestSources(schema, executable, target,
            testOptions: new CSharpTestGenerationOptions
            {
                Suites = new HashSet<string>(["operations", "unions"], StringComparer.Ordinal),
                UseRawJsonLiterals = true
            });

        var responseTests = tests.Single(file => file.FileName == "OperationsResponseTests.cs").Content;
        var unionTests = tests.Single(file => file.FileName == "UnionConverterTests.cs").Content;
        Assert.Contains("__typename", responseTests, StringComparison.Ordinal);
        Assert.Contains("2030-01-02T03:04:05Z", responseTests, StringComparison.Ordinal);
        Assert.Contains("Search_Deserializes_Product", unionTests, StringComparison.Ordinal);
        Assert.Contains("Search_Deserializes_Customer", unionTests, StringComparison.Ordinal);
        Assert.Contains("\"\"\"{\"__typename\":\"Product\"}\"\"\"", unionTests, StringComparison.Ordinal);
    }

    [Fact]
    public void CSharpTemplateGenerator_AllowsTestTemplateOverrides()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Query { value: String }"));
        var executable = GraphQlModelFactory.CreateExecutable(schema, [Parse("query Read { value }")]);
        var files = new ScribanCSharpTemplateGenerator().GenerateTestSources(schema, executable, new CSharpCodeGenerationTarget(),
            testOptions: new CSharpTestGenerationOptions { Suites = new HashSet<string>(["operations"], StringComparer.Ordinal) },
            templateOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operation-metadata-tests"] = "// {{ settings.client_name }} metadata output"
            });

        Assert.Equal("// GraphQLClient metadata output", files.Single(file => file.FileName == "OperationsMetadataTests.cs").Content);
    }

    [Fact]
    public void CSharpSampleValueFactory_UsesScalarPoliciesEnumsAndOwnedInputTypes()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("enum State { ACTIVE } input Filter { state: State! count: Int! }"));
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
        {
            TypeOwners = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Filter"] = "Shared.Types",
                ["State"] = "Shared.Types"
            }
        });
        var filter = Assert.Single(schema.InputTypes);

        Assert.Equal("new global::Shared.Types.Filter { State = global::Shared.Types.State.ACTIVE, Count = 123 }",
            new CSharpSampleValueFactory(schema, target).Create(new GraphQlNamedTypeReference(filter.Name, new GraphQlSourceSpan(0, 0, 0, 0, false))));
    }

    [Theory]
    [InlineData("Opinionated", "Client")]
    [InlineData("MinimalDependencies", "Client")]
    public Task CSharpTemplateGenerator_RendersExistingSampleGraphQlInputs(string sample, string clientProject)
    {
        var repositoryRoot = FindRepositoryRoot();
        var sampleRoot = Path.Combine(repositoryRoot, "src", "samples", "Scriban", sample);
        var schemaPath = Path.Combine(repositoryRoot, "src", "samples", "Salep.Samples.GraphQLServer", "Generated", "schema.graphql");
        var operationDirectory = Path.Combine(sampleRoot, clientProject, "graphql");
        var schema = GraphQlModelFactory.CreateSchema(Parse(File.ReadAllText(schemaPath)));
        var executableDocuments = Directory.GetFiles(operationDirectory, "*.graphql", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => Parse(File.ReadAllText(path))).ToArray();
        var executable = GraphQlModelFactory.CreateExecutable(schema, executableDocuments);
        var generator = new ScribanCSharpTemplateGenerator();
        var clientOptions = new CSharpClientGenerationOptions
        {
            UseHttpGet = true,
            EnableBatching = true,
            EmitOperationSample = true,
            EmitAgentInstructions = true
        };
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions
        {
            DefaultNamespace = "GraphQL.Sharp.Generated",
#if NET11_0_OR_GREATER
            UseNativeUnions = true
#endif
        });
        var clientFiles = generator.GenerateClientSources(schema, executable, target, clientOptions);
        var testFiles = generator.GenerateTestSources(schema, executable, target, clientOptions,
            new CSharpTestGenerationOptions
            {
                Namespace = "Salep.Sample.Generated.Tests",
                Suites = new HashSet<string>(["transport", "operations", "unions", "samples"], StringComparer.Ordinal),
                    UseRawJsonLiterals = true,
                EmitAgentInstructions = true
            });

        Assert.Contains(clientFiles, file => file.FileName == "Operations.Sample.cs");
        Assert.Contains(clientFiles, file => file.FileName == "agents.md");
        Assert.Contains(testFiles, file => file.FileName == "GraphQLClientPayloadTests.cs");
        Assert.Contains(testFiles, file => file.FileName == "OperationsResponseTests.cs");
        Assert.Contains(testFiles, file => file.FileName == "UnionConverterTests.cs");
        Assert.Contains(testFiles, file => file.FileName == "OperationsSampleTests.cs");
        Assert.NotEmpty(executable.Operations);
#if NET11_0_OR_GREATER
        return CompileAndRunGeneratedFilesAsync(clientFiles.Concat(testFiles).Where(file => file.FileName.EndsWith(".cs", StringComparison.Ordinal))
            .Select(file => file.Content), "Scriban" + sample.Replace("Dependencies", string.Empty, StringComparison.Ordinal) + "SampleFixture");
#else
        return Task.CompletedTask;
#endif
    }

#if NET11_0_OR_GREATER
    [Fact]
    public async Task GeneratedNativeUnionTests_CompileAndRun()
    {
        var schema = GraphQlModelFactory.CreateSchema(Parse("type Product { id: ID! } type Customer { email: String! } union Search = Product | Customer"));
        var target = new CSharpCodeGenerationTarget(new CSharpCodeGenerationOptions { UseNativeUnions = true });
        var generator = new ScribanCSharpTemplateGenerator();
        var testOptions = new CSharpTestGenerationOptions { Suites = new HashSet<string>(["unions"], StringComparer.Ordinal) };
        var tests = generator.GenerateTestSources(schema, GraphQlModelFactory.CreateExecutable([]), target, testOptions: testOptions);
        var sources = new[] { generator.GenerateSchema(schema, target), generator.GenerateUnionConverters(schema, target) }
            .Concat(tests.Select(file => file.Content));
        var compilation = CSharpCompilation.Create("ScribanNativeUnionTestsFixture",
            sources.Select(source => CSharpSyntaxTree.ParseText("#nullable enable\n" + source,
                new CSharpParseOptions(LanguageVersion.Preview), cancellationToken: TestContext.Current.CancellationToken)),
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var assemblyImage = new MemoryStream();
        var emit = compilation.Emit(assemblyImage, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(assemblyImage.ToArray()));
        var testType = assembly.GetType("Salep.Generated.Tests.UnionConverterTests", throwOnError: true)!;
        foreach (var method in testType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!method.GetCustomAttributes<FactAttribute>().Any()) continue;
            var result = method.Invoke(Activator.CreateInstance(testType), null);
            if (result is Task task) await task;
        }
    }
#endif

    [Fact]
    public void CSharpTemplateGenerator_RendersCompilableSharedTypesAndHttpClient()
    {
        var generator = new ScribanCSharpTemplateGenerator();
        var target = new CSharpCodeGenerationTarget();
        var shared = generator.GenerateSharedTypes(target);
        var client = generator.GenerateClient(target, new CSharpClientGenerationOptions
        {
            ClientClassName = "CatalogClient",
            UseHttpGet = true,
            EnableBatching = true,
            MaxGetUrlLength = 512,
            RegisterUnionConverters = false,
            SerializerConfigurationStatements = ["_jsonOptions.WriteIndented = false;"]
        });

        Assert.Contains("public interface IGraphQLOperation<TResponse, TVariables>", shared, StringComparison.Ordinal);
        Assert.Contains("public sealed record GraphQLResponse<TResponse>", shared, StringComparison.Ordinal);
        Assert.Contains("public sealed class CatalogClient", client, StringComparison.Ordinal);
        Assert.Contains("static bool UseHttpGet => true", client, StringComparison.Ordinal);
        Assert.Contains("static int MaxGetUrlLength => 512", client, StringComparison.Ordinal);
        Assert.Contains("_jsonOptions.WriteIndented = false;", client, StringComparison.Ordinal);
        Assert.Contains("ExecuteBatchAsync", client, StringComparison.Ordinal);

        var compilation = CSharpCompilation.Create("ScribanClientFixture",
            [CSharpSyntaxTree.ParseText("#nullable enable\n" + shared, cancellationToken: TestContext.Current.CancellationToken),
             CSharpSyntaxTree.ParseText("#nullable enable\n" + client, cancellationToken: TestContext.Current.CancellationToken)],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ScribanEngine_ReportsTemplateParseErrorsWithSourceName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ScribanTemplateEngine().Render("{{ if }}", new ScriptObject(), "broken.scriban"));

        Assert.Contains("broken.scriban", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScribanEngine_RejectsUndefinedVariables()
    {
        Assert.Throws<ScriptRuntimeException>(() => new ScribanTemplateEngine().Render("{{ missing }}", new ScriptObject(), "strict.scriban"));
    }

    private static DocumentNode Parse(string source) => global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(source.AsMemory()));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "samples"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Salep repository root from the test output directory.");
    }

    private static async Task CompileAndRunGeneratedFilesAsync(IEnumerable<string> sources, string assemblyName)
    {
        var compilation = CSharpCompilation.Create(assemblyName,
            sources.Select(source => CSharpSyntaxTree.ParseText("#nullable enable\n" + source,
                new CSharpParseOptions(LanguageVersion.Preview), cancellationToken: TestContext.Current.CancellationToken)),
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var assemblyImage = new MemoryStream();
        var emit = compilation.Emit(assemblyImage, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(assemblyImage.ToArray()));
        foreach (var type in assembly.GetExportedTypes())
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<FactAttribute>().Any());
            foreach (var method in methods)
            {
                var result = method.Invoke(Activator.CreateInstance(type), null);
                if (result is Task task) await task;
            }
        }
    }
}
