namespace Salep.Parser;

/// <summary>A standard GraphQL directive definition location.</summary>
public sealed class DirectiveLocation : IEquatable<DirectiveLocation>
{
    private static readonly IReadOnlyDictionary<string, DirectiveLocation> Locations = CreateLocations();

    private DirectiveLocation(string value) => Value = value;

    /// <summary>Gets the location's GraphQL specification name.</summary>
    public string Value { get; }

    /// <summary>Gets the argument definition location.</summary>
    public static DirectiveLocation ArgumentDefinition => Get(nameof(ArgumentDefinition));
    /// <summary>Gets the directive definition location.</summary>
    public static DirectiveLocation DirectiveDefinition => Get(nameof(DirectiveDefinition));
    /// <summary>Gets the enum definition location.</summary>
    public static DirectiveLocation Enum => Get(nameof(Enum));
    /// <summary>Gets the enum value definition location.</summary>
    public static DirectiveLocation EnumValue => Get(nameof(EnumValue));
    /// <summary>Gets the field selection location.</summary>
    public static DirectiveLocation Field => Get(nameof(Field));
    /// <summary>Gets the field definition location.</summary>
    public static DirectiveLocation FieldDefinition => Get(nameof(FieldDefinition));
    /// <summary>Gets the fragment definition location.</summary>
    public static DirectiveLocation FragmentDefinition => Get(nameof(FragmentDefinition));
    /// <summary>Gets the fragment spread location.</summary>
    public static DirectiveLocation FragmentSpread => Get(nameof(FragmentSpread));
    /// <summary>Gets the inline fragment location.</summary>
    public static DirectiveLocation InlineFragment => Get(nameof(InlineFragment));
    /// <summary>Gets the input field definition location.</summary>
    public static DirectiveLocation InputFieldDefinition => Get(nameof(InputFieldDefinition));
    /// <summary>Gets the input object definition location.</summary>
    public static DirectiveLocation InputObject => Get(nameof(InputObject));
    /// <summary>Gets the interface definition location.</summary>
    public static DirectiveLocation Interface => Get(nameof(Interface));
    /// <summary>Gets the mutation operation location.</summary>
    public static DirectiveLocation Mutation => Get(nameof(Mutation));
    /// <summary>Gets the object definition location.</summary>
    public static DirectiveLocation Object => Get(nameof(Object));
    /// <summary>Gets the query operation location.</summary>
    public static DirectiveLocation Query => Get(nameof(Query));
    /// <summary>Gets the scalar definition location.</summary>
    public static DirectiveLocation Scalar => Get(nameof(Scalar));
    /// <summary>Gets the schema definition location.</summary>
    public static DirectiveLocation Schema => Get(nameof(Schema));
    /// <summary>Gets the subscription operation location.</summary>
    public static DirectiveLocation Subscription => Get(nameof(Subscription));
    /// <summary>Gets the union definition location.</summary>
    public static DirectiveLocation Union => Get(nameof(Union));
    /// <summary>Gets the variable definition location.</summary>
    public static DirectiveLocation VariableDefinition => Get(nameof(VariableDefinition));

    /// <summary>Checks whether a string names a standard directive location.</summary>
    public static bool IsValidName(string value) => value is not null && Locations.ContainsKey(value);

    /// <summary>Attempts to parse a standard directive location name.</summary>
    public static bool TryParse(string value, out DirectiveLocation? location)
    {
        if (value is not null && Locations.TryGetValue(value, out location)) return true;
        location = null;
        return false;
    }

    /// <summary>Determines whether this value equals another directive location.</summary>
    public bool Equals(DirectiveLocation? other) => other is not null && StringComparer.Ordinal.Equals(Value, other.Value);
    /// <summary>Determines whether this value equals another object.</summary>
    public override bool Equals(object? obj) => obj is DirectiveLocation other && Equals(other);
    /// <summary>Returns the hash code for this directive location.</summary>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    /// <summary>Returns the GraphQL specification name.</summary>
    public override string ToString() => Value;

    private static IReadOnlyDictionary<string, DirectiveLocation> CreateLocations()
    {
        var values = new[]
        {
            nameof(ArgumentDefinition), nameof(DirectiveDefinition), nameof(Enum), nameof(EnumValue), nameof(Field),
            nameof(FieldDefinition), nameof(FragmentDefinition), nameof(FragmentSpread), nameof(InlineFragment),
            nameof(InputFieldDefinition), nameof(InputObject), nameof(Interface), nameof(Mutation), nameof(Object),
            nameof(Query), nameof(Scalar), nameof(Schema), nameof(Subscription), nameof(Union), nameof(VariableDefinition),
        };
        return values.ToDictionary(value => ToGraphQLName(value), value => new DirectiveLocation(ToGraphQLName(value)), StringComparer.Ordinal);
    }

    private static DirectiveLocation Get(string propertyName) => Locations[ToGraphQLName(propertyName)];

    private static string ToGraphQLName(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsUpper(value[index]) && index > 0) result.Append('_');
            result.Append(char.ToUpperInvariant(value[index]));
        }
        return result.ToString();
    }
}
