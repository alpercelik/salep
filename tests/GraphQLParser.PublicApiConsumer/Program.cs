using System.Text;
using GraphQLParser;
using GraphQLParser.Utilities;
using GraphQLParser.Visitors;

const string query = "query Sample($id: ID!) { user(id: $id) { name } }";
var source = new SourceText(query.AsMemory());
var document = GraphQLParser.GraphQLParser.Parse(source);
ISyntaxNode root = document;
var operation = (OperationDefinitionNode)document.Definitions[0];
IHasDirectives variableDirectives = operation.VariableDefinitions[0];
if (variableDirectives.Directives.Count != 0) throw new InvalidOperationException("The public variable definition contract returned unexpected directives.");
ISelectionNode selection = (ISelectionNode)operation.SelectionSet.Selections[0];
IHasDirectives selectionDirectives = selection;
if (selectionDirectives.Directives.Count != 0) throw new InvalidOperationException("The public selection contract returned unexpected directives.");

var visitedNodes = 0;
var visitor = SyntaxVisitor.Create(_ =>
{
    visitedNodes++;
    return new ContinueSyntaxVisitorAction();
});
visitor.Visit(root, new object());
if (visitedNodes == 0) throw new InvalidOperationException("The public syntax visitor did not traverse the document.");

var rewritten = new SyntaxRewriter<object>().Rewrite(root, new object());
if (!SyntaxComparer.BySyntax.Equals(root, rewritten)) throw new InvalidOperationException("The public rewriter changed an untouched document.");
if (string.IsNullOrWhiteSpace(SyntaxPrinter.Print(rewritten))) throw new InvalidOperationException("The public printer returned empty output.");

var utf8Document = Utf8GraphQLParser.Parse(Encoding.UTF8.GetBytes(query));
if (!SyntaxComparer.BySyntax.Equals(root, utf8Document)) throw new InvalidOperationException("UTF-8 parsing changed the document syntax.");
if (Utf8GraphQLParser.Syntax.ParseTypeReference("[String!]!").Kind != SyntaxKind.NonNullType)
    throw new InvalidOperationException("The public UTF-8 syntax helper returned an unexpected type.");

if (new NameNode("api").WithValue("consumer").Value != "consumer")
    throw new InvalidOperationException("The public immutable AST update API failed.");

static NamedSyntaxNode RequireNamedSyntax(NamedSyntaxNode node) => node;

const string namedSyntaxSource = "query Q { user { name ...Details } } fragment Details on User { id } type QueryType { field(arg: Int): String } enum Status { READY } input Filter { term: String }";
var namedDocument = GraphQLParser.GraphQLParser.Parse(new SourceText(namedSyntaxSource.AsMemory()));
var namedOperation = (OperationDefinitionNode)namedDocument.Definitions[0];
var namedField = (FieldNode)namedOperation.SelectionSet.Selections[0];
_ = RequireNamedSyntax(namedField);
_ = RequireNamedSyntax((FragmentSpreadNode)namedField.SelectionSet!.Selections[1]);
_ = RequireNamedSyntax((FragmentDefinitionNode)namedDocument.Definitions[1]);
var objectType = (ObjectTypeDefinitionNode)namedDocument.Definitions[2];
_ = RequireNamedSyntax(objectType.Fields[0]);
_ = RequireNamedSyntax(objectType.Fields[0].Arguments[0]);
_ = RequireNamedSyntax(((EnumTypeDefinitionNode)namedDocument.Definitions[3]).Values[0]);
_ = RequireNamedSyntax(((InputObjectTypeDefinitionNode)namedDocument.Definitions[4]).Fields[0]);
_ = RequireNamedSyntax(new DirectiveExtensionNode(new Location(0, 0, 1, 1), new NameNode("tag"), Array.Empty<DirectiveNode>()));

Console.WriteLine($"Public API consumer passed ({visitedNodes} visited nodes).");
