using Salep.GraphQLParser;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Operations;

namespace Salep.ClientGenerator.Diagnostics;

public static class DiagnosticsReporter
{
    private static readonly HashSet<string> BuiltInScalars = new(StringComparer.Ordinal)
    {
        "String",
        "ID",
        "Int",
        "Float",
        "Boolean",
        "Long",
        "Decimal",
        "DateTime",
        "Instant",
        "UUID",
        "URL"
    };

    public static IReadOnlyList<string> CollectWarnings(string schemaPath, string operationsPath, string configPath)
    {
        var schemaText = File.ReadAllText(schemaPath);
        var document = Utf8GraphQLParser.Parse(schemaText);
        var config = GeneratorConfig.Load(configPath);
        var schema = SchemaModel.FromDocument(document, config);
        var operations = Directory.Exists(operationsPath)
            ? OperationLoader.Load(operationsPath).Operations
            : [];

        return CollectWarnings(schema, operations);
    }

    internal static IReadOnlyList<string> CollectWarnings(SchemaModel schema, IReadOnlyList<OperationDefinitionNode> operations)
    {
        var warnings = new List<string>();

        AddMissingScalarWarnings(schema, operations, warnings);
        AddSchemaExtensionWarnings(schema, warnings);
        AddOperationWarnings(schema, operations, warnings);

        warnings.Sort(StringComparer.Ordinal);
        return warnings;
    }

    private static void AddMissingScalarWarnings(
        SchemaModel schema,
        IReadOnlyList<OperationDefinitionNode> operations,
        List<string> warnings)
    {
        // Scalar declarations and directive-definition arguments alone do not produce C# members.
        var referencedTypes = new HashSet<string>(StringComparer.Ordinal);
        var fields = schema.ObjectTypes.Values.SelectMany(type => type.Fields)
            .Concat(schema.InterfaceTypes.Values.SelectMany(type => type.Fields));
        foreach (var field in fields)
        {
            referencedTypes.Add(SyntaxNodeExtensions.NamedType(field.Type).Name.Value);
            foreach (var argument in field.Arguments)
            {
                referencedTypes.Add(SyntaxNodeExtensions.NamedType(argument.Type).Name.Value);
            }
        }

        foreach (var field in schema.InputTypes.Values.SelectMany(type => type.Fields))
        {
            referencedTypes.Add(SyntaxNodeExtensions.NamedType(field.Type).Name.Value);
        }

        foreach (var variable in operations.SelectMany(operation => operation.VariableDefinitions))
        {
            referencedTypes.Add(SyntaxNodeExtensions.NamedType(variable.Type).Name.Value);
        }

        foreach (var scalar in schema.ScalarTypes.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!referencedTypes.Contains(scalar) || BuiltInScalars.Contains(scalar))
            {
                continue;
            }

            if (schema.Config.TryGetScalarMapping(scalar, out _, out _))
            {
                continue;
            }

            warnings.Add($"Scalar '{scalar}' has no mapping. It will be generated as string.");
        }
    }

    private static void AddSchemaExtensionWarnings(SchemaModel schema, List<string> warnings)
    {
        foreach (var definition in schema.Document.Definitions)
        {
            if (!IsSchemaExtension(definition)) continue;
            
            warnings.Add("Schema extensions are ignored by the generator.");
            return;
        }
    }

    private static bool IsSchemaExtension(IDefinitionNode definition)
        => definition is SchemaExtensionNode
            or ScalarTypeExtensionNode
            or ObjectTypeExtensionNode
            or InterfaceTypeExtensionNode
            or UnionTypeExtensionNode
            or EnumTypeExtensionNode
            or InputObjectTypeExtensionNode;

    private static void AddOperationWarnings(
        SchemaModel schema,
        IReadOnlyList<OperationDefinitionNode> operations,
        List<string> warnings)
    {
        if (operations.Count == 0)
        {
            warnings.Add("No operations found. Generated client will include only types.");
            return;
        }

        if (!schema.Config.InlineDefaultVariables)
        {
            return;
        }

        foreach (var operation in operations)
        {
            if (!HasFragmentSpreads(operation.SelectionSet)) continue;
            
            var name = operation.Name?.Value ?? "(unnamed)";
            warnings.Add($"Operation '{name}' contains fragment spreads; inline default variables are skipped.");
        }
    }

    private static bool HasFragmentSpreads(SelectionSetNode selectionSet)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FragmentSpreadNode:
                    return true;
                case FieldNode { SelectionSet: not null } field:
                    if (HasFragmentSpreads(field.SelectionSet))
                    {
                        return true;
                    }
                    break;
                case InlineFragmentNode fragment:
                    if (HasFragmentSpreads(fragment.SelectionSet))
                    {
                        return true;
                    }
                    break;
            }
        }

        return false;
    }
}
