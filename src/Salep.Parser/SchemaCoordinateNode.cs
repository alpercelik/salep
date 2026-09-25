namespace Salep.Parser;

/// <summary>Represents a type, member, argument, or directive schema coordinate.</summary>
public class SchemaCoordinateNode : AstNode
{
    /// <summary>Creates a schema coordinate from its components.</summary>
    public SchemaCoordinateNode(Location location, bool ofDirective, NameNode name, NameNode? memberName, NameNode? argumentName)
        : this(DetermineKind(ofDirective, memberName, argumentName), (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))),
            name, memberName, argumentName, ofDirective)
    {
    }

    /// <summary>Creates a schema coordinate with its explicit syntax kind and components.</summary>
    protected SchemaCoordinateNode(AstNodeKind kind, SourceLocation location, NameNode name, NameNode? memberName, NameNode? argumentName, bool ofDirective)
        : base(kind, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (argumentName is not null && !ofDirective && memberName is null)
            throw new ArgumentException("An argument coordinate requires a member name.", nameof(argumentName));
        Name = name;
        MemberName = memberName;
        ArgumentName = argumentName;
        OfDirective = ofDirective;
    }

    /// <summary>Gets the coordinate's type or directive name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets the optional member or field name.</summary>
    public NameNode? MemberName { get; }
    /// <summary>Gets the optional argument name.</summary>
    public NameNode? ArgumentName { get; }
    /// <summary>Gets whether this coordinate refers to a directive.</summary>
    public bool OfDirective { get; }

    /// <summary>Returns a copy with a different argument name.</summary>
    public SchemaCoordinateNode WithArgumentName(NameNode argumentName) => Recreate(Name, MemberName, argumentName, Location);
    /// <summary>Returns a copy with a different source location.</summary>
    public SchemaCoordinateNode WithLocation(Location location) => Recreate(Name, MemberName, ArgumentName, (SourceLocation)(location ?? throw new ArgumentNullException(nameof(location))));
    /// <summary>Returns a copy with a different member name.</summary>
    public SchemaCoordinateNode WithMemberName(NameNode memberName) => Recreate(Name, memberName, ArgumentName, Location);
    /// <summary>Returns a copy with a different name.</summary>
    public SchemaCoordinateNode WithName(NameNode name) => Recreate(name, MemberName, ArgumentName, Location);
    /// <summary>Returns a copy with directive-coordinate mode changed.</summary>
    public SchemaCoordinateNode WithOfDirective(bool ofDirective) => RecreateWithMode(ofDirective);

    private SchemaCoordinateNode Recreate(NameNode name, NameNode? memberName, NameNode? argumentName, SourceLocation location) =>
        new(new Location(location.Start, location.End, location.Line, location.Column), OfDirective, name, memberName, argumentName);

    private SchemaCoordinateNode RecreateWithMode(bool ofDirective)
    {
        return new SchemaCoordinateNode(new Location(Location.Start, Location.End, Location.Line, Location.Column), ofDirective, Name, MemberName, ArgumentName);
    }

    private static AstNodeKind DetermineKind(bool ofDirective, NameNode? memberName, NameNode? argumentName)
    {
        if (ofDirective) return argumentName is null ? AstNodeKind.DirectiveCoordinate : AstNodeKind.DirectiveArgumentCoordinate;
        return argumentName is not null ? AstNodeKind.ArgumentCoordinate : memberName is not null ? AstNodeKind.MemberCoordinate : AstNodeKind.TypeCoordinate;
    }
}

/// <summary>A named type coordinate.</summary>
public sealed class TypeCoordinateNode : SchemaCoordinateNode
{
    /// <summary>Creates a type coordinate.</summary>
    public TypeCoordinateNode(NameNode name, SourceLocation location) : base(AstNodeKind.TypeCoordinate, location, name, null, null, false) { }
}

/// <summary>A type field coordinate.</summary>
public sealed class MemberCoordinateNode : SchemaCoordinateNode
{
    /// <summary>Creates a member coordinate.</summary>
    public MemberCoordinateNode(NameNode name, NameNode memberName, SourceLocation location) : base(AstNodeKind.MemberCoordinate, location, name, memberName, null, false) { }
    /// <summary>Gets the required member name.</summary>
    public new NameNode MemberName => base.MemberName!;
}

/// <summary>A field argument coordinate.</summary>
public sealed class ArgumentCoordinateNode : SchemaCoordinateNode
{
    /// <summary>Creates an argument coordinate.</summary>
    public ArgumentCoordinateNode(NameNode name, NameNode fieldName, NameNode argumentName, SourceLocation location)
        : base(AstNodeKind.ArgumentCoordinate, location, name, fieldName, argumentName, false) { }
    /// <summary>Gets the required field name.</summary>
    public NameNode FieldName => MemberName!;
    /// <summary>Gets the required argument name.</summary>
    public new NameNode ArgumentName => base.ArgumentName!;
}

/// <summary>A directive coordinate.</summary>
public sealed class DirectiveCoordinateNode : SchemaCoordinateNode
{
    /// <summary>Creates a directive coordinate.</summary>
    public DirectiveCoordinateNode(NameNode name, SourceLocation location) : base(AstNodeKind.DirectiveCoordinate, location, name, null, null, true) { }
}

/// <summary>A directive argument coordinate.</summary>
public sealed class DirectiveArgumentCoordinateNode : SchemaCoordinateNode
{
    /// <summary>Creates a directive argument coordinate.</summary>
    public DirectiveArgumentCoordinateNode(NameNode name, NameNode argumentName, SourceLocation location)
        : base(AstNodeKind.DirectiveArgumentCoordinate, location, name, null, argumentName, true) { }
    /// <summary>Gets the required argument name.</summary>
    public new NameNode ArgumentName => base.ArgumentName!;
}
