using Scriban;
using Scriban.Runtime;
using Scriban.Parsing;
using Scriban.Syntax;
using System.Reflection;

namespace Salep.ClientGenerator.Templates;

/// <summary>Runtime limits and member access rules used while executing templates.</summary>
public sealed record ScribanTemplateEngineOptions
{
    /// <summary>Maximum cumulative loop iterations across a render.</summary>
    public int LoopLimit { get; init; } = 100_000;

    /// <summary>Maximum nested template evaluation depth.</summary>
    public int RecursiveLimit { get; init; } = 128;
}

/// <summary>Parses and executes Scriban source templates against explicit generation models.</summary>
public sealed class ScribanTemplateEngine
{
    private readonly ScribanTemplateEngineOptions options;

    /// <summary>Creates a Scriban engine with bounded evaluation defaults.</summary>
    public ScribanTemplateEngine(ScribanTemplateEngineOptions? options = null)
    {
        this.options = options ?? new ScribanTemplateEngineOptions();
        if (this.options.LoopLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The template loop limit must be positive.");
        }

        if (this.options.RecursiveLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The template recursion limit must be positive.");
        }
    }

    /// <summary>Parses and renders the supplied template using an explicit model.</summary>
    public string Render(string source, object model, string sourceName = "<template>",
        IReadOnlyDictionary<string, string>? templateOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var template = Template.Parse(source, sourceName);
        if (template.HasErrors)
        {
            var diagnostics = string.Join(Environment.NewLine, template.Messages.Select(message => message.ToString()));
            throw new InvalidOperationException($"Scriban template '{sourceName}' could not be parsed:{Environment.NewLine}{diagnostics}");
        }

        var global = model as ScriptObject ?? ScriptObject.From(model);
        var context = new TemplateContext
        {
            StrictVariables = true,
            EnableRelaxedMemberAccess = false,
            EnableRelaxedTargetAccess = false,
            MemberRenamer = StandardMemberRenamer.Default,
            MemberFilter = member => member.MemberType is MemberTypes.Field or MemberTypes.Property,
            LoopLimit = options.LoopLimit,
            RecursiveLimit = options.RecursiveLimit,
            TemplateLoader = new NamedTemplateLoader(templateOverrides)
        };
        context.PushGlobal(global);
        return template.Render(context);
    }

    private sealed class NamedTemplateLoader(IReadOnlyDictionary<string, string>? overrides) : ITemplateLoader
    {
        public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
        {
            var key = templateName.StartsWith("default:", StringComparison.Ordinal) ? templateName[8..] : templateName;
            if (!ScribanTemplateNames.All.Contains(key))
                throw new ScriptRuntimeException(callerSpan, $"Unknown Salep template include '{templateName}'. Use a stable template key, not a filesystem path.");
            return templateName;
        }

        public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            if (templatePath.StartsWith("default:", StringComparison.Ordinal))
                return ScribanTemplateCatalog.ReadDefault(templatePath[8..]);
            return overrides is not null && overrides.TryGetValue(templatePath, out var source)
                ? source ?? throw new ScriptRuntimeException(callerSpan, $"Override for '{templatePath}' cannot be null.")
                : ScribanTemplateCatalog.ReadDefault(templatePath);
        }

        public ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
            => ValueTask.FromResult<string?>(Load(context, callerSpan, templatePath));
    }

}
