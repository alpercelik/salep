using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Salep.ClientGenerator.Model;

namespace Salep.ClientGenerator.Targets;

/// <summary>A C# scalar mapping, including whether the mapped type is a value type.</summary>
public sealed record CSharpScalarMapping(
    string TypeName,
    bool IsValueType,
    string? SampleExpression = null,
    string? SampleJson = null);

/// <summary>Customizable C# naming, scalar, namespace, and import policies.</summary>
public sealed record CSharpCodeGenerationOptions
{
    /// <summary>Default namespace for generated C# declarations.</summary>
    public string DefaultNamespace { get; init; } = "Salep.Generated";

    /// <summary>Chooses native C# unions instead of Dunet-generated unions.</summary>
    public bool UseNativeUnions { get; init; }

    /// <summary>Owning namespaces for shared generated types used in union cases.</summary>
    public IReadOnlyDictionary<string, string> TypeOwners { get; init; }
        = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Overrides the C# type identifier transformation.</summary>
    public Func<string, string>? TypeName { get; init; }

    /// <summary>Overrides the C# interface identifier transformation.</summary>
    public Func<string, string>? InterfaceName { get; init; }

    /// <summary>Overrides the C# property identifier transformation.</summary>
    public Func<string, string>? PropertyName { get; init; }

    /// <summary>Overrides the C# enum member identifier transformation.</summary>
    public Func<string, string>? EnumValueName { get; init; }

    /// <summary>Overrides the C# method identifier transformation.</summary>
    public Func<string, string>? MethodName { get; init; }

    /// <summary>Overrides the C# parameter identifier transformation.</summary>
    public Func<string, string>? ParameterName { get; init; }

    /// <summary>Overrides built-in scalar mappings and supplies mappings for custom scalars.</summary>
    public IReadOnlyDictionary<string, CSharpScalarMapping> ScalarMappings { get; init; }
        = new ReadOnlyDictionary<string, CSharpScalarMapping>(new Dictionary<string, CSharpScalarMapping>(StringComparer.Ordinal));

    /// <summary>Additional namespaces always required by generated code.</summary>
    public IReadOnlyList<string> AdditionalImports { get; init; } = [];
}

/// <summary>Default C# generation policies, configurable without changing the GraphQL model.</summary>
public sealed class CSharpCodeGenerationTarget : ICodeGenerationTarget
{
    private static readonly IReadOnlyDictionary<string, CSharpScalarMapping> BuiltInScalars =
        new ReadOnlyDictionary<string, CSharpScalarMapping>(new Dictionary<string, CSharpScalarMapping>(StringComparer.Ordinal)
        {
            ["String"] = new("string", false, "\"sample\"", "\"sample\""),
            ["ID"] = new("string", false, "\"sample\"", "\"sample\""),
            ["URL"] = new("string", false, "\"https://example.com\"", "\"https://example.com\""),
            ["Int"] = new("int", true, "123", "123"),
            ["Float"] = new("double", true, "12.34", "12.34"),
            ["Boolean"] = new("bool", true, "true", "true"),
            ["Long"] = new("long", true, "123L", "123"),
            ["Decimal"] = new("decimal", true, "12.34m", "12.34"),
            ["DateTime"] = new("DateTime", true, "DateTime.UnixEpoch", "\"1970-01-01T00:00:00Z\""),
            ["Instant"] = new("DateTimeOffset", true, "DateTimeOffset.UnixEpoch", "\"1970-01-01T00:00:00Z\""),
            ["UUID"] = new("Guid", true, "Guid.Empty", "\"00000000-0000-0000-0000-000000000000\"")
        });

    private readonly CSharpCodeGenerationOptions options;

    /// <summary>Creates the C# target with default policies and optional consumer overrides.</summary>
    public CSharpCodeGenerationTarget(CSharpCodeGenerationOptions? options = null)
    {
        this.options = options ?? new CSharpCodeGenerationOptions();
        if (string.IsNullOrWhiteSpace(this.options.DefaultNamespace))
        {
            throw new ArgumentException("The default C# namespace must not be empty.", nameof(options));
        }

        ArgumentNullException.ThrowIfNull(this.options.ScalarMappings);
        ArgumentNullException.ThrowIfNull(this.options.TypeOwners);
        ArgumentNullException.ThrowIfNull(this.options.AdditionalImports);
        ValidateScalarMappings(this.options.ScalarMappings);
    }

    /// <inheritdoc />
    public string Id => "csharp";

    /// <inheritdoc />
    public string DefaultNamespace => options.DefaultNamespace;

    /// <inheritdoc />
    public string TypeName(string graphQlName) => ApplyName(graphQlName, options.TypeName, PascalCase);

    /// <inheritdoc />
    public string InterfaceName(string graphQlName)
    {
        if (options.InterfaceName is not null)
        {
            return options.InterfaceName(graphQlName);
        }

        var typeName = TypeName(graphQlName);
        return typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]) ? typeName : $"I{typeName}";
    }

    /// <inheritdoc />
    public string PropertyName(string graphQlName) => ApplyName(graphQlName, options.PropertyName, PascalCase);

    /// <inheritdoc />
    public string EnumValueName(string graphQlName) => ApplyName(graphQlName, options.EnumValueName, PascalCase);

    /// <inheritdoc />
    public string MethodName(string graphQlName) => ApplyName(graphQlName, options.MethodName, PascalCase);

    /// <inheritdoc />
    public string ParameterName(string graphQlName) => ApplyName(graphQlName, options.ParameterName, CamelCase);

    /// <inheritdoc />
    public string StringLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ when char.IsControl(character) => $"\\u{(int)character:x4}",
                _ => character.ToString()
            });
        }

        return builder.Append('"').ToString();
    }

    /// <summary>Indicates whether union declarations should use the C# native union syntax.</summary>
    public bool UseNativeUnions => options.UseNativeUnions;

    /// <summary>Returns a configured or built-in scalar mapping, when one exists.</summary>
    public bool TryGetScalar(string graphQlName, out CSharpScalarMapping mapping)
        => TryGetScalarMapping(graphQlName, out mapping!);

    /// <summary>Finds the configured namespace owning a shared generated type.</summary>
    public string TypeOwner(string graphQlTypeName) => options.TypeOwners.GetValueOrDefault(TypeName(graphQlTypeName), DefaultNamespace);

    /// <summary>Finds the configured namespace owning a generated interface contract.</summary>
    public string InterfaceOwner(string graphQlTypeName)
    {
        var interfaceName = InterfaceName(graphQlTypeName);
        return options.TypeOwners.GetValueOrDefault(interfaceName,
            options.TypeOwners.GetValueOrDefault(TypeName(graphQlTypeName) + "Result", DefaultNamespace));
    }

    /// <inheritdoc />
    public string RenderType(GraphQlTypeReference type, GraphQlSchemaModel schema)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(schema);
        return RenderTypeCore(type, schema, nullable: true);
    }

    /// <inheritdoc />
    public bool IsValueType(GraphQlTypeReference type, GraphQlSchemaModel schema)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(schema);
        while (type is GraphQlNonNullTypeReference nonNull)
        {
            type = nonNull.NullableType;
        }

        if (type is not GraphQlNamedTypeReference named)
        {
            return false;
        }

        if (TryGetScalarMapping(named.Name, out var mapping))
        {
            return mapping.IsValueType;
        }

        return schema.EnumTypes.Any(item => StringComparer.Ordinal.Equals(item.Name, named.Name));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetImports(GraphQlSchemaModel schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var imports = new HashSet<string>(options.AdditionalImports, StringComparer.Ordinal)
        {
            "System",
            "System.Collections.Generic",
            "System.Text.Json.Serialization"
        };

        return imports.Order(StringComparer.Ordinal).ToArray();
    }

    private string RenderTypeCore(GraphQlTypeReference type, GraphQlSchemaModel schema, bool nullable)
    {
        switch (type)
        {
            case GraphQlNonNullTypeReference nonNull:
                return RenderTypeCore(nonNull.NullableType, schema, nullable: false);
            case GraphQlListTypeReference list:
                return $"List<{RenderTypeCore(list.ElementType, schema, nullable: true)}>" + (nullable ? "?" : string.Empty);
            case GraphQlNamedTypeReference named:
                return NamedType(named.Name, schema) + (nullable ? "?" : string.Empty);
            default:
                throw new NotSupportedException($"Unsupported GraphQL type reference '{type.GetType().Name}'.");
        }
    }

    private string NamedType(string name, GraphQlSchemaModel schema)
    {
        if (TryGetScalarMapping(name, out var mapping))
        {
            return mapping.TypeName;
        }

        if (schema.InterfaceTypes.Any(type => StringComparer.Ordinal.Equals(type.Name, name)))
        {
            return OwnedType(TypeName(name) + "Result");
        }

        if (schema.ScalarTypes.Any(type => StringComparer.Ordinal.Equals(type.Name, name)))
        {
            return "string";
        }

        if (schema.Types.Any(type => StringComparer.Ordinal.Equals(type.Name, name)))
        {
            return OwnedType(TypeName(name));
        }

        return TypeName(name);
    }

    private string OwnedType(string generatedName)
    {
        var owner = options.TypeOwners.GetValueOrDefault(generatedName, DefaultNamespace);
        return StringComparer.Ordinal.Equals(owner, DefaultNamespace) ? generatedName : $"global::{owner}.{generatedName}";
    }

    private bool TryGetScalarMapping(string name, out CSharpScalarMapping mapping)
    {
        if (options.ScalarMappings.TryGetValue(name, out mapping!))
        {
            return true;
        }

        return BuiltInScalars.TryGetValue(name, out mapping!);
    }

    private static string ApplyName(string value, Func<string, string>? custom, Func<string, string> fallback)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = custom is null ? fallback(value) : custom(value);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException("A C# naming policy returned an empty identifier.");
        }

        return result;
    }

    private static string PascalCase(string value) => CSharpIdentifier(value, upperFirst: true);
    private static string CamelCase(string value) => CSharpIdentifier(value, upperFirst: false);

    private static string CSharpIdentifier(string value, bool upperFirst)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        var builder = new StringBuilder(value.Length + 1);
        var upperNext = upperFirst;
        foreach (var character in value)
        {
            if (character is '_' or '-')
            {
                upperNext = true;
                continue;
            }

            builder.Append(upperNext ? char.ToUpperInvariant(character) : character);
            upperNext = false;
        }

        var result = builder.ToString();
        if (char.IsDigit(result[0]))
        {
            result = $"_{result}";
        }

        return IsKeyword(result) ? $"@{result}" : result;
    }

    private static bool IsKeyword(string value) => value is
        "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or "char" or
        "checked" or "class" or "const" or "continue" or "decimal" or "default" or "delegate" or "do" or
        "double" or "else" or "enum" or "event" or "explicit" or "extern" or "false" or "finally" or "fixed" or
        "float" or "for" or "foreach" or "goto" or "if" or "implicit" or "in" or "int" or "interface" or
        "internal" or "is" or "lock" or "long" or "namespace" or "new" or "null" or "object" or "operator" or
        "out" or "override" or "params" or "private" or "protected" or "public" or "readonly" or "ref" or
        "return" or "sbyte" or "sealed" or "short" or "sizeof" or "stackalloc" or "static" or "string" or
        "struct" or "switch" or "this" or "throw" or "true" or "try" or "typeof" or "uint" or "ulong" or
        "unchecked" or "unsafe" or "ushort" or "using" or "virtual" or "void" or "volatile" or "while";

    private static void ValidateScalarMappings(IReadOnlyDictionary<string, CSharpScalarMapping> mappings)
    {
        foreach (var (name, mapping) in mappings)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(mapping.TypeName) ||
                mapping.SampleExpression is not null && string.IsNullOrWhiteSpace(mapping.SampleExpression))
            {
                throw new ArgumentException("Scalar mappings require non-empty GraphQL and C# type names and non-blank sample expressions.", nameof(mappings));
            }

            if (mapping.SampleJson is not null)
            {
                try { using var _ = JsonDocument.Parse(mapping.SampleJson); }
                catch (JsonException exception) { throw new ArgumentException($"Scalar '{name}' has invalid sample JSON.", nameof(mappings), exception); }
            }
        }
    }
}
