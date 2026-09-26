using Xunit;

namespace Salep.GraphQLParser.Tests;

public sealed class ListExtensionsTests
{
    [Fact]
    public void PushPeekAndPopOperateOnTheListTail()
    {
        IList<string> items = new List<string> { "first" };

        ListExtensions.Push(items, "last");

        Assert.Equal("last", ListExtensions.Peek(items));
        Assert.Equal("last", ListExtensions.Pop(items));
        Assert.Equal(["first"], items);
    }

    [Fact]
    public void TryPeekSupportsOffsetsAndRejectsOutOfRangeOffsets()
    {
        IList<int> items = new List<int> { 10, 20, 30 };

        Assert.True(ListExtensions.TryPeek(items, out var top));
        Assert.Equal(30, top);
        Assert.True(ListExtensions.TryPeek(items, 2, out var bottom));
        Assert.Equal(10, bottom);
        Assert.False(ListExtensions.TryPeek(items, 3, out var missing));
        Assert.Equal(0, missing);
        Assert.False(ListExtensions.TryPeek(items, -1, out _));
    }

    [Fact]
    public void EmptyListOperationsReturnDefaultsOrThrowAsDocumented()
    {
        IList<int> items = new List<int>();

        Assert.Equal(42, ListExtensions.PeekOrDefault<int>(items, 42));
        Assert.Equal("fallback", ListExtensions.PeekOrDefault<int, string>(items, "fallback"));
        Assert.Equal(0, ListExtensions.PeekOrDefault<int>(items));
        Assert.False(ListExtensions.TryPeek(items, out var noPeek));
        Assert.Equal(0, noPeek);
        Assert.False(ListExtensions.TryPop(items, out var missing));
        Assert.Equal(0, missing);
        Assert.Throws<InvalidOperationException>(() => ListExtensions.Peek(items));
        Assert.Throws<InvalidOperationException>(() => ListExtensions.Pop(items));
    }
}
