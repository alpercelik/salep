using Scriban;
using Scriban.Runtime;
using Scriban.Parsing;

namespace Salep.ClientGenerator.TypeScript.Templates;

/// <summary>Composable embedded TypeScript templates, independently owned by the TypeScript generator.</summary>
public sealed class TypeScriptTemplateCatalog(IReadOnlyDictionary<string, string>? overrides = null)
{
    public static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["local-execution"] = "LocalExecution.scriban-ts",
        ["resolvers"] = "Resolvers.scriban-ts",
        ["partition.operation"] = "PartitionOperation.scriban-ts",
        ["partition.barrel"] = "PartitionBarrel.scriban-ts",
        ["documentation"] = "Documentation.scriban-ts",
        ["graphql"] = "GraphQL.scriban-ts", ["schema"] = "Schema.scriban-ts",
        ["schema.scalars"] = "Scalars.scriban-ts", ["schema.enum"] = "Enum.scriban-ts", ["schema.input"] = "Input.scriban-ts", ["schema.input-oneof"] = "InputOneOf.scriban-ts",
        ["property"] = "Property.scriban-ts", ["operations"] = "Operations.scriban-ts",
        ["operations.operation"] = "Operation.scriban-ts", ["operations.variables"] = "Variables.scriban-ts",
        ["operations.response"] = "Response.scriban-ts", ["operations.response-object"] = "ResponseObject.scriban-ts", ["operations.document"] = "Document.scriban-ts",
        ["client"] = "Client.scriban-ts", ["lookup"] = "Lookup.scriban-ts",
        ["fragment-masking"] = "FragmentMasking.scriban-ts", ["index"] = "Index.scriban-ts"
    };

    public static string ReadDefault(string key)
    {
        if (!Files.TryGetValue(key, out var file)) throw new InvalidDataException($"Unknown TypeScript template '{key}'.");
        var assembly = typeof(TypeScriptTemplateCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".Templates." + file, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public string Render(string key, object model)
    {
        var template = Template.Parse(overrides is not null && overrides.TryGetValue(key, out var custom) ? custom : ReadDefault(key));
        if (template.HasErrors) throw new InvalidDataException(string.Join(Environment.NewLine, template.Messages));
        var values = new ScriptObject();
        values.Import(model, renamer: member => member.Name);
        var context = new TemplateContext { StrictVariables = true, TemplateLoader = new CatalogLoader(overrides) };
        context.PushGlobal(values);
        return template.Render(context);
    }
    private sealed class CatalogLoader(IReadOnlyDictionary<string, string>? overrides) : ITemplateLoader
    {
        public string GetPath(TemplateContext context, SourceSpan span, string name)
        {
            var key = name.StartsWith("default:", StringComparison.Ordinal) ? name[8..] : name;
            if (!Files.ContainsKey(key)) throw new InvalidDataException("Includes must name a registered TypeScript template key.");
            return name;
        }
        public string Load(TemplateContext context, SourceSpan span, string name)
            => name.StartsWith("default:", StringComparison.Ordinal) ? ReadDefault(name[8..])
                : overrides is not null && overrides.TryGetValue(name, out var source) ? source : ReadDefault(name);
        public ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan span, string name) => ValueTask.FromResult<string?>(Load(context, span, name));
    }
}
