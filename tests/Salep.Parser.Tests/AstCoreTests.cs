using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Salep.Parser;
using Xunit;

namespace Salep.Parser.Tests;

public sealed class AstCoreTests
{
    [Fact]
    public void SourceLocationsAreValidatedHalfOpenUtf16Ranges()
    {
        var location = new SourceLocation(2, 7);

        Assert.Equal(2, location.Start);
        Assert.Equal(7, location.End);
        Assert.Equal(5, location.Length);
        Assert.Equal(0, new SourceLocation(4, 4).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocation(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocation(2, 1));
        Assert.Throws<ArgumentException>(() => new NameNode("short".AsMemory(), new SourceLocation(0, 2)));
    }

    [Fact]
    public void NameNodesPreserveKindLocationAndCallerOwnedMemory()
    {
        var buffer = "query".ToCharArray();
        var name = new NameNode(buffer.AsMemory(1, 3), new SourceLocation(1, 4));

        Assert.Equal(AstNodeKind.Name, name.AstKind);
        Assert.Equal(new SourceLocation(1, 4), (SourceLocation)name.Location);
        Assert.Equal("uer", name.Value.ToString());
        Assert.True(MemoryMarshal.TryGetArray(name.SourceValue, out ArraySegment<char> segment));
        Assert.Same(buffer, segment.Array);
        Assert.Equal(1, segment.Offset);
        Assert.Equal(3, segment.Count);
    }

    [Fact]
    public void ChildListsSnapshotInputAndDoNotExposeMutableCollections()
    {
        var first = new NameNode("first".AsMemory(), new SourceLocation(0, 5));
        var replacement = new NameNode("other".AsMemory(), new SourceLocation(0, 5));
        var source = new List<NameNode> { first };

        var children = new AstNodeList<NameNode>(source);
        source[0] = replacement;
        source.Add(replacement);

        Assert.Same(first, Assert.Single(children));
    }

    [Fact]
    public void ChildListsRejectNullInputsAndProvideAnEmptySingleton()
    {
        Assert.Empty(AstNodeList<NameNode>.Empty);
        Assert.Throws<ArgumentNullException>(() => new AstNodeList<NameNode>(null!));
        Assert.Throws<ArgumentNullException>(() => new AstNodeList<NameNode>(new NameNode[] { null! }));
    }

    [Fact]
    public void ReturnedNodesKeepTheirSourceMemoryAliveAfterTheSourceViewLeavesScope()
    {
        var name = CreateNameNodeFromTemporarySource();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal("field", name.Value.ToString());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static NameNode CreateNameNodeFromTemporarySource()
    {
        var buffer = "field".ToCharArray();
        var source = new SourceText(buffer.AsMemory());
        var lexer = new GraphQLLexer(source);
        var token = lexer.NextToken();
        return new NameNode(token.Value, new SourceLocation(token.Start, token.End));
    }
}
