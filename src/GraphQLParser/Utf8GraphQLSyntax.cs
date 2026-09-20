namespace GraphQLParser;

public ref partial struct Utf8GraphQLParser
{
    /// <summary>Parses individual GraphQL syntax constructs from strings and UTF-8 input.</summary>
    public static class Syntax
    {
        /// <summary>Parses a directive definition.</summary>
        public static DirectiveDefinitionNode ParseDirectiveDefinition(Utf8GraphQLReader reader) => ParseDirectiveDefinition(reader.GetDecodedSource());
        /// <summary>Parses a directive definition.</summary>
        public static DirectiveDefinitionNode ParseDirectiveDefinition(ReadOnlySpan<byte> sourceText) => ParseDirectiveDefinition(Decode(sourceText));
        /// <summary>Parses a directive definition.</summary>
        public static DirectiveDefinitionNode ParseDirectiveDefinition(string sourceText) => ParseFirstDefinition<DirectiveDefinitionNode>(sourceText);

        /// <summary>Parses a field selection.</summary>
        public static FieldNode ParseField(Utf8GraphQLReader reader) => ParseField(reader.GetDecodedSource());
        /// <summary>Parses a field selection.</summary>
        public static FieldNode ParseField(ReadOnlySpan<byte> sourceText) => ParseField(Decode(sourceText));
        /// <summary>Parses a field selection.</summary>
        public static FieldNode ParseField(string sourceText)
        {
            var document = ParseDocument($"query {{ {RequireSource(sourceText)} }}");
            var operation = RequireNode<OperationDefinitionNode>(document.Definitions[0]);
            if (operation.SelectionSet.Selections.Count != 1) throw new FormatException("The source must contain exactly one field selection.");
            return RequireNode<FieldNode>(operation.SelectionSet.Selections[0]);
        }

        /// <summary>Parses a field definition.</summary>
        public static FieldDefinitionNode ParseFieldDefinition(Utf8GraphQLReader reader) => ParseFieldDefinition(reader.GetDecodedSource());
        /// <summary>Parses a field definition.</summary>
        public static FieldDefinitionNode ParseFieldDefinition(ReadOnlySpan<byte> sourceText) => ParseFieldDefinition(Decode(sourceText));
        /// <summary>Parses a field definition.</summary>
        public static FieldDefinitionNode ParseFieldDefinition(string sourceText)
        {
            var document = ParseDocument($"type __SyntaxType {{ {RequireSource(sourceText)} }}");
            var fields = RequireNode<ObjectTypeDefinitionNode>(document.Definitions[0]).Fields;
            if (fields.Count != 1) throw new FormatException("The source must contain exactly one field definition.");
            return fields[0];
        }

        /// <summary>Parses a fragment definition.</summary>
        public static FragmentDefinitionNode ParseFragmentDefinition(Utf8GraphQLReader reader) => ParseFragmentDefinition(reader.GetDecodedSource());
        /// <summary>Parses a fragment definition.</summary>
        public static FragmentDefinitionNode ParseFragmentDefinition(ReadOnlySpan<byte> sourceText) => ParseFragmentDefinition(Decode(sourceText));
        /// <summary>Parses a fragment definition.</summary>
        public static FragmentDefinitionNode ParseFragmentDefinition(string sourceText) => ParseFirstDefinition<FragmentDefinitionNode>(sourceText);

        /// <summary>Parses an object value literal.</summary>
        public static ObjectValueNode ParseObjectLiteral(Utf8GraphQLReader reader, bool constant = true) => RequireNode<ObjectValueNode>(ParseValueLiteral(reader.GetDecodedSource(), constant));
        /// <summary>Parses an object value literal.</summary>
        public static ObjectValueNode ParseObjectLiteral(ReadOnlySpan<byte> sourceText, bool constant = true) => RequireNode<ObjectValueNode>(ParseValueLiteral(Decode(sourceText), constant));
        /// <summary>Parses an object value literal.</summary>
        public static ObjectValueNode ParseObjectLiteral(string sourceText, bool constant = true) => RequireNode<ObjectValueNode>(ParseValueLiteral(sourceText, constant));

        /// <summary>Parses an object type definition.</summary>
        public static ObjectTypeDefinitionNode ParseObjectTypeDefinition(Utf8GraphQLReader reader) => ParseObjectTypeDefinition(reader.GetDecodedSource());
        /// <summary>Parses an object type definition.</summary>
        public static ObjectTypeDefinitionNode ParseObjectTypeDefinition(ReadOnlySpan<byte> sourceText) => ParseObjectTypeDefinition(Decode(sourceText));
        /// <summary>Parses an object type definition.</summary>
        public static ObjectTypeDefinitionNode ParseObjectTypeDefinition(string sourceText) => ParseFirstDefinition<ObjectTypeDefinitionNode>(sourceText);

        /// <summary>Parses a schema coordinate.</summary>
        public static SchemaCoordinateNode ParseSchemaCoordinate(Utf8GraphQLReader reader) => ParseSchemaCoordinate(reader.GetDecodedSource());
        /// <summary>Parses a schema coordinate.</summary>
        public static SchemaCoordinateNode ParseSchemaCoordinate(ReadOnlySpan<byte> sourceText) => ParseSchemaCoordinate(Decode(sourceText));
        /// <summary>Parses a schema coordinate.</summary>
        public static SchemaCoordinateNode ParseSchemaCoordinate(string sourceText) => global::GraphQLParser.GraphQLParser.ParseSchemaCoordinate(RequireSource(sourceText));

        /// <summary>Parses a selection set.</summary>
        public static SelectionSetNode ParseSelectionSet(Utf8GraphQLReader reader) => ParseSelectionSet(reader.GetDecodedSource());
        /// <summary>Parses a selection set.</summary>
        public static SelectionSetNode ParseSelectionSet(ReadOnlySpan<byte> sourceText) => ParseSelectionSet(Decode(sourceText));
        /// <summary>Parses a selection set.</summary>
        public static SelectionSetNode ParseSelectionSet(string sourceText)
        {
            var document = ParseDocument($"query {RequireSource(sourceText)}");
            return RequireNode<OperationDefinitionNode>(document.Definitions[0]).SelectionSet;
        }

        /// <summary>Parses a type reference.</summary>
        public static ITypeNode ParseTypeReference(Utf8GraphQLReader reader) => ParseTypeReference(reader.GetDecodedSource());
        /// <summary>Parses a type reference.</summary>
        public static ITypeNode ParseTypeReference(ReadOnlySpan<byte> sourceText) => ParseTypeReference(Decode(sourceText));
        /// <summary>Parses a type reference.</summary>
        public static ITypeNode ParseTypeReference(string sourceText)
        {
            var document = ParseDocument($"type __SyntaxType {{ value: {RequireSource(sourceText)} }}");
            var fields = RequireNode<ObjectTypeDefinitionNode>(document.Definitions[0]).Fields;
            if (fields.Count != 1) throw new FormatException("The source must contain exactly one type reference.");
            return fields[0].Type;
        }

        /// <summary>Parses a value literal.</summary>
        public static IValueNode ParseValueLiteral(Utf8GraphQLReader reader, bool constant = true) => ParseValueLiteral(reader.GetDecodedSource(), constant);
        /// <summary>Parses a value literal.</summary>
        public static IValueNode ParseValueLiteral(ReadOnlySpan<byte> sourceText, bool constant = true) => ParseValueLiteral(Decode(sourceText), constant);
        /// <summary>Parses a value literal.</summary>
        public static IValueNode ParseValueLiteral(string sourceText, bool constant = true)
        {
            var document = ParseDocument($"query {{ value(input: {RequireSource(sourceText)}) }}");
            var operation = RequireNode<OperationDefinitionNode>(document.Definitions[0]);
            var field = RequireNode<FieldNode>(operation.SelectionSet.Selections[0]);
            if (field.Arguments.Count != 1) throw new FormatException("The source must contain exactly one value literal.");
            var value = field.Arguments[0].Value;
            if (constant && !GraphQLAstPredicates.IsConstValueNode(value))
                throw new ArgumentException("A constant value literal cannot contain variables.", nameof(sourceText));
            return value;
        }

        private static T ParseFirstDefinition<T>(string sourceText) where T : AstNode
        {
            var document = ParseDocument(RequireSource(sourceText));
            if (document.Definitions.Count != 1) throw new FormatException("The source must contain exactly one syntax definition.");
            return RequireNode<T>(document.Definitions[0]);
        }

        private static DocumentNode ParseDocument(string sourceText) => global::GraphQLParser.GraphQLParser.Parse(new SourceText(sourceText.AsMemory()));

        private static T RequireNode<T>(object? node) where T : class => node as T
            ?? throw new FormatException($"The supplied GraphQL source did not produce a {typeof(T).Name}.");

        private static string RequireSource(string sourceText) => sourceText ?? throw new ArgumentNullException(nameof(sourceText));

        private static string Decode(ReadOnlySpan<byte> sourceText)
        {
            var reader = new Utf8GraphQLReader(sourceText);
            return reader.GetDecodedSource();
        }
    }
}
