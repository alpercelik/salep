using System.Text.Json;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.Emission.Testing;

internal static class SampleJson
{
    private const int MaxSampleDepth = 4;
    public static string BuildSampleJson(ITypeNode type, SchemaModel schema, int depth, HashSet<string> seen)
    {
        if (type is NonNullTypeNode nonNull)
        {
            return BuildSampleJson(nonNull.Type, schema, depth, seen);
        }

        if (type is ListTypeNode listType)
        {
            var element = BuildSampleJson(listType.Type, schema, depth + 1, seen);
            return $"[{element}]";
        }

        if (type is NamedTypeNode namedType)
        {
            var name = namedType.Name.Value;
            if (schema.EnumTypes.TryGetValue(name, out var enumDef))
            {
                var firstValue = enumDef.Values.FirstOrDefault();
                var enumName = firstValue?.Name.Value ?? "UNKNOWN";
                return JsonSerializer.Serialize(enumName);
            }

            if (schema.IsUnionTypeName(name))
            {
                var unionDef = schema.UnionTypes[name];
                var firstMember = unionDef.Types.FirstOrDefault();
                if (firstMember is null)
                {
                    return "null";
                }

                var memberName = firstMember.Name.Value;
                return BuildObjectJson(memberName, schema, depth + 1, seen, memberName);
            }

            if (schema.IsInterfaceTypeName(name))
            {
                var implementation = GetFirstInterfaceImplementation(name, schema);
                return implementation is null
                    ? "null"
                    : BuildObjectJson(implementation, schema, depth + 1, seen, implementation);
            }

            if (schema.ObjectTypes.ContainsKey(name))
            {
                return BuildObjectJson(name, schema, depth + 1, seen, null);
            }

            return GetScalarJsonLiteral(name, schema);
        }

        return "null";
    }

    private static string BuildObjectJson(
        string objectTypeName,
        SchemaModel schema,
        int depth,
        HashSet<string> seen,
        string? typeNameOverride)
    {
        if (depth > MaxSampleDepth || !seen.Add(objectTypeName))
        {
            return BuildFallbackObjectJson(objectTypeName, schema, typeNameOverride);
        }

        if (!schema.ObjectTypes.TryGetValue(objectTypeName, out var objectDef))
        {
            seen.Remove(objectTypeName);
            return BuildFallbackObjectJson(objectTypeName, schema, typeNameOverride);
        }

        var properties = new List<string>();
        if (!string.IsNullOrWhiteSpace(typeNameOverride))
        {
            properties.Add($"\"__typename\":{JsonSerializer.Serialize(typeNameOverride)}");
        }

        foreach (var field in objectDef.Fields)
        {
            if (!IsRequiredField(field.Type))
            {
                continue;
            }

            var fieldValue = BuildSampleJson(field.Type, schema, depth + 1, seen);
            properties.Add($"\"{field.Name.Value}\":{fieldValue}");
        }

        seen.Remove(objectTypeName);
        return $"{{{string.Join(",", properties)}}}";
    }

    private static string BuildFallbackObjectJson(
        string objectTypeName,
        SchemaModel schema,
        string? typeNameOverride,
        HashSet<string>? seen = null)
    {
        if (!schema.ObjectTypes.TryGetValue(objectTypeName, out var objectDef))
        {
            return typeNameOverride is null
                ? "{}"
                : $"{{\"__typename\":{JsonSerializer.Serialize(typeNameOverride)}}}";
        }

        seen ??= new HashSet<string>(StringComparer.Ordinal);
        if (!seen.Add(objectTypeName))
        {
            return typeNameOverride is null
                ? "{}"
                : $"{{\"__typename\":{JsonSerializer.Serialize(typeNameOverride)}}}";
        }

        var properties = new List<string>();
        if (!string.IsNullOrWhiteSpace(typeNameOverride))
        {
            properties.Add($"\"__typename\":{JsonSerializer.Serialize(typeNameOverride)}");
        }

        foreach (var field in objectDef.Fields)
        {
            if (!IsRequiredField(field.Type))
            {
                continue;
            }

            var fieldValue = GetRequiredFallbackJson(field.Type, schema, seen);
            properties.Add($"\"{field.Name.Value}\":{fieldValue}");
        }

        seen.Remove(objectTypeName);
        return $"{{{string.Join(",", properties)}}}";
    }

    private static string GetRequiredFallbackJson(ITypeNode type, SchemaModel schema, HashSet<string>? seen = null)
    {
        if (type is NonNullTypeNode nonNull)
        {
            return GetRequiredFallbackJson(nonNull.Type, schema, seen);
        }

        if (type is ListTypeNode)
        {
            return "[]";
        }

        if (type is NamedTypeNode namedType)
        {
            var name = namedType.Name.Value;
            if (schema.EnumTypes.TryGetValue(name, out var enumDef))
            {
                var firstValue = enumDef.Values.FirstOrDefault();
                var enumName = firstValue?.Name.Value ?? "UNKNOWN";
                return JsonSerializer.Serialize(enumName);
            }

            if (schema.IsUnionTypeName(name))
            {
                var unionDef = schema.UnionTypes[name];
                var firstMember = unionDef.Types.FirstOrDefault();
                if (firstMember is not null)
                {
                    return BuildFallbackObjectJson(firstMember.Name.Value, schema, firstMember.Name.Value, seen);
                }
                return "{}";
            }

            if (schema.IsInterfaceTypeName(name))
            {
                var implementation = GetFirstInterfaceImplementation(name, schema);
                if (implementation is not null)
                {
                    return BuildFallbackObjectJson(implementation, schema, implementation, seen);
                }
                return "{}";
            }

            if (schema.ObjectTypes.ContainsKey(name))
            {
                return BuildFallbackObjectJson(name, schema, null, seen);
            }

            return GetScalarJsonLiteral(name, schema);
        }

        return "null";
    }

    private static string GetScalarJsonLiteral(string name, SchemaModel schema)
    {
        if (schema.Config.TryGetScalarSampleJsonLiteral(name, out var jsonLiteral))
        {
            return jsonLiteral;
        }

        return JsonSerializer.Serialize("sample");
    }

    private static string? GetFirstInterfaceImplementation(string interfaceName, SchemaModel schema)
    {
        foreach (var objectType in schema.ObjectTypes.Values)
        {
            foreach (var interfaceType in objectType.Interfaces)
            {
                if (interfaceType.Name.Value == interfaceName)
                {
                    return objectType.Name.Value;
                }
            }
        }

        return null;
    }

    private static bool IsRequiredField(ITypeNode type)
        => type is NonNullTypeNode;

}
