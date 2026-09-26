using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Salep.GraphQLParser;
using Salep.ClientGenerator.Model;
using Salep.ClientGenerator.Utilities;
using static Salep.ClientGenerator.Emission.Syntax.Cs;

namespace Salep.ClientGenerator.Emission.Testing;

// Fixture policies are separate from test methods and always return typed expressions.
internal sealed class SampleValues(SchemaModel schema)
{
    public ExpressionSyntax Input(ITypeNode type, int depth = 0)
    {
        if (type is NonNullTypeNode required) return Input(required.Type, depth + 1);
        if (type is ListTypeNode list) return NewCollection($"List<{schema.ResolveType(list.Type)}>", Input(list.Type, depth + 1));
        if (type is not NamedTypeNode named) return New("object");
        if (Scalar(named.Name.Value) is { } scalar) return scalar;
        if (Enumeration(named.Name.Value) is { } enumeration) return enumeration;
        if (depth > 8) return Suppress(Default);
        if (schema.InputTypes.TryGetValue(named.Name.Value, out var input))
            return NewObject(CSharpNaming.ToTypeName(named.Name.Value), input.Fields.Select(field => Assign(CSharpNaming.ToPropertyName(field.Name.Value), Input(field.Type, depth + 1))));
        return New(CSharpNaming.ToTypeName(named.Name.Value));
    }

    public ExpressionSyntax Object(string name) => Object(name, 0, new HashSet<string>(StringComparer.Ordinal));
    private ExpressionSyntax Object(string name, int depth, HashSet<string> seen)
    {
        if (!seen.Add(name)) return Suppress(Default);
        try
        {
            var fields = schema.ObjectTypes.TryGetValue(name, out var obj)
                ? obj.Fields.Select(field => (Name: field.Name.Value, field.Type))
                : schema.InputTypes.TryGetValue(name, out var input) ? input.Fields.Select(field => (Name: field.Name.Value, field.Type)) : [];
            return NewObject(CSharpNaming.ToTypeName(name), fields.Where(field => field.Type is NonNullTypeNode).Select(field =>
                Assign(CSharpNaming.ToPropertyName(field.Name), FieldValue(field.Type, depth + 1, seen, depth > 4))));
        }
        finally { seen.Remove(name); }
    }
    private ExpressionSyntax FieldValue(ITypeNode type, int depth, HashSet<string> seen, bool fallback)
    {
        if (type is NonNullTypeNode required) return FieldValue(required.Type, depth, seen, fallback);
        if (type is ListTypeNode list) return fallback ? New($"List<{schema.ResolveType(list.Type)}>")
            : NewCollection($"List<{schema.ResolveType(list.Type)}>", FieldValue(list.Type, depth, seen, false));
        if (type is not NamedTypeNode named) return Null;
        var name = named.Name.Value;
        if (Enumeration(name) is { } enumeration) return enumeration;
        if (Scalar(name) is { } scalar) return scalar;
        if (schema.UnionTypes.TryGetValue(name, out var union))
        {
            if (fallback || union.Types.FirstOrDefault() is not { } member) return Suppress(Default);
            var value = Object(member.Name.Value, depth + 1, seen);
            return schema.Config.UseNativeUnions ? SyntaxFactory.CastExpression(Type(CSharpNaming.ToTypeName(name)), SyntaxFactory.ParenthesizedExpression(value))
                : New($"{CSharpNaming.ToTypeName(name)}.{CSharpNaming.ToTypeName(member.Name.Value)}", value);
        }
        if (schema.InterfaceTypes.ContainsKey(name))
        {
            var implementation = schema.GetInterfaceImplementations(name).FirstOrDefault();
            return implementation is null || fallback ? Suppress(Default) : Object(implementation.Name.Value, depth + 1, seen);
        }
        return Object(name, depth + 1, seen);
    }
    private ExpressionSyntax? Enumeration(string name) => schema.EnumTypes.TryGetValue(name, out var enumeration)
        ? enumeration.Values.FirstOrDefault() is { } first ? Name($"{CSharpNaming.ToTypeName(name)}.{CSharpNaming.ToEnumMemberName(first.Name.Value)}") : Number(0)
        : null;
    private ExpressionSyntax? Scalar(string name)
    {
        if (schema.Config.TryGetScalarSampleExpression(name, out var expression)) return ConfiguredExpression(expression);
        return schema.ScalarTypes.Contains(name) ? String("sample") : null;
    }
}
