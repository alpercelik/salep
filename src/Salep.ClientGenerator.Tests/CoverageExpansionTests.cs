using Salep.ClientGenerator.Diagnostics;
using Salep.ClientGenerator.Generation;
using System.Reflection;
using System.Text.Json;
using Salep.GraphQLParser;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class CoverageExpansionTests
{
    [Fact]
    public void DiagnosticsReporterCollectWarningsReportsUsedScalarExtensionAndFragmentSpreadInlineWarning()
    {
        var root = CreateTempDirectory();
        try
        {
            var schemaPath = Path.Combine(root, "schema.graphql");
            var operationsPath = Path.Combine(root, "graphql");
            var configPath = Path.Combine(root, "salep.json");
            Directory.CreateDirectory(operationsPath);

            File.WriteAllText(schemaPath,
                "schema { query: Query }\n" +
                "extend schema @deprecated\n" +
                "directive @opTag(name: String!) on QUERY | FIELD\n" +
                "scalar BigCustom\n" +
                "type Query { user(id: ID!): User! }\n" +
                "type User { id: ID!, name: String!, custom: BigCustom }\n");

            File.WriteAllText(Path.Combine(operationsPath, "query.graphql"),
                "query GetUser($id: ID! = \"1\") @opTag(name: \"q\") {\n" +
                "  user(id: $id) {\n" +
                "    ...UserCore\n" +
                "  }\n" +
                "}\n" +
                "fragment UserCore on User {\n" +
                "  id\n" +
                "  name\n" +
                "}\n");

            FixtureConfiguration.Write(configPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"inlineDefaultVariables\": true\n" +
                "}\n");

            var warnings = DiagnosticsReporter.CollectWarnings(schemaPath, operationsPath, configPath);

            Assert.Contains("Scalar 'BigCustom' has no mapping. It will be generated as string.", warnings);
            Assert.Contains("Schema extensions are ignored by the generator.", warnings);
            Assert.DoesNotContain("Directive definitions are ignored by the generator.", warnings);
            Assert.Contains("Operation 'GetUser' contains fragment spreads; inline default variables are skipped.", warnings);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GraphQLGeneratorInlineDefaultsAndOmitUnusedRewritesQueryAndRemovesVariableDefinitions()
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "RewriteProject");

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser($id: ID! = \"1\", $unused: String) {\n" +
                "  user(id: $id) { id name }\n" +
                "}\n");

            FixtureConfiguration.Write(project.ConfigPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"inlineDefaultVariables\": true,\n" +
                "  \"omitUnusedVariables\": true,\n" +
                "  \"generateTests\": false,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));

            var operationsPath = Path.Combine(project.GeneratedDirectory, "Operations.cs");
            var operationsSource = File.ReadAllText(operationsPath);

            Assert.DoesNotContain("$id", operationsSource, StringComparison.Ordinal);
            Assert.DoesNotContain("$unused", operationsSource, StringComparison.Ordinal);
            Assert.Contains("user(id:", operationsSource, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GraphQLGeneratorFragmentSpreadSkipsInlineDefaultVariableRewrite()
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "FragmentProject");

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser($id: ID! = \"1\") {\n" +
                "  user(id: $id) {\n" +
                "    ...UserCore\n" +
                "  }\n" +
                "}\n" +
                "fragment UserCore on User {\n" +
                "  id\n" +
                "  name\n" +
                "}\n");

            FixtureConfiguration.Write(project.ConfigPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"inlineDefaultVariables\": true,\n" +
                "  \"omitUnusedVariables\": true,\n" +
                "  \"generateTests\": false,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));

            var operationsPath = Path.Combine(project.GeneratedDirectory, "Operations.cs");
            var operationsSource = File.ReadAllText(operationsPath);

            Assert.Contains("$id", operationsSource, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GraphQLGeneratorGenerateSampleEmitsSampleAndSampleTests()
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "SampleProject");

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser {\n" +
                "  user(id: \"1\") { id name }\n" +
                "}\n");

            FixtureConfiguration.Write(project.ConfigPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"generateSample\": true,\n" +
                "  \"generateTests\": true,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));

            var samplePath = Path.Combine(project.GeneratedDirectory, "Operations.Sample.cs");
            var sampleTestsPath = Path.Combine(project.TestsDirectory, "OperationsSampleTests.cs");

            Assert.True(File.Exists(samplePath));
            Assert.True(File.Exists(sampleTestsPath));
            Assert.Contains("RunAsync", File.ReadAllText(samplePath), StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void GenerationPreservesDeveloperOwnedDependencies(bool customMapping, bool generateTests)
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "PackageProject");
            var projectPaths = new[]
            {
                Path.Combine(root, "GeneratedClient", "GeneratedClient.csproj"),
                Path.Combine(root, "GeneratedClient.Tests", "GeneratedClient.Tests.csproj"),
                Path.Combine(project.ProjectDirectory, "Consumer.csproj"),
                Path.Combine(root, "Directory.Packages.props")
            };
            var original = new Dictionary<string, byte[]>();
            foreach (var path in projectPaths)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, path.EndsWith(".props", StringComparison.Ordinal)
                    ? "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include=\"NodaTime\" Version=\"3.2.1\" /></ItemGroup></Project>"
                    : "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"NodaTime\" /></ItemGroup></Project>");
                original[path] = File.ReadAllBytes(path);
            }

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser { user(id: \"1\") { id name createdAt } }");
            var config = new Dictionary<string, object>
            {
                ["schemaPath"] = "./schema.graphql",
                ["operationsPath"] = "./graphql",
                ["outputDirectory"] = "./Generated",
                ["testsOutputDirectory"] = "./GeneratedTests",
                ["useNodaTime"] = !customMapping,
                ["generateTests"] = generateTests,
                ["generateClient"] = true
            };
            if (customMapping)
                config["scalarMappings"] = new Dictionary<string, string> { ["DateTime"] = "NodaTime.Instant" };
            FixtureConfiguration.Write(project.ConfigPath, JsonSerializer.Serialize(config));

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));
            Assert.Contains("NodaTime.Instant", File.ReadAllText(Path.Combine(project.GeneratedDirectory, "SchemaTypes.cs")), StringComparison.Ordinal);

            // Repeat through the CLI to verify that generation remains side-effect free for dependencies.
            var args = new List<string> { "--config", project.ConfigPath, "--working-directory", project.ProjectDirectory };
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            Assert.Equal(0, Salep.ClientGenerator.Cli.Program.Run(args.ToArray(), stdout, stderr));
            Assert.Empty(stderr.ToString());
            foreach (var (path, bytes) in original)
                Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GeneratorConfigTryGetScalarSampleExpressionAndScalarMappingFallbackAreCovered()
    {
        var root = CreateTempDirectory();
        try
        {
            var configPath = Path.Combine(root, "salep.json");
            FixtureConfiguration.Write(configPath,
                "{\n" +
                "  \"scalarMappings\": {\n" +
                "    \"MyInt\": \"int\",\n" +
                "    \"MyRef\": \"Custom.Ref\",\n" +
                "    \"MyDouble\": \"double\",\n" +
                "    \"MyBool\": \"bool\",\n" +
                "    \"MyLong\": \"long\",\n" +
                "    \"MyDecimal\": \"decimal\",\n" +
                "    \"MyDateTime\": \"DateTime\",\n" +
                "    \"MyDateTimeOffset\": \"DateTimeOffset\",\n" +
                "    \"MyGuid\": \"Guid\",\n" +
                "    \"MyInstant\": \"NodaTime.Instant\",\n" +
                "    \"MyUnknown\": \"Some.Unknown.Type\"\n" +
                "  },\n" +
                "  \"scalarSampleExpressions\": {\n" +
                "    \"MyInt\": \"123\"\n" +
                "  }\n" +
                "}\n");

            var configType = typeof(SalepGenerator).Assembly.GetType("Salep.ClientGenerator.Config.GeneratorConfig", throwOnError: true)!;
            var load = configType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)!;
            var config = load.Invoke(null, [configPath])!;

            var tryGetSampleExpression = configType.GetMethod("TryGetScalarSampleExpression", BindingFlags.Public | BindingFlags.Instance)!;
            var sampleArgs = new object[] { "MyInt", null! };
            var foundExpression = (bool)tryGetSampleExpression.Invoke(config, sampleArgs)!;
            Assert.True(foundExpression);
            Assert.Equal("123", sampleArgs[1] as string);

            var missingSampleArgs = new object[] { "MissingScalar", null! };
            var missingExpression = (bool)tryGetSampleExpression.Invoke(config, missingSampleArgs)!;
            Assert.False(missingExpression);
            Assert.Equal(string.Empty, missingSampleArgs[1] as string);

            var tryGetScalarMapping = configType.GetMethod("TryGetScalarMapping", BindingFlags.Public | BindingFlags.Instance)!;

            var myIntArgs = new object[] { "MyInt", null!, null! };
            var foundIntMapping = (bool)tryGetScalarMapping.Invoke(config, myIntArgs)!;
            Assert.True(foundIntMapping);
            Assert.Equal("int", myIntArgs[1] as string);
            Assert.True((bool)myIntArgs[2]);

            var myRefArgs = new object[] { "MyRef", null!, null! };
            var foundRefMapping = (bool)tryGetScalarMapping.Invoke(config, myRefArgs)!;
            Assert.True(foundRefMapping);
            Assert.Equal("Custom.Ref", myRefArgs[1] as string);
            Assert.False((bool)myRefArgs[2]);

            var myDoubleArgs = new object[] { "MyDouble", null!, null! };
            var foundDoubleMapping = (bool)tryGetScalarMapping.Invoke(config, myDoubleArgs)!;
            Assert.True(foundDoubleMapping);
            Assert.Equal("double", myDoubleArgs[1] as string);
            Assert.True((bool)myDoubleArgs[2]);

            var myBoolArgs = new object[] { "MyBool", null!, null! };
            var foundBoolMapping = (bool)tryGetScalarMapping.Invoke(config, myBoolArgs)!;
            Assert.True(foundBoolMapping);
            Assert.Equal("bool", myBoolArgs[1] as string);
            Assert.True((bool)myBoolArgs[2]);

            var myLongArgs = new object[] { "MyLong", null!, null! };
            var foundLongMapping = (bool)tryGetScalarMapping.Invoke(config, myLongArgs)!;
            Assert.True(foundLongMapping);
            Assert.Equal("long", myLongArgs[1] as string);
            Assert.True((bool)myLongArgs[2]);

            var myDecimalArgs = new object[] { "MyDecimal", null!, null! };
            var foundDecimalMapping = (bool)tryGetScalarMapping.Invoke(config, myDecimalArgs)!;
            Assert.True(foundDecimalMapping);
            Assert.Equal("decimal", myDecimalArgs[1] as string);
            Assert.True((bool)myDecimalArgs[2]);

            var myDateTimeArgs = new object[] { "MyDateTime", null!, null! };
            var foundDateTimeMapping = (bool)tryGetScalarMapping.Invoke(config, myDateTimeArgs)!;
            Assert.True(foundDateTimeMapping);
            Assert.Equal("DateTime", myDateTimeArgs[1] as string);
            Assert.True((bool)myDateTimeArgs[2]);

            var dtoArgs = new object[] { "MyDateTimeOffset", null!, null! };
            var foundDtoMapping = (bool)tryGetScalarMapping.Invoke(config, dtoArgs)!;
            Assert.True(foundDtoMapping);
            Assert.Equal("DateTimeOffset", dtoArgs[1] as string);
            Assert.True((bool)dtoArgs[2]);

            var guidArgs = new object[] { "MyGuid", null!, null! };
            var foundGuidMapping = (bool)tryGetScalarMapping.Invoke(config, guidArgs)!;
            Assert.True(foundGuidMapping);
            Assert.Equal("Guid", guidArgs[1] as string);
            Assert.True((bool)guidArgs[2]);

            var instantArgs = new object[] { "MyInstant", null!, null! };
            var foundInstantMapping = (bool)tryGetScalarMapping.Invoke(config, instantArgs)!;
            Assert.True(foundInstantMapping);
            Assert.Equal("NodaTime.Instant", instantArgs[1] as string);
            Assert.True((bool)instantArgs[2]);

            var unknownArgs = new object[] { "MyUnknown", null!, null! };
            var foundUnknownMapping = (bool)tryGetScalarMapping.Invoke(config, unknownArgs)!;
            Assert.True(foundUnknownMapping);
            Assert.Equal("Some.Unknown.Type", unknownArgs[1] as string);
            Assert.False((bool)unknownArgs[2]);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GeneratorConfigTryGetScalarSampleJsonLiteralCoversPresentAndMissing()
    {
        var root = CreateTempDirectory();
        try
        {
            var configPath = Path.Combine(root, "salep.json");
            FixtureConfiguration.Write(configPath,
                "{\n" +
                "  \"scalarSampleJsonLiterals\": {\n" +
                "    \"MyScalar\": \"\\\"value\\\"\"\n" +
                "  }\n" +
                "}\n");

            var configType = typeof(SalepGenerator).Assembly.GetType("Salep.ClientGenerator.Config.GeneratorConfig", throwOnError: true)!;
            var load = configType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)!;
            var config = load.Invoke(null, [configPath])!;

            var tryGetJsonLiteral = configType.GetMethod("TryGetScalarSampleJsonLiteral", BindingFlags.Public | BindingFlags.Instance)!;

            var foundArgs = new object[] { "MyScalar", null! };
            var found = (bool)tryGetJsonLiteral.Invoke(config, foundArgs)!;
            Assert.True(found);
            Assert.Equal("\"value\"", foundArgs[1] as string);

            var missingArgs = new object[] { "Missing", null! };
            var missing = (bool)tryGetJsonLiteral.Invoke(config, missingArgs)!;
            Assert.False(missing);
            Assert.Equal(string.Empty, missingArgs[1] as string);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void DiagnosticsReporterInlineFragmentsWithoutFragmentSpreadsDoNotAddInlineDefaultWarning()
    {
        var root = CreateTempDirectory();
        try
        {
            var schemaPath = Path.Combine(root, "schema.graphql");
            var operationsPath = Path.Combine(root, "graphql");
            var configPath = Path.Combine(root, "salep.json");
            Directory.CreateDirectory(operationsPath);

            File.WriteAllText(schemaPath,
                "schema { query: Query }\n" +
                "type Query { user(id: ID!): User! }\n" +
                "type User { id: ID!, name: String! }\n");

            File.WriteAllText(Path.Combine(operationsPath, "query.graphql"),
                "query GetUser($id: ID! = \"1\") {\n" +
                "  user(id: $id) {\n" +
                "    ... on User {\n" +
                "      id\n" +
                "      name\n" +
                "    }\n" +
                "  }\n" +
                "}\n");

            FixtureConfiguration.Write(configPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"inlineDefaultVariables\": true\n" +
                "}\n");

            var warnings = DiagnosticsReporter.CollectWarnings(schemaPath, operationsPath, configPath);
            Assert.DoesNotContain(
                "Operation 'GetUser' contains fragment spreads; inline default variables are skipped.",
                warnings);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void DiagnosticsReporterNestedInlineFragmentWithFragmentSpreadAddsInlineDefaultWarning()
    {
        var root = CreateTempDirectory();
        try
        {
            var schemaPath = Path.Combine(root, "schema.graphql");
            var operationsPath = Path.Combine(root, "graphql");
            var configPath = Path.Combine(root, "salep.json");
            Directory.CreateDirectory(operationsPath);

            File.WriteAllText(schemaPath,
                "schema { query: Query }\n" +
                "type Query { user(id: ID!): User! }\n" +
                "type User { id: ID!, name: String! }\n");

            File.WriteAllText(Path.Combine(operationsPath, "query.graphql"),
                "query GetUser($id: ID! = \"1\") {\n" +
                "  user(id: $id) {\n" +
                "    ... on User {\n" +
                "      ...UserCore\n" +
                "    }\n" +
                "  }\n" +
                "}\n" +
                "fragment UserCore on User {\n" +
                "  id\n" +
                "  name\n" +
                "}\n");

            FixtureConfiguration.Write(configPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"inlineDefaultVariables\": true\n" +
                "}\n");

            var warnings = DiagnosticsReporter.CollectWarnings(schemaPath, operationsPath, configPath);
            Assert.Contains(
                "Operation 'GetUser' contains fragment spreads; inline default variables are skipped.",
                warnings);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void CSharpNamingToInterfaceNameAndToEnumMemberNameCoverEdgeCases()
    {
        var namingType = typeof(SalepGenerator).Assembly.GetType("Salep.ClientGenerator.Utilities.CSharpNaming", throwOnError: true)!;
        var toInterfaceName = namingType.GetMethod("ToInterfaceName", BindingFlags.Public | BindingFlags.Static)!;
        var toEnumMemberName = namingType.GetMethod("ToEnumMemberName", BindingFlags.Public | BindingFlags.Static)!;

        var alreadyInterface = (string)toInterfaceName.Invoke(null, ["IResult"])!;
        var prefixedInterface = (string)toInterfaceName.Invoke(null, ["user-name"])!;
        var digitEnumMember = (string)toEnumMemberName.Invoke(null, ["1-value"])!;

        Assert.Equal("IResult", alreadyInterface);
        Assert.Equal("IUserName", prefixedInterface);
        Assert.Equal("_1Value", digitEnumMember);
    }

    [Fact]
    public void GraphQLGeneratorGeneratedTestsOmitUnusedVariablesInResponseTestCalls()
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "TestsVarProject");

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser($used: ID!, $unused: String) {\n" +
                "  user(id: $used) {\n" +
                "    id\n" +
                "    name\n" +
                "  }\n" +
                "}\n");

            FixtureConfiguration.Write(project.ConfigPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"omitUnusedVariables\": true,\n" +
                "  \"inlineDefaultVariables\": false,\n" +
                "  \"generateTests\": true,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));

            var responseTests = Path.Combine(project.TestsDirectory, "OperationsResponseTests.cs");
            var responseTestsSource = File.ReadAllText(responseTests);
            Assert.Contains("Used =", responseTestsSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Unused =", responseTestsSource, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GraphQLGeneratorOmitUnusedVariablesTracksVariablesInDirectivesListsAndObject()
    {
        var root = CreateTempDirectory();
        try
        {
            var projectDirectory = Path.Combine(root, "DirectiveVarsProject");
            var operationsDirectory = Path.Combine(projectDirectory, "graphql");
            var generatedDirectory = Path.Combine(projectDirectory, "Generated");
            var testsDirectory = Path.Combine(projectDirectory, "GeneratedTests");
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(operationsDirectory);

            File.WriteAllText(Path.Combine(projectDirectory, "schema.graphql"),
                "schema { query: Query }\n" +
                "directive @op(name: String!) on QUERY\n" +
                "directive @field(input: FilterInput!) on FIELD\n" +
                "directive @inline(flag: Boolean!) on INLINE_FRAGMENT\n" +
                "input MetaInput { note: String! }\n" +
                "input FilterInput { ids: [Int!]!, meta: MetaInput! }\n" +
                "type Query { user(id: ID!): User! }\n" +
                "type User { id: ID!, name: String! }\n");

            File.WriteAllText(Path.Combine(operationsDirectory, "query.graphql"),
                "query Complex(\n" +
                "  $opTag: String!,\n" +
                "  $id: ID!,\n" +
                "  $inList: Int!,\n" +
                "  $inObj: String!,\n" +
                "  $inlineFlag: Boolean!,\n" +
                "  $unused: String\n" +
                ") @op(name: $opTag) {\n" +
                "  user(id: $id) @field(input: { ids: [$inList], meta: { note: $inObj } }) {\n" +
                "    ... on User @inline(flag: $inlineFlag) {\n" +
                "      id\n" +
                "      name\n" +
                "    }\n" +
                "  }\n" +
                "}\n");

            var configPath = Path.Combine(projectDirectory, "salep.json");
            FixtureConfiguration.Write(configPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"omitUnusedVariables\": true,\n" +
                "  \"inlineDefaultVariables\": false,\n" +
                "  \"generateTests\": true,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(configPath, WorkingDirectory: projectDirectory));

            var responseTests = Path.Combine(testsDirectory, "OperationsResponseTests.cs");
            var responseTestsSource = File.ReadAllText(responseTests);
            Assert.Contains("OpTag =", responseTestsSource, StringComparison.Ordinal);
            Assert.Contains("Id =", responseTestsSource, StringComparison.Ordinal);
            Assert.Contains("InList =", responseTestsSource, StringComparison.Ordinal);
            Assert.Contains("InObj =", responseTestsSource, StringComparison.Ordinal);
            Assert.Contains("InlineFlag =", responseTestsSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Unused =", responseTestsSource, StringComparison.Ordinal);
            Assert.True(Directory.Exists(generatedDirectory));
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GraphQLGeneratorWhenOmitUnusedVariablesDisabledKeepsUnusedVariablesInResponseTests()
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "KeepUnusedProject");

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser($used: ID!, $unused: String) {\n" +
                "  user(id: $used) {\n" +
                "    id\n" +
                "    name\n" +
                "  }\n" +
                "}\n");

            FixtureConfiguration.Write(project.ConfigPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"omitUnusedVariables\": false,\n" +
                "  \"inlineDefaultVariables\": false,\n" +
                "  \"generateTests\": true,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));

            var responseTests = Path.Combine(project.TestsDirectory, "OperationsResponseTests.cs");
            var responseTestsSource = File.ReadAllText(responseTests);
            Assert.Contains("Used =", responseTestsSource, StringComparison.Ordinal);
            Assert.Contains("Unused =", responseTestsSource, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GraphQLGeneratorOmitUnusedVariablesWithNoVariablesEmitsParameterlessResponseCall()
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "NoVarsProject");

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser {\n" +
                "  user(id: \"1\") {\n" +
                "    id\n" +
                "    name\n" +
                "  }\n" +
                "}\n");

            FixtureConfiguration.Write(project.ConfigPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"omitUnusedVariables\": true,\n" +
                "  \"inlineDefaultVariables\": false,\n" +
                "  \"generateTests\": true,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));

            var responseTests = Path.Combine(project.TestsDirectory, "OperationsResponseTests.cs");
            var responseTestsSource = File.ReadAllText(responseTests);
            Assert.Contains("api => api.GetUserAsync(TestContext.Current.CancellationToken)", responseTestsSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new GetUserVariables", responseTestsSource, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void GraphQLGeneratorOmitUnusedVariablesWithFragmentSpreadTracksFragmentVariablesInResponseTests()
    {
        var root = CreateTempDirectory();
        try
        {
            var project = CreateProject(root, "FragmentSpreadPolicyProject");

            File.WriteAllText(Path.Combine(project.OperationsDirectory, "query.graphql"),
                "query GetUser($used: ID!, $fragmentFlag: Boolean!, $unused: String) {\n" +
                "  user(id: $used) {\n" +
                "    ...UserCore\n" +
                "  }\n" +
                "}\n" +
                "fragment UserCore on User {\n" +
                "  id @include(if: $fragmentFlag)\n" +
                "  name\n" +
                "}\n");

            FixtureConfiguration.Write(project.ConfigPath,
                "{\n" +
                "  \"schemaPath\": \"./schema.graphql\",\n" +
                "  \"operationsPath\": \"./graphql\",\n" +
                "  \"outputDirectory\": \"./Generated\",\n" +
                "  \"testsOutputDirectory\": \"./GeneratedTests\",\n" +
                "  \"omitUnusedVariables\": true,\n" +
                "  \"inlineDefaultVariables\": false,\n" +
                "  \"generateTests\": true,\n" +
                "  \"generateClient\": true\n" +
                "}\n");

            FixtureConfiguration.Generate(new(project.ConfigPath, WorkingDirectory: project.ProjectDirectory));

            var responseTests = Path.Combine(project.TestsDirectory, "OperationsResponseTests.cs");
            var responseTestsSource = File.ReadAllText(responseTests);
            Assert.Contains("Used =", responseTestsSource, StringComparison.Ordinal);
            Assert.Contains("FragmentFlag =", responseTestsSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Unused =", responseTestsSource, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void VariablePoliciesCollectUsedVariablesSelectionSetIncludesFragmentSpreadDirectiveVariables()
    {
        var document = Utf8GraphQLParser.Parse(
            "query GetUser($flag: Boolean!) {\n" +
            "  user(id: \"1\") {\n" +
            "    ...UserCore @include(if: $flag)\n" +
            "  }\n" +
            "}\n" +
            "fragment UserCore on User {\n" +
            "  id\n" +
            "}\n");

        var operation = document.Definitions.OfType<OperationDefinitionNode>().Single();
    var nestedSelectionSet = operation.SelectionSet.Selections.OfType<FieldNode>().Single().SelectionSet;
    Assert.NotNull(nestedSelectionSet);
        var used = new HashSet<string>(StringComparer.Ordinal);

        var testsEmitterType = typeof(SalepGenerator).Assembly.GetType("Salep.ClientGenerator.Emission.OperationVariablePolicies", throwOnError: true)!;
        var collectSelectionSet = testsEmitterType.GetMethod(
            "CollectUsedVariables",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(SelectionSetNode), typeof(HashSet<string>)],
            modifiers: null)!;

        collectSelectionSet.Invoke(null, [nestedSelectionSet, used]);
        Assert.Contains("flag", used);
    }

    private static (string ProjectDirectory, string ConfigPath, string GeneratedDirectory, string TestsDirectory, string OperationsDirectory) CreateProject(string root, string name)
    {
        var projectDirectory = Path.Combine(root, name);
        var operationsDirectory = Path.Combine(projectDirectory, "graphql");
        var generatedDirectory = Path.Combine(projectDirectory, "Generated");
        var testsDirectory = Path.Combine(projectDirectory, "GeneratedTests");

        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(operationsDirectory);

        File.WriteAllText(Path.Combine(projectDirectory, "schema.graphql"),
            "schema { query: Query }\n" +
            "scalar DateTime\n" +
            "type Query { user(id: ID!): User! }\n" +
            "type User { id: ID!, name: String!, createdAt: DateTime! }\n");

        var configPath = Path.Combine(projectDirectory, "salep.json");

        return (projectDirectory, configPath, generatedDirectory, testsDirectory, operationsDirectory);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "salep-coverage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
