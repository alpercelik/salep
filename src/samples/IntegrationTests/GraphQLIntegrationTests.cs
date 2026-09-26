using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alba;
using GraphQL.Sharp.Generated;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
#if MINIMAL_DEPENDENCIES
using ModuleGraphQLClient = GraphQL.Sharp.Generated.Module.MinimalDependencies.GeneratedModuleClient;
using ModuleGraphQLOperations = GraphQL.Sharp.Generated.Module.MinimalDependencies.GeneratedModuleOperations;
#else
using ModuleGraphQLClient = GraphQL.Sharp.Generated.Module.GeneratedModuleClient;
using ModuleGraphQLOperations = GraphQL.Sharp.Generated.Module.GeneratedModuleOperations;
#endif

namespace GeneratedClient.IntegrationTests;

public sealed class GraphQLIntegrationTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;
    private HttpClient _httpClient = null!;
    private GraphQLOperations _operations = null!;
    private ModuleGraphQLOperations _moduleOperations = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<global::Program>();
        var server = _host.Services.GetRequiredService<IServer>().ShouldBeOfType<TestServer>();
        _httpClient = new HttpClient(new ErrorResponseHandler(server.CreateHandler()));
        _operations = new GraphQLOperations(new GraphQLClient(_httpClient, new Uri("http://localhost/graphql")));
        _moduleOperations = new ModuleGraphQLOperations(new ModuleGraphQLClient(_httpClient, new Uri("http://localhost/graphql")));
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task GeneratedQueriesExecuteAgainstTheGraphqlServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertSuccessful(await _operations.UsersDefaultRoleAsync(cancellationToken));
        AssertSuccessful(await _operations.UsersByRoleAsync(new UsersByRoleVariables(), cancellationToken));
        AssertSuccessful(await _operations.SearchUsersAndPostsAsync(new SearchUsersAndPostsVariables { Text = "Ada" }, cancellationToken));
        AssertSuccessful(await _operations.NodeByIdAsync(new NodeByIdVariables { Id = "1" }, cancellationToken));
        AssertSuccessful(await _operations.SearchWithMultipleInlineFragmentsAsync(new SearchWithMultipleInlineFragmentsVariables { Text = "Ada" }, cancellationToken));
        AssertSuccessful(await _operations.EchoListAsync(new EchoListVariables { Values = ["one", "two"] }, cancellationToken));
        AssertSuccessful(await _operations.PostsByIdsAsync(new PostsByIdsVariables { Ids = ["1"], AllowMissing = false }, cancellationToken));
        AssertSuccessful(await _operations.UnusedVarAsync(cancellationToken));
    }

    [Fact]
    public async Task QueriesExceedingTheSampleCostLimitAreRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var server = _host.Services.GetRequiredService<IServer>().ShouldBeOfType<TestServer>();
        using var client = new HttpClient(server.CreateHandler());
        client.DefaultRequestHeaders.Accept.ParseAdd("application/graphql-response+json");
        const string query = """
            {
              first: users(role: ADMIN) { ...UserWithPosts }
              second: users(role: ADMIN) { ...UserWithPosts }
              third: users(role: ADMIN) { ...UserWithPosts }
            }
            fragment UserWithPosts on User {
              id name role createdAt
              posts { id title content author { id name role createdAt } }
            }
            """;
        using var response = await client.PostAsJsonAsync("http://localhost/graphql", new { query }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        body.ShouldNotBeNull();
        var extensions = body.RootElement.GetProperty("errors")[0].GetProperty("extensions");
        extensions.GetProperty("code").GetString().ShouldBe("HC0047");
        extensions.GetProperty("maxFieldCost").GetDouble().ShouldBe(5_000);
        extensions.GetProperty("fieldCost").GetDouble().ShouldBeGreaterThan(5_000);
    }

    [Fact]
    public async Task GeneratedCreatePostSimpleMutationExecutesAgainstTheGraphqlServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertSuccessful(await _operations.CreatePostSimpleAsync(new CreatePostSimpleVariables { Title = "Simple integration post" }, cancellationToken));
    }

    [Fact]
    public async Task GeneratedCreatePostWithContentMutationExecutesAgainstTheGraphqlServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertSuccessful(await _operations.CreatePostWithContentAsync(new CreatePostWithContentVariables { Title = "Content integration post", Content = "Created through Alba" }, cancellationToken));
    }

    [Fact]
    public async Task GeneratedCreatePostAliasedMutationExecutesAgainstTheGraphqlServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertSuccessful(await _operations.CreatePostAliasedAsync(cancellationToken));
    }

    [Fact]
    public async Task GeneratedUpdatePostWithDefaultsMutationExecutesAgainstTheGraphqlServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertSuccessful(await _operations.UpdatePostWithDefaultsAsync(new UpdatePostWithDefaultsVariables
        {
            Id = "1",
            Input = new CreatePostInput { Title = "Updated integration post", Content = "Updated by test", Tags = ["integration"] }
        }, cancellationToken));
    }

    [Fact]
    public async Task GeneratedDeletePostsMutationExecutesAgainstTheGraphqlServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertSuccessful(await _operations.DeletePostsAsync(new DeletePostsVariables { Ids = ["1", "2"] }, cancellationToken));
    }

    [Fact]
    public async Task GeneratedModuleQueryExecutesAgainstTheGraphqlServer()
    {
        AssertModuleSuccessful(await _moduleOperations.ModuleUsersAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GeneratedModuleMutationExecutesAgainstTheGraphqlServer()
    {
        AssertModuleSuccessful(await _moduleOperations.ModuleCreatePostAsync(TestContext.Current.CancellationToken));
    }

    private static void AssertSuccessful<T>(GraphQLResponse<T> response)
    {
        response.Errors.ShouldBeNull();
        Assert.NotNull(response.Data);
    }

    private static void AssertModuleSuccessful<T>(GraphQLResponse<T> response)
    {
        response.Errors.ShouldBeNull();
        Assert.NotNull(response.Data);
    }

    private sealed class ErrorResponseHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var statusCode = response.StatusCode;
                response.Dispose();
                throw new HttpRequestException($"GraphQL server returned {(int)statusCode}: {body}");
            }

            return response;
        }
    }
}
