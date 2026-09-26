using System.Text.Json;
using System.Text.Json.Nodes;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Generation;

namespace Salep.ClientGenerator.Tests;

// Test scaffolding for the emission matrix: writes separate v1 client/test configs.
// Matrix controls are not accepted by the production configuration parser.
internal static class FixtureConfiguration
{
    public static void Write(string path, string specification)
    {
        var source = JsonNode.Parse(specification)!.AsObject();
        var client = new JsonObject { ["version"] = 1, ["kind"] = "client", ["schema"] = "./schema.graphql" };
        var translations = new Dictionary<string, string>
        {
            ["schemaPath"] = "schema", ["operationsPath"] = "operations", ["outputDirectory"] = "output",
            ["generatedNamespace"] = "namespace", ["clientClassName"] = "clientName", ["generateSample"] = "emitSample"
        };
        var controls = new HashSet<string> { "generateTests", "generateClient", "testsOutputDirectory", "testsNamespace", "useRawStrings", "scalarMappings", "scalarValueTypes", "scalarSampleExpressions", "scalarSampleJsonLiterals", "useNodaTime", "useNativeUnions" };
        foreach (var entry in source)
            if (!controls.Contains(entry.Key)) client[translations.GetValueOrDefault(entry.Key, entry.Key)] = entry.Value?.DeepClone();
        if (source["useNodaTime"] is { } noda) client["scalarPreset"] = noda.GetValue<bool>() ? "nodatime" : "builtin";
        if (source["useNativeUnions"] is { } native) client["unionRepresentation"] = native.GetValue<bool>() ? "native" : "dunet";
        var scalars = new JsonObject();
        var names = new[] { "scalarMappings", "scalarValueTypes", "scalarSampleExpressions", "scalarSampleJsonLiterals" }
            .SelectMany(key => source[key]?.AsObject().Select(pair => pair.Key) ?? []).Distinct(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var type = source["scalarMappings"]?[name]?.GetValue<string>() ?? GeneratorConfig.BuiltinScalars.GetValueOrDefault(name)?.Type ?? "string";
            var defaults = GeneratorConfig.BuiltinScalars.Values.FirstOrDefault(value => value.Type == type);
            scalars[name] = new JsonObject
            {
                ["type"] = type,
                ["isValueType"] = source["scalarValueTypes"]?[name]?.GetValue<bool>() ?? defaults?.IsValueType ?? type == "NodaTime.Instant",
                ["sampleExpression"] = source["scalarSampleExpressions"]?[name]?.GetValue<string>() ?? defaults?.SampleExpression ?? (type == "NodaTime.Instant" ? "NodaTime.Instant.FromUtc(2020, 1, 1, 0, 0)" : "default!"),
                ["sampleJson"] = source["scalarSampleJsonLiterals"]?[name]?.GetValue<string>() ?? defaults?.SampleJson ?? "null"
            };
        }
        if (scalars.Count > 0) client["scalars"] = scalars;
        File.WriteAllText(path, client.ToJsonString());
        var testPath = path + ".tests.json";
        if (source["generateTests"]?.GetValue<bool>() ?? true)
        {
            var test = new JsonObject
            {
                ["version"] = 1, ["kind"] = "tests", ["client"] = Path.GetFullPath(path),
                ["output"] = source["testsOutputDirectory"]?.DeepClone() ?? JsonValue.Create("./GeneratedTests"),
                ["namespace"] = source["testsNamespace"]?.DeepClone() ?? JsonValue.Create("GeneratedClient.Tests"),
                ["rawJsonLiterals"] = source["useRawStrings"]?.DeepClone() ?? JsonValue.Create(true),
                ["emitAgentInstructions"] = source["emitAgentInstructions"]?.DeepClone() ?? JsonValue.Create(true)
            };
            File.WriteAllText(testPath, test.ToJsonString());
        }
        else if (File.Exists(testPath)) File.Delete(testPath);
    }

    public static SalepGeneratorResult Generate(SalepGeneratorOptions options)
    {
        var result = SalepGenerator.Generate(options);
        var test = options.ConfigPath + ".tests.json";
        if (!File.Exists(test)) return result;
        var tests = SalepGenerator.Generate(new(test, options.WorkingDirectory));
        return new(result.GeneratedFiles.Concat(tests.GeneratedFiles).ToArray(), result.Warnings, result.Logs.Concat(tests.Logs).ToArray());
    }
}
