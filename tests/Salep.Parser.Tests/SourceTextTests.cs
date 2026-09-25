using System.Runtime.InteropServices;
using Salep.Parser;
using Xunit;

namespace Salep.Parser.Tests;

public sealed class SourceTextTests
{
    [Fact]
    public void ConstructorAndSliceKeepTheCallerOwnedBuffer()
    {
        var buffer = "query { field }".ToCharArray();
        var source = new SourceText(buffer.AsMemory());

        Assert.Equal(buffer.Length, source.Length);
        Assert.Equal("{ field }", source.Slice(6, 9).ToString());
        Assert.True(MemoryMarshal.TryGetArray(source.Slice(0, source.Length), out ArraySegment<char> segment));
        Assert.Same(buffer, segment.Array);
    }

    [Fact]
    public void SliceRejectsRangesOutsideTheSource()
    {
        var source = new SourceText("query".AsMemory());

        Assert.Throws<ArgumentOutOfRangeException>(() => source.Slice(4, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Slice(-1, 1));
    }
}
