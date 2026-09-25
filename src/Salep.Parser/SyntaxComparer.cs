using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Salep.Parser;

/// <summary>Selects how syntax nodes are compared.</summary>
public enum SyntaxComparison
{
    /// <summary>Compares node references.</summary>
    Reference = 0,
    /// <summary>Compares syntax structure and values while ignoring source locations.</summary>
    Syntax = 1,
    /// <summary>Compares syntax structure and values while ignoring source locations and descriptions.</summary>
    SyntaxIgnoreDescriptions = 2
}

/// <summary>Provides equality comparers for syntax nodes.</summary>
public abstract class SyntaxComparer
{
    /// <summary>Gets a comparer that compares node references.</summary>
    public static IEqualityComparer<ISyntaxNode> ByReference { get; } = new ReferenceNodeComparer();
    /// <summary>Gets a comparer that compares syntax structure and values.</summary>
    public static IEqualityComparer<ISyntaxNode> BySyntax { get; } = new StructuralNodeComparer(false);
    /// <summary>Gets a comparer that ignores descriptions.</summary>
    public static IEqualityComparer<ISyntaxNode> BySyntaxIgnoreDescriptions { get; } = new StructuralNodeComparer(true);

    private SyntaxComparer() { }

    private sealed class ReferenceNodeComparer : IEqualityComparer<ISyntaxNode>
    {
        public bool Equals(ISyntaxNode? x, ISyntaxNode? y) => ReferenceEquals(x, y);
        public int GetHashCode(ISyntaxNode obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class StructuralNodeComparer(bool ignoreDescriptions) : IEqualityComparer<ISyntaxNode>
    {
        public bool Equals(ISyntaxNode? x, ISyntaxNode? y) => AreEqual(x, y, ignoreDescriptions);
        public int GetHashCode(ISyntaxNode obj) => StructuralHash(obj, ignoreDescriptions);
    }

    private static bool AreEqual(object? left, object? right, bool ignoreDescriptions)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.GetType() != right.GetType()) return false;
        if (left is string || left.GetType().IsValueType) return left.Equals(right);

        if (left is IEnumerable leftItems && right is IEnumerable rightItems)
        {
            var leftEnumerator = leftItems.GetEnumerator();
            var rightEnumerator = rightItems.GetEnumerator();
            try
            {
                while (true)
                {
                    var hasLeft = leftEnumerator.MoveNext();
                    var hasRight = rightEnumerator.MoveNext();
                    if (hasLeft != hasRight) return false;
                    if (!hasLeft) return true;
                    if (!AreEqual(leftEnumerator.Current, rightEnumerator.Current, ignoreDescriptions)) return false;
                }
            }
            finally
            {
                (leftEnumerator as IDisposable)?.Dispose();
                (rightEnumerator as IDisposable)?.Dispose();
            }
        }

        var properties = GetComparableProperties(left.GetType(), ignoreDescriptions);
        foreach (var property in properties)
        {
            if (!AreEqual(property.GetValue(left), property.GetValue(right), ignoreDescriptions))
            {
                return false;
            }
        }
        return properties.Length != 0 || left.Equals(right);
    }

    private static int StructuralHash(object? value, bool ignoreDescriptions)
    {
        if (value is null) return 0;
        if (value is string || value.GetType().IsValueType) return value.GetHashCode();
        if (value is IEnumerable items)
        {
            var hash = new HashCode();
            foreach (var item in items) hash.Add(StructuralHash(item, ignoreDescriptions));
            return hash.ToHashCode();
        }

        var properties = GetComparableProperties(value.GetType(), ignoreDescriptions);
        if (properties.Length == 0) return value.GetHashCode();
        var result = new HashCode();
        foreach (var property in properties)
        {
            result.Add(property.Name, StringComparer.Ordinal);
            result.Add(StructuralHash(property.GetValue(value), ignoreDescriptions));
        }
        return result.ToHashCode();
    }

    private static PropertyInfo[] GetComparableProperties(Type type, bool ignoreDescriptions) => type
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.CanRead && property.GetIndexParameters().Length == 0
            && property.Name is not ("Location" or "Kind" or "AstKind" or "HasLocation" or "SourceValue" or "Source" or "SourceInfo")
            && (!ignoreDescriptions || property.Name != "Description"))
        .OrderBy(property => property.Name, StringComparer.Ordinal)
        .ToArray();
}
