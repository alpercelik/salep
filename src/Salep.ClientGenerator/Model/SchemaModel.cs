using Salep.GraphQLParser;
using Salep.ClientGenerator.Config;
using Salep.ClientGenerator.Utilities;

namespace Salep.ClientGenerator.Model;

internal sealed class SchemaModel
{
    public GeneratorConfig Config { get; }
    public DocumentNode Document { get; }
    public IReadOnlyDictionary<string, ObjectTypeDefinitionNode> ObjectTypes { get; }
    public IReadOnlyDictionary<string, InputObjectTypeDefinitionNode> InputTypes { get; }
    public IReadOnlyDictionary<string, InterfaceTypeDefinitionNode> InterfaceTypes { get; }
    public IReadOnlyDictionary<string, UnionTypeDefinitionNode> UnionTypes { get; }
    public IReadOnlyDictionary<string, EnumTypeDefinitionNode> EnumTypes { get; }
    public IReadOnlySet<string> ScalarTypes { get; }

    private SchemaModel(
        DocumentNode document,
        Dictionary<string, ObjectTypeDefinitionNode> objectTypes,
        Dictionary<string, InputObjectTypeDefinitionNode> inputTypes,
        Dictionary<string, InterfaceTypeDefinitionNode> interfaceTypes,
        Dictionary<string, UnionTypeDefinitionNode> unionTypes,
        Dictionary<string, EnumTypeDefinitionNode> enumTypes,
        HashSet<string> scalarTypes,
        GeneratorConfig config)
    {
        Document = document;
        ObjectTypes = objectTypes;
        InputTypes = inputTypes;
        InterfaceTypes = interfaceTypes;
        UnionTypes = unionTypes;
        EnumTypes = enumTypes;
        ScalarTypes = scalarTypes;
        Config = config;
    }

    public static SchemaModel FromDocument(DocumentNode document, GeneratorConfig config)
    {
        var objectTypes = new Dictionary<string, ObjectTypeDefinitionNode>(StringComparer.Ordinal);
        var inputTypes = new Dictionary<string, InputObjectTypeDefinitionNode>(StringComparer.Ordinal);
        var interfaceTypes = new Dictionary<string, InterfaceTypeDefinitionNode>(StringComparer.Ordinal);
        var unionTypes = new Dictionary<string, UnionTypeDefinitionNode>(StringComparer.Ordinal);
        var enumTypes = new Dictionary<string, EnumTypeDefinitionNode>(StringComparer.Ordinal);
        var scalarTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in document.Definitions)
        {
            switch (definition)
            {
                case ObjectTypeDefinitionNode objectDef:
                    objectTypes[objectDef.Name.Value] = objectDef;
                    break;
                case InputObjectTypeDefinitionNode inputDef:
                    inputTypes[inputDef.Name.Value] = inputDef;
                    break;
                case InterfaceTypeDefinitionNode interfaceDef:
                    interfaceTypes[interfaceDef.Name.Value] = interfaceDef;
                    break;
                case UnionTypeDefinitionNode unionDef:
                    unionTypes[unionDef.Name.Value] = unionDef;
                    break;
                case EnumTypeDefinitionNode enumDef:
                    enumTypes[enumDef.Name.Value] = enumDef;
                    break;
                case ScalarTypeDefinitionNode scalarDef:
                    scalarTypes.Add(scalarDef.Name.Value);
                    break;
            }
        }

        return new(document, objectTypes, inputTypes, interfaceTypes, unionTypes, enumTypes, scalarTypes,
            config);
    }

    public string ResolveType(ITypeNode type)
    {
        var isNonNull = false;
        if (type is NonNullTypeNode nonNull)
        {
            isNonNull = true;
            type = nonNull.Type;
        }

        switch (type)
        {
            case ListTypeNode listType:
            {
                var elementType = ResolveType(listType.Type);
                var listName = $"List<{elementType}>";
                return isNonNull ? listName : $"{listName}?";
            }
            case NamedTypeNode namedType:
            {
                var typeName = MapNamedType(namedType.Name.Value);
                return isNonNull ? typeName : $"{typeName}?";
            }
            default:
                return "object";
        }
    }

    public string ResolveRootFieldType(string rootTypeName, string fieldName)
    {
        if (!ObjectTypes.TryGetValue(rootTypeName, out var rootType))
        {
            return "object";
        }

        var fieldDef = rootType.Fields.FirstOrDefault(field => field.Name.Value == fieldName);
        return fieldDef is null ? "object" : ResolveType(fieldDef.Type);
    }

    public bool IsValueType(ITypeNode type)
    {
        if (type is NonNullTypeNode nonNull)
        {
            type = nonNull.Type;
        }

        if (type is ListTypeNode)
        {
            return false;
        }

        if (type is NamedTypeNode namedType)
        {
            return IsValueTypeName(namedType.Name.Value);
        }

        return false;
    }

    private bool IsValueTypeName(string graphQlName)
    {
        if (!ScalarTypes.Contains(graphQlName)) return EnumTypes.ContainsKey(graphQlName);

        if (Config.ScalarValueTypes.TryGetValue(graphQlName, out var isValueType))
        {
            return isValueType;
        }

        return graphQlName switch
        {
            "Int" => true,
            "Float" => true,
            "Boolean" => true,
            "Long" => true,
            "Decimal" => true,
            "DateTime" => true,
            "Instant" => true,
            "UUID" => true,
            _ => false
        };
    }

    public bool IsUnionTypeName(string graphQlName) => UnionTypes.ContainsKey(graphQlName);

    public bool IsInterfaceTypeName(string graphQlName) => InterfaceTypes.ContainsKey(graphQlName);

    public static string GetInterfaceResultTypeName(string interfaceName)
        => $"{CSharpNaming.ToTypeName(interfaceName)}Result";

    public IReadOnlyList<ObjectTypeDefinitionNode> GetInterfaceImplementations(string interfaceName)
    {
        var results = new List<ObjectTypeDefinitionNode>();
        foreach (var objectType in ObjectTypes.Values)
        {
            if (ObjectImplementsInterface(objectType, interfaceName))
            {
                results.Add(objectType);
            }
        }

        return results;
    }

    private static string? GetNamedTypeName(ITypeNode type)
    {
        while (true)
        {
            switch (type)
            {
                case NonNullTypeNode nonNull:
                    type = nonNull.Type;
                    continue;
                case ListTypeNode listType:
                    type = listType.Type;
                    continue;
                case NamedTypeNode namedType:
                    return namedType.Name.Value;
                default:
                    return null;
            }
        }
    }

    public string? GetFieldReturnTypeName(string parentTypeName, string fieldName)
    {
        if (ObjectTypes.TryGetValue(parentTypeName, out var objectType))
        {
            var field = objectType.Fields.FirstOrDefault(item => item.Name.Value == fieldName);
            return field is null ? null : GetNamedTypeName(field.Type);
        }

        if (InterfaceTypes.TryGetValue(parentTypeName, out var interfaceType))
        {
            var field = interfaceType.Fields.FirstOrDefault(item => item.Name.Value == fieldName);
            return field is null ? null : GetNamedTypeName(field.Type);
        }

        return null;
    }

    public ITypeNode? GetFieldTypeNode(string parentTypeName, string fieldName)
    {
        if (ObjectTypes.TryGetValue(parentTypeName, out var objectType))
        {
            return objectType.Fields.FirstOrDefault(item => item.Name.Value == fieldName)?.Type;
        }

        return InterfaceTypes.TryGetValue(parentTypeName, out var interfaceType)
            ? interfaceType.Fields.FirstOrDefault(item => item.Name.Value == fieldName)?.Type
            : null;
    }

    public string GetRootTypeName(OperationType operationType)
    {
        var schemaDef = Document.Definitions.OfType<SchemaDefinitionNode>().FirstOrDefault();

        var operationDef =
            schemaDef?.OperationTypes
                .FirstOrDefault(op => op.Operation == operationType);

        if (operationDef != null)
        {
            return operationDef.Type.Name.Value;
        }

        return operationType switch
        {
            OperationType.Query => "Query",
            OperationType.Mutation => "Mutation",
            OperationType.Subscription => "Subscription",
            _ => "Query"
        };
    }

    private string MapNamedType(string graphQlName)
    {
        if (InterfaceTypes.ContainsKey(graphQlName))
        {
            return GetInterfaceResultTypeName(graphQlName);
        }

        if (!ScalarTypes.Contains(graphQlName))
        {
            return graphQlName switch
            {
                "String" => "string",
                "ID" => "string",
                "Int" => "int",
                "Float" => "double",
                "Boolean" => "bool",
                "Long" => "long",
                "Decimal" => "decimal",
                "DateTime" => "DateTime",
                "Instant" => "DateTimeOffset",
                "UUID" => "Guid",
                _ => CSharpNaming.ToTypeName(graphQlName)
            };
        }

        if (Config.TryGetScalarMapping(graphQlName, out var mappedType, out _))
        {
            return mappedType;
        }

        return graphQlName switch
        {
            "String" => "string",
            "ID" => "string",
            "Int" => "int",
            "Float" => "double",
            "Boolean" => "bool",
            "Long" => "long",
            "Decimal" => "decimal",
            "DateTime" => "DateTime",
            "Instant" => "DateTimeOffset",
            "UUID" => "Guid",
            _ => "string"
        };
    }

    private bool ObjectImplementsInterface(ObjectTypeDefinitionNode objectType, string interfaceName)
    {
        foreach (var interfaceType in objectType.Interfaces)
        {
            var name = interfaceType.Name.Value;
            if (name == interfaceName)
            {
                return true;
            }

            if (InterfaceExtends(name, interfaceName, new(StringComparer.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private bool InterfaceExtends(string candidateInterface, string targetInterface, HashSet<string> seen)
    {
        if (!InterfaceTypes.TryGetValue(candidateInterface, out var interfaceDef))
        {
            return false;
        }

        foreach (var baseInterface in interfaceDef.Interfaces)
        {
            var baseName = baseInterface.Name.Value;
            if (baseName == targetInterface)
            {
                return true;
            }

            if (seen.Add(baseName) && InterfaceExtends(baseName, targetInterface, seen))
            {
                return true;
            }
        }

        return false;
    }
}