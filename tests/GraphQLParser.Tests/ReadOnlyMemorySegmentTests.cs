using System.Runtime.InteropServices;
using GraphQLParser;
using GraphQLParser.Buffers;
using Xunit;

namespace GraphQLParser.Tests;

public sealed class ReadOnlyMemorySegmentTests
{
    [Fact]
    public void SegmentSlicesAndLiteralNodesRetainTheCallerOwnedBackingMemory()
    {
        var backing = "--123.5e2ENUM".ToCharArray();
        ReadOnlyMemory<char> memory = backing;
        var integer = new ReadOnlyMemorySegment(memory, 2, 3);
        var floating = new ReadOnlyMemorySegment(memory, 5, 4);
        var enumValue = new ReadOnlyMemorySegment(memory, 9, 4);

        Assert.Equal("123", integer.ToString());
        Assert.Equal(".5e2", floating.ToString());
        Assert.Equal("ENUM", enumValue.ToString());
        Assert.Same(backing, GetArray(integer.Memory));

        var integerNode = new IntValueNode(integer);
        var floatNode = new FloatValueNode(floating, FloatFormat.FixedPoint);
        var enumNode = new EnumValueNode(enumValue);
        Assert.Equal(integer, integerNode.AsMemorySegment());
        Assert.Equal(floating, floatNode.AsMemorySegment());
        Assert.Equal(enumValue, enumNode.AsMemorySegment());
        Assert.Equal("123", integerNode.Value);
        Assert.Equal(".5e2", floatNode.Value);
        Assert.Equal("ENUM", enumNode.Value);

        Assert.Equal("456", integerNode.WithValue(new ReadOnlyMemorySegment("456".AsMemory())).Value);
        Assert.Equal(FloatFormat.Exponential,
            floatNode.WithValue(new ReadOnlyMemorySegment("1.5".AsMemory()), FloatFormat.Exponential).Format);
        Assert.Equal(FloatFormat.Exponential,
            new FloatValueNode(floating, FloatFormat.Exponential).WithLocation(new Location(1, 5, 1, 2)).Format);
    }

    [Fact]
    public void StringSegmentPreservesBlockStyleAndLocation()
    {
        var backing = "prefix content suffix".AsMemory();
        var segment = new ReadOnlyMemorySegment(backing, 7, 7);
        var location = new Location(10, 20, 2, 4);
        var value = new StringValueNode(location, segment, block: true);

        Assert.Equal("content", value.Value);
        Assert.True(value.IsBlock);
        Assert.Equal(location, value.Location);
        Assert.Equal(segment, value.AsMemorySegment());
    }

    [Fact]
    public void SegmentRejectsInvalidRangesAndComparesContents()
    {
        var memory = "abc".AsMemory();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReadOnlyMemorySegment(memory, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReadOnlyMemorySegment(memory, 2, 2));
        Assert.Equal(new ReadOnlyMemorySegment("same".AsMemory()), new ReadOnlyMemorySegment("same".AsMemory()));
    }

    private static char[]? GetArray(ReadOnlyMemory<char> memory) =>
        MemoryMarshal.TryGetArray(memory, out ArraySegment<char> segment) ? segment.Array : null;
}
