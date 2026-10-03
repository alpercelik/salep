using Scriban.Runtime;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Templates;

namespace Salep.ClientGenerator.Targets;

/// <summary>Projects GraphQL operation response shapes into C#-specific Scriban views.</summary>
public static class CSharpOperationTemplateModelFactory
{
    /// <summary>Creates operation, variable, and response type views for C# templates.</summary>
    public static ScriptObject Create(
        GraphQlSchemaModel schema,
        GraphQlExecutableDocument executable,
        CSharpCodeGenerationTarget target,
        GraphQlOperationDocumentOptions? documentOptions = null,
        string operationInterfaceTypeName = "IGraphQLOperation",
        string clientClassName = "GraphQLClient")
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationInterfaceTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientClassName);

        var root = ScribanTemplateModelFactory.Create(schema, target, executable);
        root.Add("operation_interface", operationInterfaceTypeName);
        var operationsBaseName = clientClassName.EndsWith("Client", StringComparison.Ordinal)
            ? clientClassName[..^"Client".Length]
            : clientClassName;
        root.Add("operations_interface", "I" + operationsBaseName + "Operations");
        root.Add("operations_class", operationsBaseName + "Operations");
        root.Add("client_class", clientClassName);
        var operations = new ScriptArray();
        var formatter = new GraphQlOperationDocumentFormatter(schema, executable);
        foreach (var operation in executable.Operations.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var normalizedOperation = formatter.NormalizeVariables(operation, documentOptions);
            var operationName = operation.Name!;
            var retainedVariableNames = normalizedOperation.Variables.Select(variable => variable.Name).ToHashSet(StringComparer.Ordinal);
            var removedVariableNames = operation.Variables
                .Where(variable => !retainedVariableNames.Contains(variable.Name))
                .Select(variable => variable.Name)
                .ToArray();
            var responseName = target.TypeName(operationName) + "Response";
            var variablesName = target.TypeName(operationName) + "Variables";
            var typeViews = new List<ScriptObject>();
            var usedTypeNames = new HashSet<string>(StringComparer.Ordinal);
            var nextTypeId = 0;
            var projection = operation.ResponseProjection ?? new GraphQlResponseProjector(schema, executable).Project(operation);
            AddProjection(projection, responseName, null, typeViews, usedTypeNames, schema, target,
                target.TypeName(operationName) + "ResponseSelection", ref nextTypeId);

            var variables = new ScriptArray(normalizedOperation.Variables.Select(variable => VariableView(variable, schema, target)));
            var view = new ScriptObject();
            view.Add("name", operationName);
            view.Add("test_name", target.TypeName(operationName));
            view.Add("operation_type", operation.OperationType);
            view.Add("type_name", target.TypeName(operationName) + "Operation");
            view.Add("method_name", target.MethodName(operationName) + "Async");
            view.Add("response_type", responseName);
            view.Add("variables_type", variablesName);
            var query = formatter.Format(operation, documentOptions);
            view.Add("query", query);
            view.Add("query_lines", new ScriptArray(query.ReplaceLineEndings("\n").Split('\n')));
            var longestQuoteRun = 0;
            var quoteRun = 0;
            foreach (var character in query)
            {
                quoteRun = character == '"' ? quoteRun + 1 : 0;
                longestQuoteRun = Math.Max(longestQuoteRun, quoteRun);
            }
            var delimiter = new string('"', Math.Max(3, longestQuoteRun + 1));
            view.Add("query_delimiter", delimiter);
            view.Add("query_literal", "        " + delimiter + Environment.NewLine
                + string.Join(Environment.NewLine, query.ReplaceLineEndings("\n").Split('\n')
                    .Select(line => line.Length == 0 ? "" : "        " + line))
                + Environment.NewLine + "        " + delimiter);
            view.Add("variables", variables);
            view.Add("removed_variables", new ScriptArray(removedVariableNames));
            view.Add("response_types", new ScriptArray(typeViews));
            operations.Add(view);
        }

        root.Add("csharp_operations", operations);
        return root;
    }

    private static ScriptObject VariableView(GraphQlVariableDefinition variable, GraphQlSchemaModel schema, CSharpCodeGenerationTarget target)
    {
        var view = new ScriptObject();
        view.Add("name", variable.Name);
        view.Add("property_name", target.PropertyName(variable.Name));
        view.Add("type", target.RenderType(variable.Type, schema));
        view.Add("is_non_null_reference", variable.Type.IsNonNull && !target.IsValueType(variable.Type, schema));
        return view;
    }

    private static void AddProjection(
        GraphQlResponseProjection projection,
        string typeName,
        string? baseType,
        List<ScriptObject> output,
        HashSet<string> usedNames,
        GraphQlSchemaModel schema,
        CSharpCodeGenerationTarget target,
        string nestedNamePrefix,
        ref int nextTypeId)
    {
        if (!usedNames.Add(typeName)) return;
        var fields = new ScriptArray();
        foreach (var field in projection.Fields)
        {
            var fieldTypeName = TypeName(field.Type);
            var isAbstract = fieldTypeName is not null &&
                (schema.InterfaceTypes.Any(type => type.Name == fieldTypeName) || schema.UnionTypes.Any(type => type.Name == fieldTypeName));
            var requiresProjection = field.Variants.Count > 0 && field.Variants.Any(ProjectionHasAlias);
            var projectedTypeName = fieldTypeName is null || !requiresProjection
                ? null
                : nestedNamePrefix + (++nextTypeId).ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (projectedTypeName is not null)
            {
                if (isAbstract)
                {
                    AddProjection(new GraphQlResponseProjection(fieldTypeName!, []), projectedTypeName, null,
                        output, usedNames, schema, target, nestedNamePrefix, ref nextTypeId);
                    var parent = output.LastOrDefault(item => item.GetSafeValue<string>("name") == projectedTypeName);
                    if (parent is not null) parent["kind"] = "abstract";
                    foreach (var variant in field.Variants)
                    {
                        var variantName = projectedTypeName + target.TypeName(variant.GraphQlTypeName);
                        AddProjection(variant, variantName, projectedTypeName,
                            output, usedNames, schema, target, nestedNamePrefix, ref nextTypeId);
                        var variantView = output.LastOrDefault(item => item.GetSafeValue<string>("name") == variantName);
                        if (variantView is not null) variantView["discriminator"] = variant.GraphQlTypeName;
                    }
                }
                else
                {
                    AddProjection(field.Variants[0], projectedTypeName, null, output, usedNames, schema, target,
                        nestedNamePrefix, ref nextTypeId);
                }
            }

            var fieldView = new ScriptObject();
            fieldView.Add("name", field.ResponseName);
            fieldView.Add("property_name", target.PropertyName(field.ResponseName));
            fieldView.Add("json_name", target.StringLiteral(field.ResponseName));
            fieldView.Add("type", field.Type is null ? "object?" : RenderProjectionType(field.Type, fieldTypeName, projectedTypeName, target, schema));
            fields.Add(fieldView);
        }

        var type = new ScriptObject();
        type.Add("name", typeName);
        type.Add("base_type", baseType);
        type.Add("kind", baseType is null ? "object" : "variant");
        type.Add("discriminator", null);
        type.Add("fields", fields);
        output.Add(type);
    }

    private static string RenderProjectionType(
        GraphQlTypeReference type,
        string? graphQlTypeName,
        string? projectedTypeName,
        CSharpCodeGenerationTarget target,
        GraphQlSchemaModel schema)
    {
        if (graphQlTypeName is null || projectedTypeName is null) return target.RenderType(type, schema);
        return target.RenderType(ReplaceNamed(type, graphQlTypeName, projectedTypeName), schema);
    }

    private static GraphQlTypeReference ReplaceNamed(GraphQlTypeReference type, string oldName, string newName) => type switch
    {
        GraphQlNamedTypeReference named when StringComparer.Ordinal.Equals(named.Name, oldName) => named with { Name = newName },
        GraphQlListTypeReference list => list with { ElementType = ReplaceNamed(list.ElementType, oldName, newName) },
        GraphQlNonNullTypeReference nonNull => nonNull with { NullableType = ReplaceNamed(nonNull.NullableType, oldName, newName) },
        _ => type
    };

    private static string? TypeName(GraphQlTypeReference? type) => type switch
    {
        GraphQlNamedTypeReference named => named.Name,
        GraphQlListTypeReference list => TypeName(list.ElementType),
        GraphQlNonNullTypeReference nonNull => TypeName(nonNull.NullableType),
        _ => null
    };

    private static bool ProjectionHasAlias(GraphQlResponseProjection projection)
        => projection.Fields.Any(field => !StringComparer.Ordinal.Equals(field.ResponseName, field.GraphQlFieldName)
            || field.Variants.Any(ProjectionHasAlias));
}
