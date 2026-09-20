namespace GraphQLParser;

/// <summary>Base class for executable and type-system document definitions.</summary>
public abstract class DefinitionNode : AstNode
{
    /// <summary>Creates a definition with a stable node kind and source location.</summary>
    protected DefinitionNode(AstNodeKind kind, SourceLocation location)
        : base(kind, location)
    {
    }
}

/// <summary>A GraphQL document containing source-ordered definitions.</summary>
public sealed class DocumentNode : AstNode
{
    /// <summary>Creates a document and snapshots its definitions in source order.</summary>
    public DocumentNode(SourceText source, IEnumerable<DefinitionNode> definitions, SourceLocation location)
        : base(AstNodeKind.Document, location)
    {
        Definitions = new AstNodeList<DefinitionNode>(definitions);
        if (Definitions.Count == 0)
        {
            throw new ArgumentException("A GraphQL document must contain at least one definition.", nameof(definitions));
        }

        if (location.End > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(location), "The document location must fit within its source memory.");
        }

        for (var index = 0; index < Definitions.Count; index++)
        {
            var definitionLocation = Definitions[index].Location;
            if (definitionLocation.Start < location.Start || definitionLocation.End > location.End)
            {
                throw new ArgumentException("Every definition must be contained by the document location.", nameof(definitions));
            }

            if (index > 0 && definitionLocation.Start < Definitions[index - 1].Location.Start)
            {
                throw new ArgumentException("Document definitions must be in source order.", nameof(definitions));
            }
        }

        Source = source;
    }

    /// <summary>Gets the original source memory used by the document.</summary>
    public SourceText Source { get; }

    /// <summary>Gets the document definitions in source order.</summary>
    public AstNodeList<DefinitionNode> Definitions { get; }
}

/// <summary>Identifies a GraphQL executable operation.</summary>
public enum OperationType : byte
{
    /// <summary>A query operation.</summary>
    Query,
    /// <summary>A mutation operation.</summary>
    Mutation,
    /// <summary>A subscription operation.</summary>
    Subscription,
}

/// <summary>A query, mutation, or subscription definition.</summary>
public sealed class OperationDefinitionNode : DefinitionNode
{
    /// <summary>Creates an immutable operation definition.</summary>
    public OperationDefinitionNode(
        OperationType operation,
        NameNode? name,
        IEnumerable<VariableDefinitionNode> variableDefinitions,
        IEnumerable<DirectiveNode> directives,
        SelectionSetNode selectionSet,
        SourceLocation location,
        StringValueNode? description = null)
        : base(AstNodeKind.OperationDefinition, location)
    {
        ArgumentNullException.ThrowIfNull(variableDefinitions);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(selectionSet);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        Operation = operation;
        Name = name;
        VariableDefinitions = new AstNodeList<VariableDefinitionNode>(variableDefinitions);
        Directives = new AstNodeList<DirectiveNode>(directives);
        SelectionSet = selectionSet;
        Description = description;
    }

    /// <summary>Gets the query, mutation, or subscription kind.</summary>
    public OperationType Operation { get; }
    /// <summary>Gets the optional operation name.</summary>
    public NameNode? Name { get; }
    /// <summary>Gets variable definitions in source order.</summary>
    public AstNodeList<VariableDefinitionNode> VariableDefinitions { get; }
    /// <summary>Gets directives in source order.</summary>
    public AstNodeList<DirectiveNode> Directives { get; }
    /// <summary>Gets the required operation selection set.</summary>
    public SelectionSetNode SelectionSet { get; }
    /// <summary>Gets the optional operation description.</summary>
    public StringValueNode? Description { get; }
}

/// <summary>A fragment definition with a required type condition and selection set.</summary>
public sealed class FragmentDefinitionNode : DefinitionNode
{
    /// <summary>Creates an immutable fragment definition.</summary>
    public FragmentDefinitionNode(
        NameNode name,
        NamedTypeNode typeCondition,
        IEnumerable<DirectiveNode> directives,
        SelectionSetNode selectionSet,
        SourceLocation location,
        StringValueNode? description = null,
        IEnumerable<VariableDefinitionNode>? variableDefinitions = null)
        : base(AstNodeKind.FragmentDefinition, location)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(typeCondition);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(selectionSet);
        Name = name;
        TypeCondition = typeCondition;
        Directives = new AstNodeList<DirectiveNode>(directives);
        SelectionSet = selectionSet;
        Description = description;
        VariableDefinitions = variableDefinitions is null ? AstNodeList<VariableDefinitionNode>.Empty : new AstNodeList<VariableDefinitionNode>(variableDefinitions);
    }

    /// <summary>Gets the fragment name.</summary>
    public NameNode Name { get; }
    /// <summary>Gets the required fragment type condition.</summary>
    public NamedTypeNode TypeCondition { get; }
    /// <summary>Gets directives in source order.</summary>
    public AstNodeList<DirectiveNode> Directives { get; }
    /// <summary>Gets the required fragment selection set.</summary>
    public SelectionSetNode SelectionSet { get; }
    /// <summary>Gets fragment variable definitions, empty unless enabled by parser options.</summary>
    public AstNodeList<VariableDefinitionNode> VariableDefinitions { get; }
    /// <summary>Gets the optional fragment description.</summary>
    public StringValueNode? Description { get; }
}
