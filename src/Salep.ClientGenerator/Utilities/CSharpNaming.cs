using System.Text;

namespace Salep.ClientGenerator.Utilities;

internal static class CSharpNaming
{
    public static string ToTypeName(string name) => ToCSharpIdentifier(name, pascalCase: true);

    public static string ToInterfaceName(string name)
    {
        var typeName = ToTypeName(name);
        if (typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
        {
            return typeName;
        }

        return $"I{typeName}";
    }

    public static string ToPropertyName(string name) => ToCSharpIdentifier(name, pascalCase: true);

    public static string ToEnumMemberName(string name) => ToCSharpIdentifier(name, pascalCase: true);

    private static string ToCSharpIdentifier(string name, bool pascalCase)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "_";
        }

        var builder = new StringBuilder(name.Length + 1);
        var nextUpper = pascalCase;
        foreach (var ch in name)
        {
            if (ch is '_' or '-')
            {
                nextUpper = true;
                continue;
            }

            var outChar = nextUpper ? char.ToUpperInvariant(ch) : ch;
            builder.Append(outChar);
            nextUpper = false;
        }

        var result = builder.ToString();
        
        if (char.IsDigit(result[0]))
        {
            result = $"_{result}";
        }

        return IsCSharpKeyword(result) ? $"@{result}" : result;
    }

    private static bool IsCSharpKeyword(string identifier)
    {
        return identifier switch
        {
            "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or
            "char" or "checked" or "class" or "const" or "continue" or "decimal" or "default" or
            "delegate" or "do" or "double" or "else" or "enum" or "event" or "explicit" or "extern" or
            "false" or "finally" or "fixed" or "float" or "for" or "foreach" or "goto" or "if" or
            "implicit" or "in" or "int" or "interface" or "internal" or "is" or "lock" or "long" or
            "namespace" or "new" or "null" or "object" or "operator" or "out" or "override" or "params" or
            "private" or "protected" or "public" or "readonly" or "ref" or "return" or "sbyte" or "sealed" or
            "short" or "sizeof" or "stackalloc" or "static" or "string" or "struct" or "switch" or
            "this" or "throw" or "true" or "try" or "typeof" or "uint" or "ulong" or "unchecked" or "unsafe" or
            "ushort" or "using" or "virtual" or "void" or "volatile" or "while" => true,
            _ => false
        };
    }
}
