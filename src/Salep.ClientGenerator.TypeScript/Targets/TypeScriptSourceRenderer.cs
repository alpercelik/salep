using Salep.ClientGenerator.TypeScript.Templates;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.TypeScript.Targets;

/// <summary>Projects neutral schema and selected operation shapes into TypeScript declarations.</summary>
public sealed class TypeScriptSourceRenderer(GraphQlSchemaModel schema, GraphQlExecutableDocument document, TypeScriptCodeGenerationTarget target, TypeScriptTemplateCatalog templates, bool fragmentMasking = true, bool persistedOperations = false, bool clientExecution = false)
{
    /// <summary>Renders typed resolver contracts for fields declared by local schema sources.</summary>
    public string ResolverTypes(IReadOnlyList<GraphQlSchemaType> localTypes)
    {
        target.PrepareNames(schema, document);
        var semantics = new GraphQlSchemaSemantics(schema);
        string Result(GraphQlTypeReference type)
        {
            var name = GraphQlSchemaSemantics.NamedType(type);
            var composite = semantics.Type(name) is GraphQlObjectType or GraphQlInterfaceType or GraphQlUnionType;
            var selected = composite ? "(" + string.Join(" | ", semantics.PossibleTypes(name).Select(item => "ResolverParentTypes[" + target.StringLiteral(item) + "]")) + ")" : null;
            return target.Render(type, schema, false, selected);
        }
        var parents = schema.Types.Where(type => type is GraphQlObjectType or GraphQlInterfaceType).DistinctBy(type => type.Name).Select(type => new
        {
            name = target.StringLiteral(type.Name),
            properties = semantics.Fields(type.Name).Where(field => !field.Name.StartsWith("__", StringComparison.Ordinal)).Select(field => Property(field.Name, "?", Result(field.Type), Description(field.Description, field.Directives))).ToArray()
        }).ToArray();
        var resolvers = localTypes.Where(type => type is GraphQlObjectType or GraphQlInterfaceType).GroupBy(type => type.Name, StringComparer.Ordinal).Select(group => new
        {
            name = target.StringLiteral(group.Key),
            fields = group.SelectMany(type => type is GraphQlObjectType obj ? obj.Fields : ((GraphQlInterfaceType)type).Fields).Select(field => new
            {
                name = target.PropertyName(field.Name),
                arguments = "{ " + string.Join("; ", field.Arguments.Select(argument => target.PropertyName(argument.Name) + (argument.Type.IsNonNull && argument.DefaultValue is null ? "" : "?") + ": " + target.Render(argument.Type, schema, true))) + " }",
                result = Result(field.Type)
            }).ToArray()
        }).ToArray();
        return templates.Render("resolvers", new { parents, resolvers });
    }

    public string SchemaTypes()
    {
        target.PrepareNames(schema, document);
        var declarations = new List<string>();
        var span = new GraphQlSourceSpan(0, 0, 0, 0, false);
        var scalarNames = new[] { "ID", "String", "Int", "Float", "Boolean" }.Concat(schema.ScalarTypes.Select(type => type.Name)).Distinct(StringComparer.Ordinal);
        declarations.Add(templates.Render("schema.scalars", new { scalars = scalarNames.Select(name =>
        {
            var type = new GraphQlNonNullTypeReference(new GraphQlNamedTypeReference(name, span), span);
            return new { name = target.PropertyName(name), input = target.Render(type, schema, true), output = target.Render(type, schema, false), documentation = Documentation(schema.ScalarTypes.FirstOrDefault(scalar => scalar.Name == name)?.Description, "  ") };
        }).ToArray() }));
        foreach (var group in schema.Types.Concat(GraphQlIntrospection.Types.OfType<GraphQlEnumType>()).OfType<GraphQlEnumType>().GroupBy(type => type.Name, StringComparer.Ordinal))
            declarations.Add(Documentation(group.First().Description) + templates.Render("schema.enum", new { name = target.TypeName(group.Key), values = string.Join(" | ", group.SelectMany(type => type.Values).Select(value => Documentation(Description(value.Description, value.Directives)) + target.StringLiteral(value.Name)).Distinct(StringComparer.Ordinal)) }));
        foreach (var group in schema.Types.OfType<GraphQlInputObjectType>().GroupBy(type => type.Name, StringComparer.Ordinal))
        {
            var fields = group.SelectMany(type => type.Fields).ToArray();
            var comment = Documentation(group.First().Description);
            if (new GraphQlSchemaSemantics(schema).IsOneOf(group.Key))
                declarations.Add(comment + templates.Render("schema.input-oneof", new
                {
                    name = target.TypeName(group.Key),
                    variants = fields.Select((selected, index) => new
                    {
                        properties = fields.Select(field => Property(field.Name, field == selected ? "" : "?", field == selected ? target.Render(new GraphQlNonNullTypeReference(field.Type, field.Source), schema, true) : "never", Description(field.Description, field.Directives))).ToArray(),
                        suffix = index == fields.Length - 1 ? ";" : " |"
                    }).ToArray()
                }));
            else
                declarations.Add(comment + templates.Render("schema.input", new { name = target.TypeName(group.Key), properties = fields.Select(field => Property(field.Name, field.Type.IsNonNull && field.DefaultValue is null ? "" : "?", target.Render(field.Type, schema, true), Description(field.Description, field.Directives))).ToArray() }));
        }
        return templates.Render("schema", new { declarations });
    }

    private string Property(string name, string optional, string type, string? description = null)
        => Documentation(description, "  ") + templates.Render("property", new { name = target.PropertyName(name), optional, type, description });

    private static string? Description(string? description, IReadOnlyList<GraphQlDirective> directives)
    {
        var deprecated = directives.FirstOrDefault(directive => directive.Name == "deprecated");
        if (deprecated is null) return description;
        var reason = deprecated.Arguments.FirstOrDefault(argument => argument.Name == "reason")?.Value.Text ?? "No longer supported";
        return (string.IsNullOrWhiteSpace(description) ? "" : description + "\n\n") + "@deprecated " + reason;
    }

    private string Documentation(string? description, string indent = "")
        => string.IsNullOrWhiteSpace(description) ? "" : templates.Render("documentation", new
        {
            indent,
            lines = description.Replace("*/", "* /", StringComparison.Ordinal).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
        });

    public static string OperationId(string query) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(query.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')))).ToLowerInvariant();

    public string Operations(IEnumerable<GraphQlOperationDefinition>? selectedOperations = null)
    {
        target.PrepareNames(schema, document);
        var declarations = new List<string>();
        var formatter = new GraphQlOperationDocumentFormatter(schema, document);
        foreach (var operation in selectedOperations ?? document.Operations)
        {
            var name = target.OperationName(operation.Name ?? throw new InvalidDataException("TypeScript operations must be named."));
            var variables = templates.Render("operations.variables", new { name, properties = operation.Variables.Select(variable => Property(variable.Name, variable.Type.IsNonNull && variable.DefaultValue is null ? "" : "?", target.Render(variable.Type, schema, true))).ToArray() });
            var response = templates.Render("operations.response", new { name = name + "Result", type = Shape(operation.ResponseProjection ?? new GraphQlResponseProjector(schema, document).Project(operation), false, operation.SelectionSet) });
            var query = formatter.Format(operation);
            var normalized = GraphQlModelFactory.CreateExecutable(new[] { global::Salep.GraphQLParser.GraphQLParser.Parse(new SourceText(query.AsMemory())) });
            var planner = new Salep.ClientGenerator.TypeScript.Generation.TypeScriptClientDocumentPlanner(schema, document);
            var hasLocal = clientExecution && planner.HasLocalSelections(operation);
            var localExecution = hasLocal ? new
            {
                query = planner.RemoteQuery(operation), selections = new Salep.ClientGenerator.TypeScript.Generation.TypeScriptClientDocumentPlanner(schema, normalized).Plan(normalized.Operations.Single()),
                inputTypes = planner.InputTypes(), variableTypes = operation.Variables.ToDictionary(variable => variable.Name, variable => Salep.ClientGenerator.TypeScript.Generation.TypeScriptClientDocumentPlanner.TypeReference(variable.Type)),
                defaults = operation.Variables.Where(variable => variable.DefaultValue is not null).ToDictionary(variable => variable.Name, variable => Salep.ClientGenerator.TypeScript.Generation.TypeScriptClientDocumentPlanner.InputValue(variable.DefaultValue!))
            } : null;
            var ast = TypeScriptDocumentAst.Operation(normalized, query, persistedOperations && (!hasLocal || localExecution?.query is not null) ? OperationId(hasLocal ? localExecution!.query! : query) : null, localExecution);
            var typedDocument = templates.Render("operations.document", new { name, ast, result = name + "Result", variables = name + "Variables", document_type = "TypedDocument" });
            declarations.Add(templates.Render("operations.operation", new { variables, response, document = typedDocument, name, kind = char.ToUpperInvariant(operation.OperationType[0]) + operation.OperationType[1..] }));
        }
        return templates.Render("operations", new { declarations });
    }

    public string Fragments()
    {
        target.PrepareNames(schema, document);
        var declarations = new List<string>();
        var formatter = new GraphQlOperationDocumentFormatter(schema, document);
        var projector = new GraphQlResponseProjector(schema, document);
        foreach (var fragment in document.Fragments)
        {
            var concrete = new GraphQlSchemaSemantics(schema).PossibleTypes(fragment.TypeCondition).ToArray();
            if (concrete.Length == 0) throw new InvalidDataException($"Unknown fragment type '{fragment.TypeCondition}'.");
            var shape = "(" + string.Join(" | ", concrete.Select(type => Shape(projector.ProjectFragment(fragment, type), concrete.Length > 1, fragment.SelectionSet))) + ") & { readonly __salepFragmentName?: " + target.StringLiteral(fragment.Name) + " }";
            declarations.Add(templates.Render("operations.response", new { name = target.FragmentName(fragment.Name), type = shape }));
            var dependencies = FragmentDependencies(fragment.SelectionSet, new HashSet<string>(StringComparer.Ordinal)).ToArray();
            var ast = TypeScriptDocumentAst.FragmentDocument(formatter.NormalizeFragment(fragment), dependencies.Select(formatter.NormalizeFragment).ToArray());
            declarations.Add(templates.Render("operations.document", new { name = target.FragmentName(fragment.Name), ast, result = target.FragmentName(fragment.Name), variables = "Record<string, never>", document_type = "TypedDocumentNode" }));
        }
        return templates.Render("operations", new { declarations });
    }

    private IEnumerable<GraphQlFragmentDefinition> FragmentDependencies(GraphQlSelectionSet selections, HashSet<string> seen)
    {
        foreach (var selection in selections.Selections)
        {
            if (selection is GraphQlFragmentSpreadSelection spread && seen.Add(spread.Name))
            {
                var fragment = document.Fragments.Single(item => item.Name == spread.Name);
                yield return fragment;
                foreach (var dependency in FragmentDependencies(fragment.SelectionSet, seen)) yield return dependency;
            }
            else if (selection is GraphQlFieldSelection { SelectionSet: { } nested })
                foreach (var dependency in FragmentDependencies(nested, seen)) yield return dependency;
            else if (selection is GraphQlInlineFragmentSelection inline)
                foreach (var dependency in FragmentDependencies(inline.SelectionSet, seen)) yield return dependency;
        }
    }

    private string Shape(GraphQlResponseProjection shape, bool includeTypename, GraphQlSelectionSet? selections = null)
    {
        var fields = new List<string>();
        var visible = selections is null ? [] : SelectedFields(selections, shape.GraphQlTypeName, !fragmentMasking).ToArray();
        var references = selections is null ? [] : FragmentReferences(selections, shape.GraphQlTypeName).Distinct(StringComparer.Ordinal).ToArray();
        if (references.Length > 0)
            fields.Add(Property("__salepFragments", "?", "{ " + string.Join("; ", references.Select(name => "readonly " + target.PropertyName(name) + ": " + target.FragmentName(name))) + " }"));
        if (includeTypename && !shape.Fields.Any(field => field.ResponseName == "__typename"))
            fields.Add(Property("__typename", "", target.StringLiteral(shape.GraphQlTypeName)));
        foreach (var field in shape.Fields)
        {
            if (fragmentMasking && selections is not null && field.GraphQlFieldName != "__typename" && !visible.Any(item => item.ResponseName == field.ResponseName)) continue;
            string type;
            if (field.GraphQlFieldName == "__typename") type = target.StringLiteral(shape.GraphQlTypeName);
            else
            {
                if (field.Type is null) throw new InvalidDataException($"Unknown field '{shape.GraphQlTypeName}.{field.GraphQlFieldName}'.");
                var children = visible.Where(item => item.ResponseName == field.ResponseName && item.SelectionSet is not null).SelectMany(item => item.SelectionSet!.Selections).ToArray();
                var childSet = new GraphQlSelectionSet(children, field.Source);
                var selected = field.Variants.Count == 0 ? null : "(" + string.Join(" | ", field.Variants.Select(variant => Shape(variant, IsAbstract(field.Type), childSet))) + ")";
                type = target.Render(field.Type, schema, false, selected);
            }
            var definition = new GraphQlSchemaSemantics(schema).Fields(shape.GraphQlTypeName).FirstOrDefault(item => item.Name == field.GraphQlFieldName);
            fields.Add(Property(field.ResponseName, field.IsConditional ? "?" : "", type, definition is null ? null : Description(definition.Description, definition.Directives)));
        }
        return templates.Render("operations.response-object", new { properties = fields }).Trim();
    }

    private bool Applies(string? condition, string concrete)
        => new GraphQlSchemaSemantics(schema).Applies(condition, concrete);

    private IEnumerable<GraphQlFieldSelection> SelectedFields(GraphQlSelectionSet selections, string concrete, bool includeFragments)
    {
        foreach (var selection in selections.Selections)
        {
            if (GraphQlSelectionConditions.Excluded(selection.Directives)) continue;
            if (selection is GraphQlFieldSelection field) yield return field;
            else if (selection is GraphQlInlineFragmentSelection inline && Applies(inline.TypeCondition, concrete))
                foreach (var nested in SelectedFields(inline.SelectionSet, concrete, includeFragments)) yield return nested;
            else if (includeFragments && selection is GraphQlFragmentSpreadSelection spread)
            {
                var fragment = document.Fragments.Single(item => item.Name == spread.Name);
                if (Applies(fragment.TypeCondition, concrete))
                    foreach (var nested in SelectedFields(fragment.SelectionSet, concrete, includeFragments)) yield return nested;
            }
        }
    }

    private IEnumerable<string> FragmentReferences(GraphQlSelectionSet selections, string concrete)
    {
        foreach (var selection in selections.Selections)
        {
            if (GraphQlSelectionConditions.Excluded(selection.Directives)) continue;
            if (selection is GraphQlFragmentSpreadSelection spread)
            {
                var fragment = document.Fragments.Single(item => item.Name == spread.Name);
                if (Applies(fragment.TypeCondition, concrete)) yield return fragment.Name;
            }
            else if (selection is GraphQlInlineFragmentSelection inline && Applies(inline.TypeCondition, concrete))
                foreach (var nested in FragmentReferences(inline.SelectionSet, concrete)) yield return nested;
        }
    }

    private bool IsAbstract(GraphQlTypeReference type)
    {
        var name = type switch
        {
            GraphQlNonNullTypeReference required => IsAbstract(required.NullableType),
            GraphQlListTypeReference list => IsAbstract(list.ElementType),
            GraphQlNamedTypeReference named => schema.UnionTypes.Any(union => union.Name == named.Name) || schema.InterfaceTypes.Any(item => item.Name == named.Name),
            _ => false
        };
        return name;
    }
}
