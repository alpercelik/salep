using System.Text;

namespace GraphQLParser;

/// <summary>Formats GraphQL quoted string literals.</summary>
public static class GraphQLString
{
    /// <summary>Escapes a value as a valid quoted GraphQL string literal.</summary>
    public static string PrintString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var output = new StringBuilder(value.Length + 2);
        output.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\t': output.Append("\\t"); break;
                case '\n': output.Append("\\n"); break;
                case '\f': output.Append("\\f"); break;
                case '\r': output.Append("\\r"); break;
                default:
                    if (character <= 0x1f || character is >= '\u007f' and <= '\u009f')
                    {
                        output.Append("\\u");
                        output.Append(((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        output.Append(character);
                    }

                    break;
            }
        }

        output.Append('"');
        return output.ToString();
    }
}
