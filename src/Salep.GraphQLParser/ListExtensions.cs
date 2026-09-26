namespace Salep.GraphQLParser;

/// <summary>Provides stack-like operations for lists.</summary>
public abstract class ListExtensions
{
    /// <summary>Returns the last item in the list.</summary>
    /// <exception cref="InvalidOperationException">The list is empty.</exception>
    public static T Peek<T>(IList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (!TryPeek(list, out var item))
            throw new InvalidOperationException("The list is empty.");
        return item;
    }

    /// <summary>Returns the last item in the list, or the supplied default when it is empty.</summary>
    public static T PeekOrDefault<T>(IList<T> list, T defaultValue = default!) =>
        TryPeek(list, out var item) ? item : defaultValue;

    /// <summary>Returns the last item in the list, or the supplied default when it is empty.</summary>
    public static TSearch PeekOrDefault<T, TSearch>(IList<T> list, TSearch defaultValue = default!)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Count == 0)
            return defaultValue;
        return (TSearch)(object?)list[list.Count - 1]!;
    }

    /// <summary>Removes and returns the last item in the list.</summary>
    /// <exception cref="InvalidOperationException">The list is empty.</exception>
    public static T Pop<T>(IList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (!TryPop(list, out var item))
            throw new InvalidOperationException("The list is empty.");
        return item;
    }

    /// <summary>Adds an item to the end of the list.</summary>
    public static void Push<T>(IList<T> list, T item)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.Add(item);
    }

    /// <summary>Attempts to read an item by its zero-based distance from the end of the list.</summary>
    public static bool TryPeek<T>(IList<T> list, int elements, out T item)
    {
        ArgumentNullException.ThrowIfNull(list);
        var index = list.Count - elements - 1;
        if (elements < 0 || index < 0)
        {
            item = default!;
            return false;
        }

        item = list[index];
        return true;
    }

    /// <summary>Attempts to read the last item in the list.</summary>
    public static bool TryPeek<T>(IList<T> list, out T item) => TryPeek(list, 0, out item);

    /// <summary>Attempts to remove and return the last item in the list.</summary>
    public static bool TryPop<T>(IList<T> list, out T item)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Count == 0)
        {
            item = default!;
            return false;
        }

        var index = list.Count - 1;
        item = list[index];
        list.RemoveAt(index);
        return true;
    }
}
