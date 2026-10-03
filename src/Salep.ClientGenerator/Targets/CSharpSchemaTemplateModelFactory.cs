using Scriban.Runtime;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Templates;

namespace Salep.ClientGenerator.Targets;

/// <summary>Builds C#-specific template projections without adding C# data to GraphQL models.</summary>
public static class CSharpSchemaTemplateModelFactory
{
    /// <summary>Creates a model containing merged SDL definitions and C# policy settings.</summary>
    public static ScriptObject Create(
        GraphQlSchemaModel schema,
        CSharpCodeGenerationTarget target,
        GraphQlExecutableDocument? executable = null,
        IReadOnlySet<string>? includedTypeNames = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(target);

        var schemaTypes = BuildSchemaTypeViews(schema);
        if (includedTypeNames is not null)
        {
            for (var index = schemaTypes.Count - 1; index >= 0; index--)
            {
                if (schemaTypes[index] is ScriptObject type && !includedTypeNames.Contains(type.GetSafeValue<string>("name") ?? string.Empty))
                {
                    schemaTypes.RemoveAt(index);
                }
            }
        }
        var model = ScribanTemplateModelFactory.Create(schema, target, executable);
        foreach (var type in schemaTypes.OfType<ScriptObject>())
        {
            var interfaces = ((ScriptArray)type["interfaces"]!).Cast<string>();
            type.Add("csharp_interfaces", string.Join(", ", interfaces.Select(name => $"global::{target.InterfaceOwner(name)}.{target.InterfaceName(name)}")));
            var members = ((ScriptArray)type["members"]!).Cast<string>();
            type.Add("csharp_union_members", string.Join(", ", members.Select(target.TypeName)));
            var unionName = target.TypeName(type.GetSafeValue<string>("name") ?? string.Empty);
            var isInterfaceResult = type.GetSafeValue<string>("kind") == "interface_result";
            type.Add("csharp_union_name", isInterfaceResult ? $"{unionName}Result" : unionName);
            type.Add("csharp_union_description", isInterfaceResult ? "interface" : "union");
        }
        model.Add("schema_types", schemaTypes);
        if (!target.UseNativeUnions && schemaTypes.OfType<ScriptObject>().Any(type => type.GetSafeValue<string>("kind") is "union" or "interface_result"))
        {
            var targetModel = (ScriptObject)model["target"]!;
            var imports = (ScriptArray)targetModel["imports"]!;
            if (!imports.Contains("Dunet")) imports.Add("Dunet");
        }
        var csharp = new ScriptObject();
        csharp.Add("use_native_unions", target.UseNativeUnions);
        csharp.Add("type_owner", DynamicCustomFunction.Create(new Func<string, string>(target.TypeOwner)));
        csharp.Add("interface_owner", DynamicCustomFunction.Create(new Func<string, string>(target.InterfaceOwner)));
        model.Add("csharp", csharp);
        return model;
    }

    private static ScriptArray BuildSchemaTypeViews(GraphQlSchemaModel schema)
    {
        var results = new ScriptArray();
        var seen = new HashSet<(Type Kind, string Name)>();
        foreach (var type in schema.Types.Where(type => !type.IsExtension))
        {
            if (!seen.Add((type.GetType(), type.Name)))
            {
                continue;
            }

            var matching = schema.Types.Where(candidate => candidate.GetType() == type.GetType()
                && StringComparer.Ordinal.Equals(candidate.Name, type.Name)).ToArray();
            results.Add(type switch
            {
                GraphQlObjectType obj => ObjectView(obj, matching.OfType<GraphQlObjectType>().ToArray()),
                GraphQlInputObjectType input => InputView(input, matching.OfType<GraphQlInputObjectType>().ToArray()),
                GraphQlInterfaceType contract => InterfaceView(contract, matching.OfType<GraphQlInterfaceType>().ToArray(), schema),
                GraphQlUnionType union => UnionView(union, matching.OfType<GraphQlUnionType>().ToArray()),
                GraphQlEnumType enumeration => EnumView(enumeration, matching.OfType<GraphQlEnumType>().ToArray()),
                GraphQlScalarType scalar => ScalarView(scalar),
                _ => throw new NotSupportedException($"Unsupported GraphQL schema type '{type.GetType().Name}'.")
            });
        }

        foreach (var contract in schema.InterfaceTypes)
        {
            var implementations = schema.ObjectTypes.Where(type => ImplementsInterface(schema, type.Name, contract.Name))
                .Select(type => type.Name).Distinct(StringComparer.Ordinal).ToArray();
            if (implementations.Length == 0)
            {
                continue;
            }

            var result = NewView("interface_result", contract.Name, null);
            result["members"] = new ScriptArray(implementations);
            results.Add(result);
        }

        return results;
    }

    private static ScriptObject ObjectView(GraphQlObjectType definition, IReadOnlyList<GraphQlObjectType> parts)
    {
        var result = NewView("object", definition.Name, definition.Description);
        result["fields"] = Fields(parts.SelectMany(part => part.Fields), inherited: new HashSet<string>(StringComparer.Ordinal));
        result["interfaces"] = new ScriptArray(parts.SelectMany(part => part.Interfaces).Distinct(StringComparer.Ordinal));
        return result;
    }

    private static ScriptObject InputView(GraphQlInputObjectType definition, IReadOnlyList<GraphQlInputObjectType> parts)
    {
        var result = NewView("input", definition.Name, definition.Description);
        result["fields"] = new ScriptArray(parts.SelectMany(part => part.Fields).Select(ScribanTemplateModelFactory.ImportProperties));
        return result;
    }

    private static ScriptObject InterfaceView(
        GraphQlInterfaceType definition,
        IReadOnlyList<GraphQlInterfaceType> parts,
        GraphQlSchemaModel schema)
    {
        var result = NewView("interface", definition.Name, definition.Description);
        var inherited = InheritedFields(schema, parts.SelectMany(part => part.Interfaces));
        result["fields"] = Fields(parts.SelectMany(part => part.Fields), inherited);
        result["interfaces"] = new ScriptArray(parts.SelectMany(part => part.Interfaces).Distinct(StringComparer.Ordinal));
        return result;
    }

    private static ScriptObject UnionView(GraphQlUnionType definition, IReadOnlyList<GraphQlUnionType> parts)
    {
        var result = NewView("union", definition.Name, definition.Description);
        result["members"] = new ScriptArray(parts.SelectMany(part => part.Members).Distinct(StringComparer.Ordinal));
        return result;
    }

    private static ScriptObject EnumView(GraphQlEnumType definition, IReadOnlyList<GraphQlEnumType> parts)
    {
        var result = NewView("enum", definition.Name, definition.Description);
        result["values"] = new ScriptArray(parts.SelectMany(part => part.Values).Select(ScribanTemplateModelFactory.ImportProperties));
        return result;
    }

    private static ScriptObject ScalarView(GraphQlScalarType definition) => NewView("scalar", definition.Name, definition.Description);

    private static ScriptArray Fields(IEnumerable<GraphQlFieldDefinition> fields, IReadOnlySet<string> inherited)
    {
        var results = new ScriptArray();
        foreach (var field in fields)
        {
            var model = ScribanTemplateModelFactory.ImportProperties(field);
            model.Add("is_inherited", inherited.Contains(field.Name));
            results.Add(model);
        }

        return results;
    }

    private static ScriptObject NewView(string kind, string name, string? description)
    {
        var result = new ScriptObject();
        result.Add("kind", kind);
        result.Add("name", name);
        result.Add("description", description);
        result.Add("fields", new ScriptArray());
        result.Add("interfaces", new ScriptArray());
        result.Add("members", new ScriptArray());
        result.Add("values", new ScriptArray());
        return result;
    }

    private static HashSet<string> InheritedFields(GraphQlSchemaModel schema, IEnumerable<string> roots)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots) Collect(root, new HashSet<string>(StringComparer.Ordinal));
        return fields;

        void Collect(string name, HashSet<string> visited)
        {
            if (!visited.Add(name)) return;
            var contracts = schema.Types.OfType<GraphQlInterfaceType>().Where(type => StringComparer.Ordinal.Equals(type.Name, name)).ToArray();
            foreach (var field in contracts.SelectMany(contract => contract.Fields)) fields.Add(field.Name);
            foreach (var parent in contracts.SelectMany(contract => contract.Interfaces)) Collect(parent, visited);
        }
    }

    private static bool ImplementsInterface(GraphQlSchemaModel schema, string objectName, string targetInterface, HashSet<string>? path = null)
    {
        path ??= new HashSet<string>(StringComparer.Ordinal);
        if (!path.Add(objectName)) return false;
        try
        {
            foreach (var item in schema.Types.OfType<GraphQlObjectType>().Where(type => StringComparer.Ordinal.Equals(type.Name, objectName)))
            {
                foreach (var implemented in item.Interfaces)
                {
                    if (StringComparer.Ordinal.Equals(implemented, targetInterface) || ImplementsInterface(schema, implemented, targetInterface, path)) return true;
                }
            }

            foreach (var item in schema.Types.OfType<GraphQlInterfaceType>().Where(type => StringComparer.Ordinal.Equals(type.Name, objectName)))
            {
                foreach (var parent in item.Interfaces)
                {
                    if (StringComparer.Ordinal.Equals(parent, targetInterface) || ImplementsInterface(schema, parent, targetInterface, path)) return true;
                }
            }

            return false;
        }
        finally
        {
            path.Remove(objectName);
        }
    }
}
