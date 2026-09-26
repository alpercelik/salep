using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Salep.ClientGenerator.Emission.Syntax.Cs;
using static Salep.ClientGenerator.Emission.Testing.TestSyntax;

namespace Salep.ClientGenerator.Emission.Testing;

internal sealed class ErrorResponseTestSyntax(TestSyntax test)
{
    public IEnumerable<MemberDeclarationSyntax> Generate() =>
    [
        Fact("GraphQLError_Defaults_Message", [Local("error", New("GraphQLError")), Assert("error.Message", "ShouldBe", Name("string.Empty")),
            Local("response", NewObject("GraphQLResponse<DummyResponse>", [Assign("Errors", Array(Name("error")))])), Assert("response.Errors", "ShouldNotBeNull")]),
        Details(),
        Fact("Response_Allows_Null_List_Items", [.. Read("DummyListResponse", "{\"data\":{\"posts\":[null]}}"),
            Assert("response.Data", "ShouldNotBeNull"), Assert(Member(Member(Suppress(Name("response.Data")), "Posts"), "Count"), "ShouldBe", Number(1)),
            Assert(Index("response.Data.Posts", 0), "ShouldBeNull")]),
        Fact("GraphQLResponse_Allows_Null_For_NonNull_Field_With_Errors", [.. Read("DummyUsersResponse", "{\"data\":{\"users\":null},\"errors\":[{\"message\":\"Non-null violation\"}]}"),
            Assert("response.Errors", "ShouldNotBeNull"), Assert("response.Data", "ShouldNotBeNull"), Assert(Member(Suppress(Name("response.Data")), "Users"), "ShouldBeNull")]),
        Fact("GraphQLResponse_Allows_Null_Data_With_Errors", [.. Read("DummyUsersResponse", "{\"data\":null,\"errors\":[{\"message\":\"Failure\"}]}"),
            Assert("response.Errors", "ShouldNotBeNull"), Assert("response.Data", "ShouldBeNull")])
    ];
    private IEnumerable<StatementSyntax> Read(string response, string json) =>
    [
        Local("json", test.Json(json)), Local("options", NewObject("JsonSerializerOptions", [Assign("PropertyNameCaseInsensitive", Bool(true))])),
        Local("response", GenericCall("JsonSerializer", "Deserialize", [Type($"GraphQLResponse<{response}>")], Name("json"), Name("options"))),
        Assert("response", "ShouldNotBeNull")
    ];
    private MethodDeclarationSyntax Details() => Fact("GraphQLError_Deserializes_Details",
    [
        .. Read("DummyResponse", "{\"errors\":[{\"message\":\"bad\",\"locations\":[{\"line\":2,\"column\":4}],\"path\":[\"users\",0,\"name\"],\"extensions\":{\"code\":\"BAD\"}}]}"),
        Assert("response.Errors", "ShouldNotBeNull"), Assert(Member(Suppress(Name("response.Errors")), "Length"), "ShouldBe", Number(1)), Local("error", Index("response.Errors", 0)),
        Assert("error.Message", "ShouldBe", String("bad")), Assert("error.Locations", "ShouldNotBeNull"),
        Assert(Member(Index(Suppress(Name("error.Locations")), 0), "Line"), "ShouldBe", Number(2)), Assert(Member(Index("error.Locations", 0), "Column"), "ShouldBe", Number(4)),
        Assert("error.Path", "ShouldNotBeNull"), Assert(Member(Suppress(Name("error.Path")), "Length"), "ShouldBe", Number(3)),
        Assert(Call(Member(Index("error.Path", 0), "GetString")), "ShouldBe", String("users")),
        Assert(Call(Member(Index("error.Path", 1), "GetInt32")), "ShouldBe", Number(0)),
        Assert(Call(Member(Index("error.Path", 2), "GetString")), "ShouldBe", String("name")), Assert("error.Extensions", "ShouldNotBeNull"),
        Assert(Call(Member(Call(Member(Member(Suppress(Name("error.Extensions")), "Value"), "GetProperty"), String("code")), "GetString")), "ShouldBe", String("BAD"))
    ]);
}
