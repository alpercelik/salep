using System.Text.Json;
using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.Targets;

/// <summary>Builds representative GraphQL response JSON from operation response projections.</summary>
public sealed class CSharpResponseSampleJsonFactory(GraphQlSchemaModel schema, CSharpCodeGenerationTarget target)
{
    /// <summary>Creates one JSON object matching the selected root response shape.</summary>
    public string Create(GraphQlOperationDefinition operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var projection = operation.ResponseProjection ?? throw new ArgumentException("Operation response projection is required.", nameof(operation));
        return Projection(projection, 0);
    }

    private string Projection(GraphQlResponseProjection projection, int depth)
    {
        if (depth > 12) return "{}";
        var fields = projection.Fields.Select(field =>
            JsonSerializer.Serialize(field.ResponseName) + ":" + FieldValue(field, projection.GraphQlTypeName, depth + 1));
        return "{" + string.Join(",", fields) + "}";
    }

    private string FieldValue(GraphQlResponseFieldProjection field, string parentTypeName, int depth)
    {
        if (field.GraphQlFieldName == "__typename") return JsonSerializer.Serialize(parentTypeName);
        var type = field.Type ?? FindField(parentTypeName, field.GraphQlFieldName)?.Type;
        if (type is null) return "null";
        while (type is GraphQlNonNullTypeReference nonNull) type = nonNull.NullableType;
        if (type is GraphQlListTypeReference list) return "[" + TypeValue(list.ElementType, field.Variants, depth + 1) + "]";
        return TypeValue(type, field.Variants, depth + 1);
    }

    private string TypeValue(GraphQlTypeReference type, IReadOnlyList<GraphQlResponseProjection> variants, int depth)
    {
        if (type is GraphQlNonNullTypeReference nonNull) return TypeValue(nonNull.NullableType, variants, depth + 1);
        if (type is GraphQlListTypeReference list) return "[" + TypeValue(list.ElementType, variants, depth + 1) + "]";
        var name = ((GraphQlNamedTypeReference)type).Name;

        if (target.TryGetScalar(name, out var mapping)) return mapping.SampleJson ?? "\"sample\"";
        if (schema.EnumTypes.FirstOrDefault(item => item.Name == name) is { } enumeration)
            return JsonSerializer.Serialize(enumeration.Values.FirstOrDefault()?.Name);
        if (schema.ScalarTypes.Any(item => item.Name == name)) return "\"sample\"";

        if (depth > 12)
        {
            if (variants.Count > 0 && (schema.UnionTypes.Any(item => item.Name == name) || schema.InterfaceTypes.Any(item => item.Name == name)))
            {
                return "{\"__typename\":" + JsonSerializer.Serialize(variants[0].GraphQlTypeName) + "}";
            }

            return schema.ObjectTypes.Any(item => item.Name == name) ? "{}" : "null";
        }

        if (variants.Count > 0)
        {
            var selected = variants[0];
            if (schema.UnionTypes.Any(item => item.Name == name) || schema.InterfaceTypes.Any(item => item.Name == name))
            {
                var fields = selected.Fields.Select(child => JsonSerializer.Serialize(child.ResponseName) + ":" +
                    FieldValue(child, selected.GraphQlTypeName, depth + 1)).ToList();
                if (!selected.Fields.Any(child => child.GraphQlFieldName == "__typename" && child.ResponseName == "__typename"))
                {
                    fields.Insert(0, "\"__typename\":" + JsonSerializer.Serialize(selected.GraphQlTypeName));
                }

                return "{" + string.Join(",", fields) + "}";
            }

            return Projection(selected, depth + 1);
        }

        return schema.ObjectTypes.Any(item => item.Name == name) ? "{}" : "null";
    }

    private GraphQlFieldDefinition? FindField(string typeName, string fieldName)
    {
        return schema.Types.FirstOrDefault(type => type.Name == typeName && !type.IsExtension) switch
        {
            GraphQlObjectType obj => schema.Types.OfType<GraphQlObjectType>().Where(type => type.Name == typeName)
                .SelectMany(type => type.Fields).FirstOrDefault(field => field.Name == fieldName),
            GraphQlInterfaceType contract => schema.Types.OfType<GraphQlInterfaceType>().Where(type => type.Name == typeName)
                .SelectMany(type => type.Fields).FirstOrDefault(field => field.Name == fieldName),
            _ => null
        };
    }
}
