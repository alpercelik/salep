using System.Net.Http;
using System.Text;
using System.Text.Json;
using Salep.ClientGenerator.Generation;

namespace Salep.ClientGenerator.TypeScript.Generation;

/// <summary>A loaded SDL source and its manifest provenance.</summary>
public sealed record TypeScriptSchemaSource(string Name, string Text, string? LocalPath, string? RemoteId, bool Converted = false);

/// <summary>Loads local SDL/introspection files and explicit remote schema endpoints.</summary>
public static class TypeScriptSchemaLoader
{
    private const int MaximumBytes = 16 * 1024 * 1024;
    public static IReadOnlyList<TypeScriptSchemaSource> Load(JsonElement value, string directory, GeneratorPathPolicy paths, HttpClient? httpClient = null)
    {
        var result = new List<TypeScriptSchemaSource>();
        var entries = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [value];
        if (entries.Length == 0) throw new InvalidDataException("schema must contain at least one source.");
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.ValueKind == JsonValueKind.String)
            {
                var name = entry.GetString()!;
                if (Uri.TryCreate(name, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                    result.Add(Remote(uri, null, index, httpClient));
                else
                {
                    var file = paths.Read(Path.GetFullPath(GeneratorPathPolicy.Normalize(name), directory));
                    var text = File.ReadAllText(file);
                    try { result.Add(new(GeneratorPathPolicy.Normalize(file), ConvertSource(text), file, null, text.AsSpan().TrimStart().StartsWith("{"))); }
                    catch (Exception error) when (error is JsonException or InvalidDataException)
                    {
                        throw new InvalidDataException(GeneratorPathPolicy.Normalize(Path.GetRelativePath(directory, file)) + ": invalid schema source: " + error.Message, error);
                    }
                }
            }
            else if (entry.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in entry.EnumerateObject())
                    if (!names.Add(property.Name) || property.Name is not ("url" or "method" or "headers")) throw new InvalidDataException($"Unknown or duplicate remote schema property '{property.Name}'.");
                if (!Uri.TryCreate(entry.GetProperty("url").GetString(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
                    throw new InvalidDataException("Remote schemas require an HTTP(S) URL without embedded credentials.");
                result.Add(Remote(uri, entry, index, httpClient));
            }
            else throw new InvalidDataException("schema accepts a path, URL, remote descriptor or array of sources.");
        }
        return result;
    }

    private static TypeScriptSchemaSource Remote(Uri uri, JsonElement? descriptor, int index, HttpClient? suppliedClient)
    {
        if (!string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Use remote schema headers instead of URL credentials.");
        var method = descriptor is { } config && config.TryGetProperty("method", out var configured) ? configured.GetString() : "get";
        if (method is not ("get" or "introspection")) throw new InvalidDataException("Remote schema method must be 'get' or 'introspection'.");
        using var ownedClient = suppliedClient is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) } : null;
        var client = suppliedClient ?? ownedClient!;
        using var request = new HttpRequestMessage(method == "get" ? HttpMethod.Get : HttpMethod.Post, uri);
        if (method == "introspection") request.Content = new StringContent(JsonSerializer.Serialize(new { query = IntrospectionQuery() }), Encoding.UTF8, "application/json");
        if (descriptor is { } settings && settings.TryGetProperty("headers", out var headers))
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers.EnumerateObject())
            {
                if (!names.Add(header.Name)) throw new InvalidDataException("Remote schema headers must be unique.");
                request.Headers.Add(header.Name, header.Value.GetString());
            }
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) throw new InvalidDataException($"Remote schema source {index + 1} returned HTTP {(int)response.StatusCode}.");
        using var stream = response.Content.ReadAsStreamAsync(timeout.Token).GetAwaiter().GetResult();
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        for (var count = stream.ReadAsync(buffer.AsMemory(), timeout.Token).AsTask().GetAwaiter().GetResult(); count > 0; count = stream.ReadAsync(buffer.AsMemory(), timeout.Token).AsTask().GetAwaiter().GetResult())
        {
            if (memory.Length + count > MaximumBytes) throw new InvalidDataException("Remote schema exceeds the 16 MiB size limit.");
            memory.Write(buffer, 0, count);
        }
        memory.Position = 0;
        using var reader = new StreamReader(memory, Encoding.UTF8, true);
        var text = ConvertSource(reader.ReadToEnd());
        return new($"remote schema source {index + 1}", text, null, "schema/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static string ConvertSource(string source)
    {
        if (!source.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal)) return source;
        using var json = JsonDocument.Parse(source);
        var root = json.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() != 0) throw new InvalidDataException("Introspection response contains GraphQL errors.");
        if (root.TryGetProperty("data", out var data)) root = data;
        if (root.TryGetProperty("__schema", out var schema)) root = schema;
        if (!root.TryGetProperty("types", out var types)) throw new InvalidDataException("Expected introspection JSON containing __schema.types.");
        var output = new StringBuilder("schema {").AppendLine();
        foreach (var operation in new[] { "query", "mutation", "subscription" })
            if (root.TryGetProperty(operation + "Type", out var type) && type.ValueKind != JsonValueKind.Null)
                output.Append("  ").Append(operation).Append(": ").Append(type.GetProperty("name").GetString()).AppendLine();
        output.AppendLine("}");
        foreach (var type in types.EnumerateArray())
        {
            var name = type.GetProperty("name").GetString()!;
            if (name.StartsWith("__", StringComparison.Ordinal) || name is "String" or "Int" or "Float" or "Boolean" or "ID") continue;
            Description(output, type);
            switch (type.GetProperty("kind").GetString())
            {
                case "SCALAR":
                    output.Append("scalar ").Append(name);
                    if (type.TryGetProperty("specifiedByURL", out var url) && url.ValueKind == JsonValueKind.String) output.Append(" @specifiedBy(url: ").Append(JsonSerializer.Serialize(url.GetString())).Append(')');
                    output.AppendLine();
                    break;
                case "ENUM":
                    output.Append("enum ").Append(name).AppendLine(" {");
                    foreach (var value in type.GetProperty("enumValues").EnumerateArray()) { Description(output, value); output.Append(value.GetProperty("name").GetString()); Deprecated(output, value); output.AppendLine(); }
                    output.AppendLine("}");
                    break;
                case "INPUT_OBJECT":
                    output.Append("input ").Append(name);
                    if (type.TryGetProperty("isOneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.True) output.Append(" @oneOf");
                    output.AppendLine(" {");
                    foreach (var field in type.GetProperty("inputFields").EnumerateArray()) { Description(output, field); Input(output, field); output.AppendLine(); }
                    output.AppendLine("}");
                    break;
                case "UNION":
                    output.Append("union ").Append(name).Append(" = ").AppendJoin(" | ", type.GetProperty("possibleTypes").EnumerateArray().Select(member => member.GetProperty("name").GetString())).AppendLine();
                    break;
                case "OBJECT": case "INTERFACE":
                    output.Append(type.GetProperty("kind").GetString() == "OBJECT" ? "type " : "interface ").Append(name);
                    var interfaces = type.TryGetProperty("interfaces", out var faces) && faces.ValueKind == JsonValueKind.Array ? faces.EnumerateArray().Select(face => face.GetProperty("name").GetString()).ToArray() : [];
                    if (interfaces.Length > 0) output.Append(" implements ").AppendJoin(" & ", interfaces);
                    output.AppendLine(" {");
                    foreach (var field in type.GetProperty("fields").EnumerateArray())
                    {
                        Description(output, field); output.Append(field.GetProperty("name").GetString());
                        Arguments(output, field.GetProperty("args"));
                        output.Append(": ").Append(Type(field.GetProperty("type"))); Deprecated(output, field); output.AppendLine();
                    }
                    output.AppendLine("}");
                    break;
                default: throw new InvalidDataException("Unknown introspection type kind.");
            }
        }
        if (root.TryGetProperty("directives", out var directives))
            foreach (var directive in directives.EnumerateArray())
            {
                var name = directive.GetProperty("name").GetString();
                if (name is "skip" or "include" or "deprecated" or "specifiedBy" or "oneOf") continue;
                Description(output, directive); output.Append("directive @").Append(name); Arguments(output, directive.GetProperty("args"));
                if (directive.TryGetProperty("isRepeatable", out var repeatable) && repeatable.ValueKind == JsonValueKind.True) output.Append(" repeatable");
                output.Append(" on ").AppendJoin(" | ", directive.GetProperty("locations").EnumerateArray().Select(location => location.GetString())).AppendLine();
            }
        return output.ToString();
    }
    private static string Type(JsonElement type) => type.GetProperty("kind").GetString() switch
    {
        "NON_NULL" => Type(type.GetProperty("ofType")) + "!", "LIST" => "[" + Type(type.GetProperty("ofType")) + "]",
        _ => type.GetProperty("name").GetString() ?? throw new InvalidDataException("Missing named introspection type.")
    };
    private static void Description(StringBuilder output, JsonElement value)
    {
        if (value.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String) output.AppendLine(JsonSerializer.Serialize(description.GetString()));
    }
    private static void Deprecated(StringBuilder output, JsonElement value)
    {
        if (value.TryGetProperty("isDeprecated", out var deprecated) && deprecated.ValueKind == JsonValueKind.True)
            output.Append(" @deprecated(reason: ").Append(JsonSerializer.Serialize(value.TryGetProperty("deprecationReason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : "No longer supported")).Append(')');
    }
    private static void Input(StringBuilder output, JsonElement value)
    {
        output.Append(value.GetProperty("name").GetString()).Append(": ").Append(Type(value.GetProperty("type")));
        if (value.TryGetProperty("defaultValue", out var fallback) && fallback.ValueKind == JsonValueKind.String) output.Append(" = ").Append(fallback.GetString());
        Deprecated(output, value);
    }
    private static void Arguments(StringBuilder output, JsonElement args)
    {
        if (args.GetArrayLength() == 0) return;
        output.Append('(');
        foreach (var arg in args.EnumerateArray()) { Description(output, arg); Input(output, arg); output.AppendLine(); }
        output.Append(')');
    }
    private static string IntrospectionQuery()
    {
        string Reference(int depth) => depth == 0 ? "kind name" : "kind name ofType { " + Reference(depth - 1) + " }";
        var reference = Reference(16);
        var input = "name description defaultValue isDeprecated deprecationReason type { " + reference + " }";
        return "query SalepSchema { __schema { queryType { name } mutationType { name } subscriptionType { name } types { kind name description specifiedByURL isOneOf interfaces { name } possibleTypes { name } fields(includeDeprecated: true) { name description isDeprecated deprecationReason args(includeDeprecated: true) { " + input + " } type { " + reference + " } } inputFields(includeDeprecated: true) { " + input + " } enumValues(includeDeprecated: true) { name description isDeprecated deprecationReason } } directives { name description isRepeatable locations args(includeDeprecated: true) { " + input + " } } } }";
    }
}
