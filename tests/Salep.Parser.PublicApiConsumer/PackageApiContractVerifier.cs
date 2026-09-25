using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Salep.Parser;

internal static class PackageApiContractVerifier
{
    public static void Verify()
    {
        var assembly = typeof(GraphQLParser).Assembly;

        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "public-api-contract.json")));
        var root = contract.RootElement;
        var project = root.GetProperty("projectIdentity");
        var expectedAssembly = project.GetProperty("assembly").GetString();
        var namespaceRoot = project.GetProperty("namespaceRoot").GetString()!;
        if (assembly.GetName().Name != expectedAssembly)
            throw new InvalidOperationException($"Expected assembly '{expectedAssembly}', found '{assembly.GetName().Name}'.");
        var frameworks = project.GetProperty("frameworks").EnumerateArray().Select(value => value.GetString()).ToArray();
        if (!frameworks.SequenceEqual(new[] { "net10.0", "net11.0" }))
            throw new InvalidOperationException("The selected package contract must target net10.0 and net11.0.");

        using var allowlist = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "package-api-allowlist.json")));
        var differences = allowlist.RootElement.GetProperty("allowedDifferences").EnumerateArray().Select(value => value.GetString()).ToArray();
        if (differences.Length != 0)
            throw new InvalidOperationException($"The selected package compatibility allowlist must be empty; found {differences.Length} entries.");

        var types = root.GetProperty("types").EnumerateArray().ToArray();
        var gaps = new List<string>();
        foreach (var expectedType in types)
        {
            var typeName = MapExpectedTypeName(expectedType.GetProperty("name").GetString()!, namespaceRoot);
            var actualType = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            if (actualType is null)
            {
                gaps.Add($"Missing type: {typeName}");
                continue;
            }
            var expectedBaseValue = expectedType.GetProperty("baseType").GetString();
            var expectedBase = string.IsNullOrEmpty(expectedBaseValue)
                ? expectedBaseValue
                : MapExpectedTypeName(expectedBaseValue, namespaceRoot);
            if (!string.IsNullOrEmpty(expectedBase) && assembly.GetType(expectedBase) is { } baseType && !baseType.IsAssignableFrom(actualType))
                gaps.Add($"Type {typeName} is not assignable to expected base {expectedBase}.");
            foreach (var expectedInterface in expectedType.GetProperty("interfaces").EnumerateArray()
                         .Select(value => MapExpectedTypeName(value.GetString()!, namespaceRoot)))
            {
                if (assembly.GetType(expectedInterface) is { } interfaceType && !interfaceType.IsAssignableFrom(actualType))
                    gaps.Add($"Type {typeName} does not implement expected interface {expectedInterface}.");
            }
            foreach (var expectedMember in expectedType.GetProperty("members").EnumerateArray())
            {
                var signature = ExpectedSignature(expectedMember, namespaceRoot);
                if (!ActualSignatures(actualType).Contains(signature))
                    gaps.Add($"Member missing or differs: {typeName} :: {signature}");
            }
        }

        if (gaps.Count != 0)
            throw new InvalidOperationException($"Package API contract found {gaps.Count} difference(s):\n" + string.Join("\n", gaps.Take(100)));

        Console.WriteLine($"Package API contract passed ({types.Length} public types, empty difference allowlist).");
    }

    private static HashSet<string> ActualSignatures(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var constructor in type.GetConstructors(flags))
            result.Add($"constructor|.ctor||{Parameters(constructor.GetParameters())}");
        foreach (var method in type.GetMethods(flags))
            result.Add($"{(method.IsStatic ? "staticMethod" : "method")}|{method.Name}|{TypeName(method.ReturnType)}|{Parameters(method.GetParameters())}");
        foreach (var property in type.GetProperties(flags))
            result.Add($"property|{property.Name}|{TypeName(property.PropertyType)}|get:{property.CanRead.ToString().ToLowerInvariant()};set:{property.CanWrite.ToString().ToLowerInvariant()}|");
        foreach (var field in type.GetFields(flags))
        {
            var value = field.IsLiteral ? Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture) : null;
            result.Add($"{(field.IsLiteral ? "constant" : "field")}|{field.Name}|{TypeName(field.FieldType)}{(field.IsLiteral ? $"|{value}" : string.Empty)}|");
        }
        return result;
    }

    private static string ExpectedSignature(JsonElement member, string namespaceRoot)
    {
        var kind = member.GetProperty("kind").GetString()!;
        var name = member.TryGetProperty("name", out var memberName) ? memberName.GetString()! : ".ctor";
        var returnType = member.TryGetProperty("returnType", out var resultType)
            ? resultType.GetString()!
            : member.TryGetProperty("type", out var propertyType) ? propertyType.GetString()! : string.Empty;
        returnType = MapExpectedTypeName(returnType, namespaceRoot);
        if (kind == "property")
            return $"{kind}|{name}|{returnType}|get:{member.GetProperty("readable").GetBoolean().ToString().ToLowerInvariant()};set:{member.GetProperty("writable").GetBoolean().ToString().ToLowerInvariant()}|";
        if (kind is "constant" or "field")
            return $"{kind}|{name}|{returnType}|{(member.TryGetProperty("value", out var value) ? value.ToString() : string.Empty)}|";
        var parameters = member.TryGetProperty("parameters", out var values)
            ? string.Join(",", values.EnumerateArray().Select(parameter => ExpectedParameter(parameter, namespaceRoot)))
            : string.Empty;
        return $"{kind}|{name}|{returnType}|{parameters}";
    }

    private static string Parameters(ParameterInfo[] parameters) =>
        string.Join(",", parameters.Select(parameter =>
            $"{parameter.Name}:{TypeName(parameter.ParameterType)}:{parameter.IsOptional.ToString().ToLowerInvariant()}:{(parameter.HasDefaultValue && parameter.DefaultValue is not null ? Convert.ToString(parameter.DefaultValue, CultureInfo.InvariantCulture) : string.Empty)}"));

    private static string ExpectedParameter(JsonElement parameter, string namespaceRoot)
    {
        var defaultValue = parameter.GetProperty("defaultValue");
        var typeName = MapExpectedTypeName(parameter.GetProperty("type").GetString()!, namespaceRoot);
        return $"{parameter.GetProperty("name").GetString()}:{typeName}:{parameter.GetProperty("optional").GetBoolean().ToString().ToLowerInvariant()}:{(defaultValue.ValueKind == JsonValueKind.String ? defaultValue.GetString() : string.Empty)}";
    }

    private static string MapExpectedTypeName(string typeName, string namespaceRoot) =>
        typeName.Replace("GraphQLParser.", namespaceRoot + ".", StringComparison.Ordinal);

    private static string TypeName(Type type)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()!) + "&";
        if (type.IsGenericParameter) return type.Name;
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().FullName!;
            name = name[..name.IndexOf('`')];
            return name + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
        }
        return type.FullName ?? type.Name;
    }
}
