namespace Salep.Parser.Buffers;

/// <summary>A read-only slice over caller-owned character memory.</summary>
/// <remarks>The owner must keep the backing memory alive and unchanged while consumers use this segment.</remarks>
public readonly struct ReadOnlyMemorySegment : IEquatable<ReadOnlyMemorySegment>
{
    private readonly ReadOnlyMemory<char> _memory;

    /// <summary>Creates a segment over all supplied memory.</summary>
    public ReadOnlyMemorySegment(ReadOnlyMemory<char> memory) => _memory = memory;

    /// <summary>Creates a segment over a range of supplied memory.</summary>
    public ReadOnlyMemorySegment(ReadOnlyMemory<char> memory, int start, int length)
    {
        if ((uint)start > (uint)memory.Length) throw new ArgumentOutOfRangeException(nameof(start));
        if ((uint)length > (uint)(memory.Length - start)) throw new ArgumentOutOfRangeException(nameof(length));
        _memory = memory.Slice(start, length);
    }

    /// <summary>Gets the represented memory.</summary>
    public ReadOnlyMemory<char> Memory => _memory;
    /// <summary>Gets the represented character span.</summary>
    public ReadOnlySpan<char> Span => _memory.Span;
    /// <summary>Gets the number of represented characters.</summary>
    public int Length => _memory.Length;
    /// <summary>Gets whether the segment is empty.</summary>
    public bool IsEmpty => _memory.IsEmpty;

    /// <summary>Returns a subslice of this segment.</summary>
    public ReadOnlyMemorySegment Slice(int start) => new(_memory[start..]);
    /// <summary>Returns a subslice of this segment.</summary>
    public ReadOnlyMemorySegment Slice(int start, int length) => new(_memory, start, length);
    /// <summary>Returns the represented memory as a string.</summary>
    public override string ToString() => _memory.ToString();
    /// <summary>Compares represented characters by value.</summary>
    public bool Equals(ReadOnlyMemorySegment other) => Span.SequenceEqual(other.Span);
    /// <summary>Compares represented characters by value.</summary>
    public override bool Equals(object? obj) => obj is ReadOnlyMemorySegment other && Equals(other);
    /// <summary>Returns a content-based hash code.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in Span) hash.Add(value);
        return hash.ToHashCode();
    }

    /// <summary>Creates a segment over the supplied memory.</summary>
    public static implicit operator ReadOnlyMemorySegment(ReadOnlyMemory<char> memory) => new(memory);
    /// <summary>Creates a segment over the supplied string.</summary>
    public static implicit operator ReadOnlyMemorySegment(string value) => new((value ?? throw new ArgumentNullException(nameof(value))).AsMemory());
    /// <summary>Returns the represented memory.</summary>
    public static implicit operator ReadOnlyMemory<char>(ReadOnlyMemorySegment segment) => segment._memory;
    /// <summary>Compares segment content.</summary>
    public static bool operator ==(ReadOnlyMemorySegment left, ReadOnlyMemorySegment right) => left.Equals(right);
    /// <summary>Compares segment content.</summary>
    public static bool operator !=(ReadOnlyMemorySegment left, ReadOnlyMemorySegment right) => !left.Equals(right);
}
