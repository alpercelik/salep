using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.Targets;

/// <summary>Creates C# fixture expressions from GraphQL input types using target naming and scalar policies.</summary>
public sealed class CSharpSampleValueFactory(GraphQlSchemaModel schema, CSharpCodeGenerationTarget target)
{
    private const int MaximumDepth = 8;

    /// <summary>Builds a deterministic expression suitable for initializing a generated operation variables object.</summary>
    public string Create(GraphQlTypeReference type) => Create(type, 0, new HashSet<string>(StringComparer.Ordinal));

    private string Create(GraphQlTypeReference type, int depth, HashSet<string> activeInputTypes)
    {
        if (type is GraphQlNonNullTypeReference required) return Create(required.NullableType, depth, activeInputTypes);
        if (type is GraphQlListTypeReference list)
        {
            var elementType = target.RenderType(list.ElementType, schema).TrimEnd('?');
            return $"new List<{elementType}> {{ {Create(list.ElementType, depth + 1, activeInputTypes)} }}";
        }

        var namedType = (GraphQlNamedTypeReference)type;
        var name = namedType.Name;
        if (target.TryGetScalar(name, out var mapping)) return mapping.SampleExpression ?? "default!";
        if (schema.EnumTypes.FirstOrDefault(item => item.Name == name) is { } enumeration)
        {
            var first = enumeration.Values.FirstOrDefault();
            var enumTypeName = target.RenderType(namedType, schema).TrimEnd('?');
            return first is null ? "default!" : $"{enumTypeName}.{target.EnumValueName(first.Name)}";
        }

        if (schema.ScalarTypes.Any(item => item.Name == name)) return target.StringLiteral("sample");
        if (schema.InputTypes.FirstOrDefault(item => item.Name == name) is not { } input) return "default!";
        if (depth >= MaximumDepth || !activeInputTypes.Add(name)) return "default!";

        try
        {
            var fields = input.Fields.Select(field =>
                $"{target.PropertyName(field.Name)} = {Create(field.Type, depth + 1, activeInputTypes)}");
            var inputTypeName = target.RenderType(namedType, schema).TrimEnd('?');
            return $"new {inputTypeName} {{ {string.Join(", ", fields)} }}";
        }
        finally
        {
            activeInputTypes.Remove(name);
        }
    }
}
