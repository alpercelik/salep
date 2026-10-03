using System.Reflection;
using Scriban.Runtime;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Targets;

namespace Salep.ClientGenerator.Templates;

/// <summary>Builds Scriban data objects for schema, operation, and target services.</summary>
public static class ScribanTemplateModelFactory
{
    /// <summary>Creates an explicit Scriban model and callable target service helpers.</summary>
    public static ScriptObject Create(
        GraphQlSchemaModel schema,
        ICodeGenerationTarget target,
        GraphQlExecutableDocument? executable = null,
        IReadOnlyDictionary<string, object?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(target);

        var root = new ScriptObject();
        root.Add("schema", ImportProperties(schema));
        root.Add("operations", ToScriptArray(executable?.Operations ?? []));
        root.Add("fragments", ToScriptArray(executable?.Fragments ?? []));
        root.Add("target", CreateTargetModel(schema, target));
        if (settings is not null)
        {
            root.Add("settings", ImportDictionary(settings));
        }

        return root;
    }

    /// <summary>Imports only public fields and properties, excluding reflected .NET methods.</summary>
    public static ScriptObject ImportProperties(object model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var scriptObject = new ScriptObject();
        scriptObject.Import(model, ScriptMemberImportFlags.Field | ScriptMemberImportFlags.Property,
            renamer: StandardMemberRenamer.Default);
        return scriptObject;
    }

    private static ScriptObject CreateTargetModel(GraphQlSchemaModel schema, ICodeGenerationTarget target)
    {
        var scriptObject = new ScriptObject();
        scriptObject.Add("id", target.Id);
        scriptObject.Add("default_namespace", target.DefaultNamespace);
        scriptObject.Add("imports", new ScriptArray(target.GetImports(schema)));
        scriptObject.Add("type_name", Function(new Func<string, string>(target.TypeName)));
        scriptObject.Add("interface_name", Function(new Func<string, string>(target.InterfaceName)));
        scriptObject.Add("property_name", Function(new Func<string, string>(target.PropertyName)));
        scriptObject.Add("enum_value_name", Function(new Func<string, string>(target.EnumValueName)));
        scriptObject.Add("method_name", Function(new Func<string, string>(target.MethodName)));
        scriptObject.Add("parameter_name", Function(new Func<string, string>(target.ParameterName)));
        scriptObject.Add("string_literal", Function(new Func<string, string>(target.StringLiteral)));
        scriptObject.Add("type", Function(new Func<GraphQlTypeReference, string>(type => target.RenderType(type, schema))));
        scriptObject.Add("is_value_type", Function(new Func<GraphQlTypeReference, bool>(type => target.IsValueType(type, schema))));
        return scriptObject;
    }

    private static DynamicCustomFunction Function(Delegate function) => DynamicCustomFunction.Create(function);

    private static ScriptArray ToScriptArray<T>(IEnumerable<T> values)
    {
        var array = new ScriptArray();
        foreach (var value in values)
        {
            array.Add(value is null ? null : ImportProperties(value));
        }

        return array;
    }

    /// <summary>Creates a Scriban object from explicit template settings.</summary>
    public static ScriptObject ImportDictionary(IReadOnlyDictionary<string, object?> values)
    {
        var scriptObject = new ScriptObject(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            scriptObject.Add(key, value);
        }

        return scriptObject;
    }
}
