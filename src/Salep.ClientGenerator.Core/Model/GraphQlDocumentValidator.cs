using System.Globalization;
using System.Text.Json;

namespace Salep.ClientGenerator.Model;

/// <summary>Validates schema relationships and executable contracts before client output is published.</summary>
public sealed class GraphQlDocumentValidator(GraphQlSchemaModel schema, GraphQlExecutableDocument document, bool allowUnusedVariables = false)
{
    private readonly GraphQlSchemaSemantics types = new(schema);
    private static readonly HashSet<string> Builtins = new(["String", "ID", "Int", "Float", "Boolean"], StringComparer.Ordinal);
    private readonly Dictionary<string, GraphQlFragmentDefinition> fragments = new(StringComparer.Ordinal);

    public void Validate()
    {
        Unique(document.Fragments.Select(fragment => (fragment.Name, fragment.Source)), "fragment");
        Unique(document.Operations.Where(operation => operation.Name is not null).Select(operation => (operation.Name!, operation.Source)), "operation");
        if (document.Operations.Count > 1 && document.Operations.FirstOrDefault(operation => operation.Name is null) is { } anonymous)
            Fail("An anonymous operation must be the only operation in its document.", anonymous.Source);
        fragments.Clear();
        foreach (var fragment in document.Fragments) fragments.Add(fragment.Name, fragment);
        ValidateSchema();
        foreach (var fragment in document.Fragments)
        {
            if (fragment.Variables.Count != 0) Fail("Fragment variables are not supported.", fragment.Source);
            Composite(fragment.TypeCondition, fragment.Source);
            Directives(fragment.Directives, "FRAGMENT_DEFINITION", null, null);
            Selections(fragment.SelectionSet, fragment.TypeCondition, null, null, new(StringComparer.Ordinal));
        }
        foreach (var operation in document.Operations)
        {
            var root = types.Root(operation.OperationType);
            if (types.Type(root) is not GraphQlObjectType) Fail($"Missing object root for {operation.OperationType}.", operation.Source);
            Unique(operation.Variables.Select(variable => variable.Name), "variable", operation.Source);
            var variables = operation.Variables.ToDictionary(variable => variable.Name, StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var variable in operation.Variables)
            {
                InputType(variable.Type);
                if (variable.DefaultValue is not null) Value(variable.DefaultValue, variable.Type, null, null, false);
                Directives(variable.Directives, "VARIABLE_DEFINITION", variables, used);
            }
            Directives(operation.Directives, operation.OperationType.ToUpperInvariant(), variables, used);
            Selections(operation.SelectionSet, root, variables, used, new(StringComparer.Ordinal));
            foreach (var variable in operation.Variables)
                if (!allowUnusedVariables && !used.Contains(variable.Name)) Fail($"Unused variable '${variable.Name}' in operation '{operation.Name}'.", variable.Source);
            if (operation.OperationType == "subscription")
            {
                ValidateSubscriptionRoot(operation.SelectionSet, new(StringComparer.Ordinal));
                var fields = Flatten(operation.SelectionSet, root, new(StringComparer.Ordinal)).ToArray();
                if (fields.Select(field => field.Field.ResponseName).Distinct(StringComparer.Ordinal).Count() != 1 || fields.Any(field => field.Field.Name.StartsWith("__", StringComparison.Ordinal)))
                    Fail("Subscriptions require exactly one non-introspection root field.", operation.Source);
            }
        }
    }

    private void ValidateSchema()
    {
        foreach (var group in schema.Types.GroupBy(type => type.Name, StringComparer.Ordinal))
        {
            var definitions = group.Where(type => !type.IsExtension).ToArray();
            if (definitions.Length != 1) Fail($"Type '{group.Key}' requires exactly one definition.", group.First().Source);
            var definition = definitions[0];
            if (group.Key.StartsWith("__", StringComparison.Ordinal)) Fail("Schema names beginning with '__' are reserved.", definition.Source);
            if (Builtins.Contains(group.Key) && definition is not GraphQlScalarType) Fail($"Built-in scalar '{group.Key}' cannot be replaced.", definition.Source);
            if (Builtins.Contains(group.Key) && group.SelectMany(type => type.Directives).FirstOrDefault(directive => directive.Name == "specifiedBy") is { } specification)
                Fail("@specifiedBy cannot appear on built-in scalar types.", specification.Source);
            if (group.Any(type => type.GetType() != definition.GetType())) Fail($"Extension kind does not match '{group.Key}'.", definition.Source);
            Unique(group.SelectMany(type => type switch
            {
                GraphQlObjectType obj => obj.Fields.Select(field => field.Name),
                GraphQlInterfaceType face => face.Fields.Select(field => field.Name),
                GraphQlInputObjectType input => input.Fields.Select(field => field.Name),
                GraphQlEnumType enumeration => enumeration.Values.Select(value => value.Name),
                GraphQlUnionType union => union.Members,
                _ => Array.Empty<string>()
            }), "schema member", definition.Source);
            if (definition is GraphQlObjectType or GraphQlInterfaceType)
            {
                var fields = types.Fields(group.Key).Where(field => !GraphQlIntrospection.QueryFields.Contains(field)).ToArray();
                if (fields.Length == 0) Fail($"Type '{group.Key}' has no fields.", definition.Source);
                Unique(types.Interfaces(group.Key), "implemented interface", definition.Source);
                foreach (var field in fields)
                {
                    Reserved(field.Name, field.Source);
                    OutputType(field.Type);
                    InputDefinitions(field.Arguments);
                    Directives(field.Directives, "FIELD_DEFINITION", null, null);
                }
                foreach (var face in types.Interfaces(group.Key))
                {
                    if (types.Type(face) is not GraphQlInterfaceType || face == group.Key || HasInterfaceCycle(face, new(StringComparer.Ordinal) { group.Key }))
                        Fail($"Invalid interface relationship '{group.Key}' -> '{face}'.", definition.Source);
                    foreach (var ancestor in types.Interfaces(face))
                        if (!types.Interfaces(group.Key).Contains(ancestor, StringComparer.Ordinal)) Fail($"'{group.Key}' must explicitly implement ancestor interface '{ancestor}'.", definition.Source);
                    foreach (var contract in types.Fields(face))
                    {
                        var field = fields.FirstOrDefault(candidate => candidate.Name == contract.Name);
                        if (field is null || !OutputSubtype(field.Type, contract.Type)) Fail($"'{group.Key}' does not satisfy interface field '{face}.{contract.Name}'.", definition.Source);
                        foreach (var arg in contract.Arguments)
                            if (!field!.Arguments.Any(candidate => candidate.Name == arg.Name && TypeEqual(candidate.Type, arg.Type))) Fail($"Interface argument '{face}.{contract.Name}({arg.Name}:)' does not match.", definition.Source);
                        if (field!.Arguments.Any(arg => Required(arg) && !contract.Arguments.Any(candidate => candidate.Name == arg.Name))) Fail("Interface implementations cannot add required arguments.", field.Source);
                    }
                }
            }
            if (definition is GraphQlInputObjectType)
            {
                var fields = types.Inputs(group.Key).ToArray();
                if (fields.Length == 0) Fail($"Input '{group.Key}' has no fields.", definition.Source);
                InputDefinitions(fields, "INPUT_FIELD_DEFINITION");
                if (types.IsOneOf(group.Key) && fields.Any(field => field.Type.IsNonNull || field.DefaultValue is not null)) Fail("OneOf fields must be nullable and have no defaults.", definition.Source);
                if (HasInputCycle(group.Key, new(StringComparer.Ordinal))) Fail($"Required input cycle at '{group.Key}'.", definition.Source);
                if (HasInputDefaultCycle(group.Key, new(GraphQlValueKind.ObjectLiteral, Fields: []), new(StringComparer.Ordinal)))
                    Fail($"Input default cycle at '{group.Key}'.", definition.Source);
            }
            if (definition is GraphQlUnionType)
            {
                var members = group.OfType<GraphQlUnionType>().SelectMany(union => union.Members).ToArray();
                if (members.Length == 0 || members.Any(name => types.Type(name) is not GraphQlObjectType)) Fail($"Union '{group.Key}' requires object members.", definition.Source);
            }
            if (definition is GraphQlEnumType)
            {
                var values = group.OfType<GraphQlEnumType>().SelectMany(enumeration => enumeration.Values).ToArray();
                if (values.Length == 0) Fail($"Enum '{group.Key}' has no values.", definition.Source);
                foreach (var value in values)
                {
                    Reserved(value.Name, value.Source);
                    if (value.Name is "true" or "false" or "null") Fail("Invalid enum value.", value.Source);
                    Directives(value.Directives, "ENUM_VALUE", null, null);
                }
            }
            Directives(group.SelectMany(type => type.Directives).ToArray(), definition switch
            {
                GraphQlObjectType => "OBJECT", GraphQlInterfaceType => "INTERFACE", GraphQlInputObjectType => "INPUT_OBJECT", GraphQlUnionType => "UNION", GraphQlEnumType => "ENUM", _ => "SCALAR"
            }, null, null);
        }
        Unique(schema.SchemaDefinitions.SelectMany(definition => definition.RootOperations).Select(root => (root.Operation, root.Source)), "schema root");
        if (types.Type(types.Root("query")) is not GraphQlObjectType) Fail("Schema requires a query object root.", SchemaSource());
        foreach (var definition in schema.SchemaDefinitions)
        {
            foreach (var root in definition.RootOperations)
                if (types.Type(root.TypeName) is not GraphQlObjectType) Fail($"Root '{root.TypeName}' must be an object.", root.Source);
        }
        Directives(schema.SchemaDefinitions.SelectMany(definition => definition.Directives).ToArray(), "SCHEMA", null, null);
        if (schema.SchemaDefinitions.Where(definition => !definition.IsExtension).Skip(1).FirstOrDefault() is { } duplicateSchema)
            Fail("Multiple schema definitions.", duplicateSchema.Source);
        var roots = new[] { "query", "mutation", "subscription" }.Select(types.Root).Where(name => types.Type(name) is not null).ToArray();
        foreach (var root in roots)
            if (types.Type(root) is not GraphQlObjectType) Fail($"Root '{root}' must be an object.", types.Type(root)!.Source);
        Unique(roots.Select(name => (name, schema.SchemaDefinitions.SelectMany(definition => definition.RootOperations).LastOrDefault(root => root.TypeName == name)?.Source ?? types.Type(name)!.Source)), "root object type");
        Unique(schema.DirectiveDefinitions.Select(directive => (directive.Name, directive.Source)), "directive definition");
        foreach (var directive in schema.DirectiveDefinitions)
        {
            Reserved(directive.Name, directive.Source);
            InputDefinitions(directive.Arguments);
            Unique(directive.Locations, "directive location", directive.Source);
            if (directive.Locations.Count == 0 || directive.Locations.Any(location => !Locations.Contains(location))) Fail($"Invalid location for directive '@{directive.Name}'.", directive.Source);
            if (HasDirectiveReferenceCycle(directive)) Fail($"Directive reference cycle at '@{directive.Name}'.", directive.Source);
        }
    }

    private bool HasDirectiveReferenceCycle(GraphQlDirectiveDefinition root)
    {
        var visitedDirectives = new HashSet<string>(StringComparer.Ordinal);
        var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
        bool Uses(IReadOnlyList<GraphQlDirective> usages)
        {
            foreach (var usage in usages)
            {
                if (usage.Name == root.Name) return true;
                if (visitedDirectives.Add(usage.Name) && schema.DirectiveDefinitions.FirstOrDefault(item => item.Name == usage.Name) is { } referenced
                    && ArgumentsReference(referenced.Arguments)) return true;
            }
            return false;
        }
        bool TypeReferences(string name)
        {
            if (!visitedTypes.Add(name)) return false;
            foreach (var type in schema.Types.Where(type => type.Name == name))
            {
                if (Uses(type.Directives)) return true;
                if (type is GraphQlInputObjectType input && ArgumentsReference(input.Fields)) return true;
                if (type is GraphQlEnumType enumeration && enumeration.Values.Any(value => Uses(value.Directives))) return true;
            }
            return false;
        }
        bool ArgumentsReference(IReadOnlyList<GraphQlInputValueDefinition> arguments)
            => arguments.Any(argument => Uses(argument.Directives) || TypeReferences(GraphQlSchemaSemantics.NamedType(argument.Type)));
        return ArgumentsReference(root.Arguments);
    }

    private GraphQlSourceSpan SchemaSource() => schema.SchemaDefinitions.FirstOrDefault()?.Source
        ?? schema.Types.FirstOrDefault()?.Source ?? schema.DirectiveDefinitions.FirstOrDefault()?.Source
        ?? document.Operations.FirstOrDefault()?.Source ?? document.Fragments.FirstOrDefault()?.Source ?? EmptySpan;

    private void ValidateSubscriptionRoot(GraphQlSelectionSet set, HashSet<string> visitedFragments)
    {
        foreach (var selection in set.Selections)
        {
            foreach (var directive in selection.Directives)
                if (directive.Name is "skip" or "include") Fail("Subscription root selections cannot use @skip or @include.", directive.Source);
            if (selection is GraphQlInlineFragmentSelection inline) ValidateSubscriptionRoot(inline.SelectionSet, visitedFragments);
            if (selection is GraphQlFragmentSpreadSelection spread && visitedFragments.Add(spread.Name))
                ValidateSubscriptionRoot(fragments[spread.Name].SelectionSet, visitedFragments);
        }
    }

    private void InputDefinitions(IEnumerable<GraphQlInputValueDefinition> definitions, string location = "ARGUMENT_DEFINITION")
    {
        var values = definitions.ToArray();
        Unique(values.Select(value => value.Name), "input value", values.FirstOrDefault()?.Source ?? EmptySpan);
        foreach (var value in values)
        {
            Reserved(value.Name, value.Source);
            InputType(value.Type);
            if (value.DefaultValue is not null) Value(value.DefaultValue, value.Type, null, null, false);
            Directives(value.Directives, location, null, null);
            if (Required(value) && value.Directives.Any(directive => directive.Name == "deprecated")) Fail("Required inputs cannot be deprecated.", value.Source);
        }
    }

    private bool HasInterfaceCycle(string name, HashSet<string> path)
    {
        if (!path.Add(name)) return true;
        try { return types.Interfaces(name).Any(parent => HasInterfaceCycle(parent, path)); }
        finally { path.Remove(name); }
    }
    private bool HasInputCycle(string name, HashSet<string> path)
    {
        if (!path.Add(name)) return true;
        try { return types.Inputs(name).Any(field => field.Type is GraphQlNonNullTypeReference { NullableType: GraphQlNamedTypeReference named } && types.Type(named.Name) is GraphQlInputObjectType && HasInputCycle(named.Name, path)); }
        finally { path.Remove(name); }
    }

    private bool HasInputDefaultCycle(string name, GraphQlValue value, HashSet<string> visitedDefaults)
    {
        if (types.Type(name) is not GraphQlInputObjectType) return false;
        if (value.Kind == GraphQlValueKind.ListLiteral)
            return value.Items!.Any(item => HasInputDefaultCycle(name, item, visitedDefaults));
        if (value.Kind != GraphQlValueKind.ObjectLiteral) return false;
        foreach (var field in types.Inputs(name))
        {
            var fieldType = GraphQlSchemaSemantics.NamedType(field.Type);
            if (types.Type(fieldType) is not GraphQlInputObjectType) continue;
            var supplied = value.Fields!.FirstOrDefault(item => item.Name == field.Name);
            if (supplied is not null)
            {
                if (HasInputDefaultCycle(fieldType, supplied.Value, visitedDefaults)) return true;
            }
            else if (field.DefaultValue is not null)
            {
                var identity = name + "." + field.Name;
                if (!visitedDefaults.Add(identity)) return true;
                try { if (HasInputDefaultCycle(fieldType, field.DefaultValue, visitedDefaults)) return true; }
                finally { visitedDefaults.Remove(identity); }
            }
        }
        return false;
    }

    private void Selections(GraphQlSelectionSet set, string parent, IReadOnlyDictionary<string, GraphQlVariableDefinition>? variables, HashSet<string>? used, HashSet<string> active)
    {
        foreach (var selection in set.Selections)
        {
            switch (selection)
            {
                case GraphQlFieldSelection field:
                    Directives(field.Directives, "FIELD", variables, used);
                    var definition = Field(parent, field);
                    Arguments(field.Arguments, definition.Arguments, variables, used, field.Source);
                    var child = GraphQlSchemaSemantics.NamedType(definition.Type);
                    var composite = types.Type(child) is GraphQlObjectType or GraphQlInterfaceType or GraphQlUnionType;
                    if (composite != (field.SelectionSet is not null)) Fail($"Field '{parent}.{field.Name}' {(composite ? "requires" : "cannot have")} a selection set.", field.Source);
                    if (field.SelectionSet is not null) Selections(field.SelectionSet, child, variables, used, active);
                    break;
                case GraphQlInlineFragmentSelection inline:
                    Directives(inline.Directives, "INLINE_FRAGMENT", variables, used);
                    var condition = inline.TypeCondition ?? parent;
                    Overlap(parent, condition, inline.Source);
                    Selections(inline.SelectionSet, condition, variables, used, active);
                    break;
                case GraphQlFragmentSpreadSelection spread:
                    Directives(spread.Directives, "FRAGMENT_SPREAD", variables, used);
                    if (spread.Arguments.Count != 0) Fail("Fragment arguments are not supported.", spread.Source);
                    if (!fragments.TryGetValue(spread.Name, out var fragment)) Fail($"Unknown fragment '{spread.Name}'.", spread.Source);
                    Overlap(parent, fragment!.TypeCondition, spread.Source);
                    if (!active.Add(spread.Name)) Fail($"Fragment cycle at '{spread.Name}'.", spread.Source);
                    Directives(fragment.Directives, "FRAGMENT_DEFINITION", variables, used);
                    Selections(fragment.SelectionSet, fragment.TypeCondition, variables, used, active);
                    active.Remove(spread.Name);
                    break;
            }
        }
        Conflicts(Flatten(set, parent, new(StringComparer.Ordinal)).ToArray());
    }

    private GraphQlFieldDefinition Field(string parent, GraphQlFieldSelection field)
    {
        if (field.Name == "__typename") return new("__typename", null, [], new GraphQlNonNullTypeReference(new GraphQlNamedTypeReference("String", field.Source), field.Source), [], field.Source);
        return types.Fields(parent).FirstOrDefault(definition => definition.Name == field.Name)
            ?? throw Error($"Unknown field '{parent}.{field.Name}'.", field.Source);
    }
    private sealed record Selected(GraphQlFieldSelection Field, string Parent, IReadOnlyList<string> Possible);
    private IEnumerable<Selected> Flatten(GraphQlSelectionSet set, string parent, HashSet<string> path, IReadOnlyList<string>? possible = null)
    {
        possible ??= types.PossibleTypes(parent);
        foreach (var selection in set.Selections)
        {
            if (selection is GraphQlFieldSelection field) yield return new(field, parent, possible);
            else if (selection is GraphQlInlineFragmentSelection inline)
            {
                var condition = inline.TypeCondition ?? parent;
                foreach (var nested in Flatten(inline.SelectionSet, condition, path, possible.Intersect(types.PossibleTypes(condition), StringComparer.Ordinal).ToArray())) yield return nested;
            }
            else if (selection is GraphQlFragmentSpreadSelection spread && path.Add(spread.Name))
            {
                if (!fragments.TryGetValue(spread.Name, out var fragment)) Fail($"Unknown fragment '{spread.Name}'.", spread.Source);
                foreach (var nested in Flatten(fragment!.SelectionSet, fragment.TypeCondition, path, possible.Intersect(types.PossibleTypes(fragment.TypeCondition), StringComparer.Ordinal).ToArray())) yield return nested;
                path.Remove(spread.Name);
            }
        }
    }
    private void Conflicts(IReadOnlyList<Selected> fields, bool mutuallyExclusive = false)
    {
        foreach (var group in fields.GroupBy(field => field.Field.ResponseName, StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            for (var i = 0; i < entries.Length; i++) for (var j = i + 1; j < entries.Length; j++)
            {
                var left = entries[i]; var right = entries[j];
                var overlaps = !(left.Parent != right.Parent && types.Type(left.Parent) is GraphQlObjectType && types.Type(right.Parent) is GraphQlObjectType);
                var a = Field(left.Parent, left.Field); var b = Field(right.Parent, right.Field);
                if (!SameResponseType(a.Type, b.Type) || !mutuallyExclusive && overlaps && (left.Field.Name != right.Field.Name || ArgumentsKey(left.Field.Arguments) != ArgumentsKey(right.Field.Arguments)))
                    Fail($"Conflicting fields for response name '{group.Key}'.", right.Field.Source);
                if (left.Field.SelectionSet is not null && right.Field.SelectionSet is not null)
                    Conflicts(Flatten(left.Field.SelectionSet, GraphQlSchemaSemantics.NamedType(a.Type), new(StringComparer.Ordinal)).Concat(Flatten(right.Field.SelectionSet, GraphQlSchemaSemantics.NamedType(b.Type), new(StringComparer.Ordinal))).ToArray(), mutuallyExclusive || !overlaps);
            }
        }
    }
    private bool SameResponseType(GraphQlTypeReference a, GraphQlTypeReference b)
    {
        if (a is GraphQlNonNullTypeReference required) return b is GraphQlNonNullTypeReference other && SameResponseType(required.NullableType, other.NullableType);
        if (b is GraphQlNonNullTypeReference) return false;
        if (a is GraphQlListTypeReference list) return b is GraphQlListTypeReference other && SameResponseType(list.ElementType, other.ElementType);
        if (b is GraphQlListTypeReference) return false;
        var x = GraphQlSchemaSemantics.NamedType(a); var y = GraphQlSchemaSemantics.NamedType(b);
        return types.Type(x) is GraphQlObjectType or GraphQlInterfaceType or GraphQlUnionType && types.Type(y) is GraphQlObjectType or GraphQlInterfaceType or GraphQlUnionType || x == y;
    }

    private void Arguments(IReadOnlyList<GraphQlArgument> supplied, IReadOnlyList<GraphQlInputValueDefinition> expected, IReadOnlyDictionary<string, GraphQlVariableDefinition>? variables, HashSet<string>? used, GraphQlSourceSpan source)
    {
        Unique(supplied.Select(argument => argument.Name), "argument", source);
        foreach (var argument in supplied)
        {
            var definition = expected.FirstOrDefault(candidate => candidate.Name == argument.Name) ?? throw Error($"Unknown argument '{argument.Name}'.", argument.Source);
            Value(argument.Value, definition.Type, variables, used, definition.DefaultValue is not null);
        }
        foreach (var definition in expected)
            if (Required(definition) && !supplied.Any(argument => argument.Name == definition.Name)) Fail($"Missing required argument '{definition.Name}'.", source);
    }
    private void Value(GraphQlValue value, GraphQlTypeReference expected, IReadOnlyDictionary<string, GraphQlVariableDefinition>? variables, HashSet<string>? used, bool locationDefault)
    {
        if (value.Kind == GraphQlValueKind.Variable)
        {
            if (variables is null) return; // Fragment-only documents have no operation variable scope.
            if (!variables.TryGetValue(value.Text!, out var variable)) Fail($"Undefined variable '${value.Text}'.", value.Source ?? EmptySpan);
            used!.Add(variable!.Name);
            var adjusted = expected is GraphQlNonNullTypeReference required && !variable.Type.IsNonNull && (locationDefault || variable.DefaultValue is { Kind: not GraphQlValueKind.NullLiteral }) ? required.NullableType : expected;
            if (!VariableFits(variable.Type, adjusted)) Fail($"Variable '${variable.Name}' has an incompatible type.", variable.Source);
            return;
        }
        if (value.Kind == GraphQlValueKind.NullLiteral)
        {
            if (expected.IsNonNull) Fail("Null is not allowed for a non-null input.", value.Source ?? EmptySpan);
            return;
        }
        if (expected is GraphQlNonNullTypeReference nonNull) { Value(value, nonNull.NullableType, variables, used, locationDefault); return; }
        if (expected is GraphQlListTypeReference list)
        {
            foreach (var item in value.Kind == GraphQlValueKind.ListLiteral ? value.Items! : [value]) Value(item, list.ElementType, variables, used, false);
            return;
        }
        var name = GraphQlSchemaSemantics.NamedType(expected);
        var valid = name switch
        {
            "Int" => value.Kind == GraphQlValueKind.IntegerLiteral && int.TryParse(value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "Float" => value.Kind is GraphQlValueKind.IntegerLiteral or GraphQlValueKind.FloatLiteral && double.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number),
            "Boolean" => value.Kind == GraphQlValueKind.BooleanLiteral,
            "String" => value.Kind == GraphQlValueKind.StringLiteral,
            "ID" => value.Kind is GraphQlValueKind.StringLiteral or GraphQlValueKind.IntegerLiteral,
            _ => true
        };
        if (types.Type(name) is GraphQlEnumType)
            valid = value.Kind == GraphQlValueKind.EnumLiteral && schema.Types.OfType<GraphQlEnumType>().Where(type => type.Name == name).SelectMany(type => type.Values).Any(item => item.Name == value.Text);
        if (types.Type(name) is GraphQlInputObjectType)
        {
            if (value.Kind != GraphQlValueKind.ObjectLiteral) valid = false;
            else
            {
                Unique(value.Fields!.Select(field => field.Name), "input object field", value.Source ?? EmptySpan);
                var fields = types.Inputs(name).ToArray();
                var oneOf = types.IsOneOf(name);
                if (oneOf && (value.Fields!.Count != 1 || value.Fields[0].Value.Kind == GraphQlValueKind.NullLiteral)) Fail("OneOf values require exactly one non-null field.", value.Source ?? EmptySpan);
                foreach (var field in value.Fields!)
                {
                    var definition = fields.FirstOrDefault(candidate => candidate.Name == field.Name) ?? throw Error($"Unknown input field '{name}.{field.Name}'.", field.Source);
                    if (oneOf && field.Value.Kind == GraphQlValueKind.Variable && variables is not null && variables.TryGetValue(field.Value.Text!, out var variable) && !variable.Type.IsNonNull)
                        Fail("OneOf member variables must have non-null declared types, even with defaults.", variable.Source);
                    Value(field.Value, oneOf ? new GraphQlNonNullTypeReference(definition.Type, definition.Source) : definition.Type, variables, used, definition.DefaultValue is not null);
                }
                foreach (var field in fields)
                    if (Required(field) && !value.Fields!.Any(item => item.Name == field.Name)) Fail($"Missing required input field '{name}.{field.Name}'.", value.Source ?? EmptySpan);
            }
        }
        else if (!Builtins.Contains(name) && types.Type(name) is GraphQlScalarType)
            ScalarLiteralVariables(value, variables, used);
        if (!valid) Fail($"Invalid value for input type '{name}'.", value.Source ?? EmptySpan);
    }

    private static void ScalarLiteralVariables(GraphQlValue value, IReadOnlyDictionary<string, GraphQlVariableDefinition>? variables, HashSet<string>? used)
    {
        if (value.Kind == GraphQlValueKind.Variable && variables is not null)
        {
            if (!variables.ContainsKey(value.Text!)) Fail($"Undefined variable '${value.Text}'.", value.Source ?? EmptySpan);
            used!.Add(value.Text!);
        }
        // A scalar owns its composite literal shape, so nested members have no
        // schema input-field type for variable compatibility checks.
        if (value.Items is not null)
            foreach (var item in value.Items) ScalarLiteralVariables(item, variables, used);
        if (value.Fields is not null)
        {
            Unique(value.Fields.Select(field => (field.Name, field.Source)), "input object field");
            foreach (var field in value.Fields) ScalarLiteralVariables(field.Value, variables, used);
        }
    }
    private void Directives(IReadOnlyList<GraphQlDirective> directives, string location, IReadOnlyDictionary<string, GraphQlVariableDefinition>? variables, HashSet<string>? used)
    {
        foreach (var group in directives.GroupBy(directive => directive.Name, StringComparer.Ordinal))
        {
            var custom = schema.DirectiveDefinitions.FirstOrDefault(definition => definition.Name == group.Key);
            var builtin = BuiltinDirective(group.Key);
            var arguments = custom?.Arguments ?? builtin?.Arguments ?? throw Error($"Unknown directive '@{group.Key}'.", group.First().Source);
            var locations = custom?.Locations ?? builtin!.Locations;
            if (!locations.Contains(location, StringComparer.Ordinal)) Fail($"Directive '@{group.Key}' is invalid at {location}.", group.First().Source);
            if (!(custom?.IsRepeatable ?? false) && group.Count() > 1) Fail($"Directive '@{group.Key}' is not repeatable.", group.First().Source);
            foreach (var directive in group) Arguments(directive.Arguments, arguments, variables, used, directive.Source);
        }
    }
    private static GraphQlDirectiveDefinition? BuiltinDirective(string name)
    {
        GraphQlInputValueDefinition Argument(string argument, string type, bool required, GraphQlValue? fallback = null)
            => new(argument, null, required ? new GraphQlNonNullTypeReference(new GraphQlNamedTypeReference(type, EmptySpan), EmptySpan) : new GraphQlNamedTypeReference(type, EmptySpan), fallback, [], EmptySpan);
        return name switch
        {
            "include" or "skip" => new(name, null, [Argument("if", "Boolean", true)], false, ["FIELD", "FRAGMENT_SPREAD", "INLINE_FRAGMENT"], [], EmptySpan),
            "deprecated" => new(name, null, [Argument("reason", "String", true, new(GraphQlValueKind.StringLiteral, "No longer supported"))], false, ["FIELD_DEFINITION", "ENUM_VALUE", "ARGUMENT_DEFINITION", "INPUT_FIELD_DEFINITION"], [], EmptySpan),
            "oneOf" => new(name, null, [], false, ["INPUT_OBJECT"], [], EmptySpan),
            "specifiedBy" => new(name, null, [Argument("url", "String", true)], false, ["SCALAR"], [], EmptySpan),
            _ => null
        };
    }
    private void InputType(GraphQlTypeReference type)
    {
        var name = GraphQlSchemaSemantics.NamedType(type);
        if (!Builtins.Contains(name) && types.Type(name) is not (GraphQlScalarType or GraphQlEnumType or GraphQlInputObjectType)) Fail($"Unknown or non-input type '{name}'.", type.Source);
    }
    private void OutputType(GraphQlTypeReference type)
    {
        var name = GraphQlSchemaSemantics.NamedType(type);
        if (!Builtins.Contains(name) && types.Type(name) is not (GraphQlScalarType or GraphQlEnumType or GraphQlObjectType or GraphQlInterfaceType or GraphQlUnionType)) Fail($"Unknown or non-output type '{name}'.", type.Source);
    }
    private void Composite(string name, GraphQlSourceSpan source)
    {
        if (types.Type(name) is not (GraphQlObjectType or GraphQlInterfaceType or GraphQlUnionType)) Fail($"Fragment condition '{name}' must be a composite type.", source);
    }
    private void Overlap(string parent, string condition, GraphQlSourceSpan source)
    {
        Composite(condition, source);
        if (!types.PossibleTypes(parent).Intersect(types.PossibleTypes(condition), StringComparer.Ordinal).Any()) Fail($"Fragment condition '{condition}' cannot apply to '{parent}'.", source);
    }
    private static bool Required(GraphQlInputValueDefinition definition) => definition.Type.IsNonNull && definition.DefaultValue is null;
    private static bool VariableFits(GraphQlTypeReference variable, GraphQlTypeReference location)
    {
        if (location is GraphQlNonNullTypeReference required) return variable is GraphQlNonNullTypeReference other && VariableFits(other.NullableType, required.NullableType);
        if (variable is GraphQlNonNullTypeReference nonNull) return VariableFits(nonNull.NullableType, location);
        if (location is GraphQlListTypeReference list) return variable is GraphQlListTypeReference other && VariableFits(other.ElementType, list.ElementType);
        return variable is GraphQlNamedTypeReference a && location is GraphQlNamedTypeReference b && a.Name == b.Name;
    }
    private static bool TypeEqual(GraphQlTypeReference a, GraphQlTypeReference b) => VariableFits(a, b) && VariableFits(b, a);
    private bool OutputSubtype(GraphQlTypeReference actual, GraphQlTypeReference contract)
    {
        if (contract is GraphQlNonNullTypeReference required) return actual is GraphQlNonNullTypeReference other && OutputSubtype(other.NullableType, required.NullableType);
        if (actual is GraphQlNonNullTypeReference nonNull) return OutputSubtype(nonNull.NullableType, contract);
        if (contract is GraphQlListTypeReference list) return actual is GraphQlListTypeReference other && OutputSubtype(other.ElementType, list.ElementType);
        if (actual is not GraphQlNamedTypeReference a || contract is not GraphQlNamedTypeReference b) return false;
        return a.Name == b.Name || types.Type(a.Name) is GraphQlObjectType && types.Applies(b.Name, a.Name) || types.Type(a.Name) is GraphQlInterfaceType && types.Implements(a.Name, b.Name);
    }
    private static string ArgumentsKey(IReadOnlyList<GraphQlArgument> arguments) => string.Join(";", arguments.OrderBy(argument => argument.Name, StringComparer.Ordinal).Select(argument => argument.Name + ":" + ValueKey(argument.Value)));
    private static string ValueKey(GraphQlValue value) => value.Kind switch
    {
        GraphQlValueKind.ListLiteral => "[" + string.Join(",", value.Items!.Select(ValueKey)) + "]",
        GraphQlValueKind.ObjectLiteral => "{" + string.Join(",", value.Fields!.OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => field.Name + ":" + ValueKey(field.Value))) + "}",
        _ => value.Kind + ":" + JsonSerializer.Serialize(value.Text ?? value.Boolean?.ToString(CultureInfo.InvariantCulture))
    };
    private static void Unique(IEnumerable<string> names, string kind, GraphQlSourceSpan source)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names) if (!seen.Add(name)) Fail($"Duplicate {kind} '{name}'.", source);
    }
    private static void Unique(IEnumerable<(string Name, GraphQlSourceSpan Source)> definitions, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
            if (!seen.Add(definition.Name)) Fail($"Duplicate {kind} '{definition.Name}'.", definition.Source);
    }
    private static void Reserved(string name, GraphQlSourceSpan source) { if (name.StartsWith("__", StringComparison.Ordinal)) Fail("Names beginning with '__' are reserved.", source); }
    private static readonly GraphQlSourceSpan EmptySpan = new(0, 0, 0, 0, false);
    private static readonly HashSet<string> Locations = new(["QUERY", "MUTATION", "SUBSCRIPTION", "FIELD", "FRAGMENT_DEFINITION", "FRAGMENT_SPREAD", "INLINE_FRAGMENT", "VARIABLE_DEFINITION", "SCHEMA", "SCALAR", "OBJECT", "FIELD_DEFINITION", "ARGUMENT_DEFINITION", "INTERFACE", "UNION", "ENUM", "ENUM_VALUE", "INPUT_OBJECT", "INPUT_FIELD_DEFINITION"], StringComparer.Ordinal);
    private static InvalidDataException Error(string message, GraphQlSourceSpan source) => new(source.Diagnostic(message));
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string message, GraphQlSourceSpan source) => throw Error(message, source);
}
