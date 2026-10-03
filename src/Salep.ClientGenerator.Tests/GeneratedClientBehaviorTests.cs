using System.Collections;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Salep.ClientGenerator.Generation;
using Xunit;

namespace Salep.ClientGenerator.Tests;

public sealed class GeneratedClientBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generated_clients_honor_options_when_batching_is_disabled(bool useHttpGet)
    {
        using var fixture = new ClientFixture(useHttpGet: useHttpGet, enableBatching: false);
        var assembly = fixture.GenerateAndCompile();
        var observation = await ObserveDisabledTransportOptionsAsync(assembly, "Scriban", TestContext.Current.CancellationToken);

        Assert.Equal(useHttpGet ? "GET" : "POST", observation.Query.Method);
        Assert.Equal("System.InvalidOperationException: Batching is disabled.", observation.BatchFailure);
        Assert.Equal(string.Empty, observation.BatchRequest.Method);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generated_clients_propagate_request_cancellation(bool useHttpGet)
    {
        using var fixture = new ClientFixture(useHttpGet: useHttpGet);
        var assembly = fixture.GenerateAndCompile();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        async Task<string> Observe(string backend)
        {
            var type = assembly.GetType($"Parity.{backend}.GraphQLClient", throwOnError: true)!;
            var client = CreateClient(type, out _);
            var operation = CreateOperation(assembly, backend, "Greet", "Ada");
            return await CaptureFailureAsync(() => InvokeAsync(client, operation, cancellation.Token));
        }
        var observation = await Observe("Scriban");
        Assert.Contains("canceled", observation, StringComparison.OrdinalIgnoreCase);
    }

#if NET11_0_OR_GREATER
    [Fact]
    public async Task Generated_clients_preserve_interface_response_deserialization()
    {
        using var fixture = new ClientFixture(unionRepresentation: "native", targetFramework: "net11.0", languageVersion: "preview", includeInterface: true);
        var assembly = fixture.GenerateAndCompile();
        var observation = await ObserveInterfaceResponseAsync(assembly, "Scriban", "{\"data\":{\"node\":{\"__typename\":\"Product\",\"id\":\"p-1\",\"sku\":\"sku-1\"}}}");

        Assert.Equal("Product", observation.ConcreteResponseType);
        Assert.Equal("p-1", observation.Id);
        Assert.Equal("sku-1", observation.Sku);

        var unionJson = "{\"data\":{\"search\":[{\"__typename\":\"User\",\"id\":\"u-1\",\"name\":\"Ada\"},{\"__typename\":\"Product\",\"id\":\"p-2\",\"sku\":\"sku-2\"}]}}";
        var union = await ObserveUnionResponseAsync(assembly, "Scriban", unionJson);
        Assert.Equal("User:Ada|Product:sku-2", union.Variants);

        var missingTypeName = await ObserveInterfaceFailureAsync(assembly, "Scriban", "{\"data\":{\"node\":{\"id\":\"p-1\"}}}");
        Assert.StartsWith($"{typeof(JsonException).FullName}:", missingTypeName, StringComparison.Ordinal);

        var unknownTypeName = await ObserveInterfaceFailureAsync(assembly, "Scriban", "{\"data\":{\"node\":{\"__typename\":\"Unknown\",\"id\":\"p-1\"}}}");
        Assert.StartsWith($"{typeof(JsonException).FullName}:", unknownTypeName, StringComparison.Ordinal);

        var missingUnionTypeName = await ObserveUnionFailureAsync(assembly, "Scriban", "{\"data\":{\"search\":[{\"id\":\"u-1\"}]}}");
        Assert.StartsWith($"{typeof(JsonException).FullName}:", missingUnionTypeName, StringComparison.Ordinal);

        var unknownUnionTypeName = await ObserveUnionFailureAsync(assembly, "Scriban", "{\"data\":{\"search\":[{\"__typename\":\"Unknown\",\"id\":\"u-1\"}]}}");
        Assert.StartsWith($"{typeof(JsonException).FullName}:", unknownUnionTypeName, StringComparison.Ordinal);
    }
#endif

    [Fact]
    public async Task Generated_clients_preserve_http_transport_behavior()
    {
        using var fixture = new ClientFixture();
        var assembly = fixture.GenerateAndCompile();
        var observation = await ObserveClientAsync(assembly, "Scriban", TestContext.Current.CancellationToken);

        Assert.Equal("GET", observation.Get.Method);
        Assert.Contains("operationName=Greet", new Uri(observation.Get.Uri!).Query, StringComparison.Ordinal);
        var decodedGetQuery = WebUtility.UrlDecode(new Uri(observation.Get.Uri!).Query.TrimStart('?'));
        var variablesMarker = decodedGetQuery.LastIndexOf("&variables=", StringComparison.Ordinal);
        Assert.True(variablesMarker >= 0, decodedGetQuery);
        using (var variables = JsonDocument.Parse(decodedGetQuery[(variablesMarker + "&variables=".Length)..]))
            Assert.Equal("Ada + & Sons", variables.RootElement.GetProperty("name").GetString());
        Assert.Equal("GET", observation.EnumQuery.Method);
        using (var variables = JsonDocument.Parse(WebUtility.UrlDecode(new Uri(observation.EnumQuery.Uri!).Query.TrimStart('?'))
                   .Split("&variables=", StringSplitOptions.None)[1]))
            Assert.Equal("ACTIVE", variables.RootElement.GetProperty("input").GetString());
        Assert.Equal("ACTIVE", observation.EnumResponse);
        Assert.True(observation.Get.CancellationTokenCanCancel);
        Assert.Equal("GET", observation.NamedQueryWithoutVariables.Method);
        Assert.DoesNotContain("variables=", new Uri(observation.NamedQueryWithoutVariables.Uri!).Query, StringComparison.Ordinal);
        Assert.Equal("GET", observation.EmptyVariablesQuery.Method);
        var emptyVariablesQuery = WebUtility.UrlDecode(new Uri(observation.EmptyVariablesQuery.Uri!).Query.TrimStart('?'));
        Assert.Contains("&variables={}", emptyVariablesQuery, StringComparison.Ordinal);
        Assert.Equal("GET", observation.GetUrlAtLimit.Method);
        Assert.Equal(2048, new Uri(observation.GetUrlAtLimit.Uri!).AbsoluteUri.Length);
        Assert.Equal("POST", observation.GetUrlOverLimit.Method);
        Assert.Equal("POST", observation.Mutation.Method);
        Assert.Contains("application/json", observation.Mutation.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.True(observation.Mutation.CancellationTokenCanCancel);
        Assert.Equal("POST", observation.Subscription.Method);
        using (var mutation = JsonDocument.Parse(observation.Mutation.Body!))
        {
            Assert.Equal("Rename", mutation.RootElement.GetProperty("operationName").GetString());
            Assert.Equal("Ada", mutation.RootElement.GetProperty("variables").GetProperty("name").GetString());
        }

        Assert.Equal("POST", observation.NestedInputMutation.Method);
        using (var mutation = JsonDocument.Parse(observation.NestedInputMutation.Body!))
        {
            var filter = mutation.RootElement.GetProperty("variables").GetProperty("filter");
            Assert.Equal("ACTIVE", filter.GetProperty("status").GetString());
            Assert.Equal(new[] { "one", "two" }, filter.GetProperty("labels").EnumerateArray().Select(value => value.GetString()));
            Assert.False(filter.TryGetProperty("optional", out _));
        }

        Assert.Equal("POST", observation.Batch.Method);
        Assert.True(observation.Batch.CancellationTokenCanCancel);
        using (var batch = JsonDocument.Parse(observation.Batch.Body!))
        {
            Assert.Equal(JsonValueKind.Array, batch.RootElement.ValueKind);
            Assert.Equal(2, batch.RootElement.GetArrayLength());
            Assert.All(batch.RootElement.EnumerateArray(), operation =>
                Assert.Equal("Greet", operation.GetProperty("operationName").GetString()));
        }

        Assert.Equal("POST", observation.AnonymousQuery.Method);
        using (var anonymousRequest = JsonDocument.Parse(observation.AnonymousQuery.Body!))
        {
            Assert.Equal(string.Empty, anonymousRequest.RootElement.GetProperty("operationName").GetString());
            Assert.False(anonymousRequest.RootElement.TryGetProperty("variables", out _));
        }
        Assert.Equal("POST", observation.LongQueryFallback.Method);
        Assert.Equal(2, observation.IncrementalParts);
        Assert.True(observation.Incremental.CancellationTokenCanCancel);
        Assert.Equal("Hello Ada|Hello again", observation.IncrementalValues);
        Assert.Equal(2, observation.QuotedBoundaryIncrementalParts);
        Assert.Equal("Hello Ada|Hello again", observation.QuotedBoundaryIncrementalValues);
        Assert.Equal(1, observation.SingleIncrementalParts);
        Assert.Equal("Hello Ada", observation.SingleIncrementalValues);
        Assert.Equal("Hello Ada", observation.GreetResponse);
        Assert.Equal("Updated Ada", observation.MutationResponse);
        Assert.Equal("Hello Ada|Hello Ada again", observation.BatchResponses);
        Assert.True(observation.NullResponseWasMaterialized);
        Assert.True(observation.NullResponseDataIsNull);
        Assert.True(observation.NullResponseErrorsIsNull);
        Assert.Equal("partial result", observation.GraphQlErrorMessage);
        Assert.Equal("Hello Ada", observation.GraphQlErrorPartialData);
        Assert.Contains(nameof(HttpRequestException), observation.HttpFailure, StringComparison.Ordinal);
    }

    private static async Task<DisabledTransportObservation> ObserveDisabledTransportOptionsAsync(Assembly assembly, string backend, CancellationToken cancellationToken)
    {
        var clientType = assembly.GetType($"Parity.{backend}.GraphQLClient", throwOnError: true)!;
        var client = CreateClient(clientType, out var queryHandler);
        var operation = CreateOperation(assembly, backend, "Greet", "Ada");
        _ = await InvokeAsync(client, operation, cancellationToken);

        client = CreateClient(clientType, out var batchHandler);
        var responseType = assembly.GetType($"Parity.{backend}.GreetResponse", throwOnError: true)!;
        var variablesType = assembly.GetType($"Parity.{backend}.GreetVariables", throwOnError: true)!;
        var contractType = assembly.GetType($"Parity.{backend}.IGraphQLOperation`2", throwOnError: true)!
            .MakeGenericType(responseType, variablesType);
        var operations = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contractType))!;
        operations.Add(operation);
        var method = clientType.GetMethods().Single(candidate => candidate.Name == "ExecuteBatchAsync")
            .MakeGenericMethod(responseType, variablesType);
        var task = (Task)method.Invoke(client, [operations, cancellationToken])!;
        var failure = await CaptureTaskFailureAsync(async () => await task.WaitAsync(cancellationToken));
        return new(RequestSnapshot.From(queryHandler), RequestSnapshot.From(batchHandler), failure);
    }

    private static async Task<InterfaceResponseObservation> ObserveInterfaceResponseAsync(Assembly assembly, string backend, string json)
    {
        var clientType = assembly.GetType($"Parity.{backend}.GraphQLClient", throwOnError: true)!;
        var client = CreateClient(clientType, out var handler, json);
        var operation = CreateInterfaceOperation(assembly, backend);
        var response = await InvokeAsync(client, operation, TestContext.Current.CancellationToken);
        var node = ReadDataProperty(response, "Node")!;
        var concreteValue = node.GetType().GetProperty("Value")?.GetValue(node) ?? node;
        return new(RequestSnapshot.From(handler), concreteValue.GetType().Name,
            concreteValue.GetType().GetProperty("Id")?.GetValue(concreteValue)?.ToString(),
            concreteValue.GetType().GetProperty("Sku")?.GetValue(concreteValue)?.ToString());
    }

    private static async Task<string> ObserveInterfaceFailureAsync(Assembly assembly, string backend, string json)
    {
        var clientType = assembly.GetType($"Parity.{backend}.GraphQLClient", throwOnError: true)!;
        var client = CreateClient(clientType, out _, json);
        return await CaptureFailureAsync(() => InvokeAsync(client, CreateInterfaceOperation(assembly, backend), TestContext.Current.CancellationToken));
    }

    private static object CreateInterfaceOperation(Assembly assembly, string backend)
    {
        var variablesType = assembly.GetType($"Parity.{backend}.NodeByIdVariables", throwOnError: true)!;
        var variables = Activator.CreateInstance(variablesType)!;
        variablesType.GetProperty("Id")!.SetValue(variables, "p-1");
        var operationType = assembly.GetType($"Parity.{backend}.NodeByIdOperation", throwOnError: true)!;
        return Activator.CreateInstance(operationType, variables)!;
    }

    private static async Task<UnionResponseObservation> ObserveUnionResponseAsync(Assembly assembly, string backend, string json)
    {
        var clientType = assembly.GetType($"Parity.{backend}.GraphQLClient", throwOnError: true)!;
        var client = CreateClient(clientType, out var handler, json);
        var response = await InvokeAsync(client, CreateUnionOperation(assembly, backend), TestContext.Current.CancellationToken);
        var values = ((IEnumerable)ReadDataProperty(response, "Search")!).Cast<object>()
            .Select(value => value.GetType().GetProperty("Value")!.GetValue(value)!)
            .Select(value => value.GetType().Name == "User"
                ? $"User:{value.GetType().GetProperty("Name")!.GetValue(value)}"
                : $"Product:{value.GetType().GetProperty("Sku")!.GetValue(value)}");
        return new(RequestSnapshot.From(handler), string.Join("|", values));
    }

    private static async Task<string> ObserveUnionFailureAsync(Assembly assembly, string backend, string json)
    {
        var clientType = assembly.GetType($"Parity.{backend}.GraphQLClient", throwOnError: true)!;
        var client = CreateClient(clientType, out _, json);
        return await CaptureFailureAsync(() => InvokeAsync(client, CreateUnionOperation(assembly, backend), TestContext.Current.CancellationToken));
    }

    private static object CreateUnionOperation(Assembly assembly, string backend)
    {
        var operationType = assembly.GetType($"Parity.{backend}.SearchByTypeOperation", throwOnError: true)!;
        return Activator.CreateInstance(operationType, [null])!;
    }

    private static async Task<TransportObservation> ObserveClientAsync(Assembly assembly, string backend, CancellationToken cancellationToken)
    {
        var clientType = assembly.GetType($"Parity.{backend}.GraphQLClient", throwOnError: true)!;
        var client = CreateClient(clientType, out var getHandler);
        var greetOperation = CreateOperation(assembly, backend, "Greet", "Ada + & Sons");
        var greetResult = await InvokeAsync(client, greetOperation, cancellationToken);
        var greetResponse = (string?)ReadDataProperty(greetResult, "Greet");

        client = CreateClient(clientType, out var enumHandler, "{\"data\":{\"status\":\"ACTIVE\"}}");
        var enumOperation = CreateEnumOperation(assembly, backend);
        var enumResult = await InvokeAsync(client, enumOperation, cancellationToken);
        var enumResponse = ReadDataProperty(enumResult, "Status")?.ToString();

        client = CreateClient(clientType, out var mutationHandler);
        var mutation = CreateOperation(assembly, backend, "Rename", "Ada");
        var mutationResult = await InvokeAsync(client, mutation, cancellationToken);
        var mutationResponse = (string?)ReadDataProperty(mutationResult, "Rename");

        client = CreateClient(clientType, out var nestedInputHandler, "{\"data\":{\"search\":\"Found\"}}");
        var nestedInputResult = await InvokeAsync(client, CreateNestedInputOperation(assembly, backend), cancellationToken);
        _ = ReadDataProperty(nestedInputResult, "Search");

        client = CreateClient(clientType, out var subscriptionHandler, "{\"data\":{\"created\":\"New item\"}}");
        var subscriptionType = assembly.GetType($"Parity.{backend}.CreatedOperation", throwOnError: true)!;
        var subscriptionVariables = Activator.CreateInstance(assembly.GetType($"Parity.{backend}.CreatedVariables", throwOnError: true)!)!;
        _ = await InvokeAsync(client, Activator.CreateInstance(subscriptionType, subscriptionVariables)!, cancellationToken);

        client = CreateClient(clientType, out _, "{\"data\":{\"greet\":\"Hello Ada\"},\"errors\":[{\"message\":\"partial result\"}]}");
        var graphqlErrorResult = await InvokeAsync(client, greetOperation, cancellationToken);
        var graphqlErrorMessage = ReadFirstErrorMessage(graphqlErrorResult);
        var graphqlErrorPartialData = (string?)ReadDataProperty(graphqlErrorResult, "Greet");

        client = CreateClient(clientType, out _, "null");
        var nullResponse = await InvokeAsync(client, greetOperation, cancellationToken);
        var nullResponseType = nullResponse.GetType();

        client = CreateClient(clientType, out var batchHandler,
            "[{\"data\":{\"greet\":\"Hello Ada\"}},{\"data\":{\"greet\":\"Hello Ada again\"}}]");
        var responseType = assembly.GetType($"Parity.{backend}.GreetResponse", throwOnError: true)!;
        var variablesType = assembly.GetType($"Parity.{backend}.GreetVariables", throwOnError: true)!;
        var contractType = assembly.GetType($"Parity.{backend}.IGraphQLOperation`2", throwOnError: true)!
            .MakeGenericType(responseType, variablesType);
        var operations = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contractType))!;
        operations.Add(greetOperation);
        operations.Add(greetOperation);
        var batchMethod = clientType.GetMethods().Single(method => method.Name == "ExecuteBatchAsync")
            .MakeGenericMethod(responseType, variablesType);
        var batchTask = (Task)batchMethod.Invoke(client, [operations, cancellationToken])!;
        await batchTask.WaitAsync(cancellationToken);
        var batchResponses = ((IEnumerable)batchTask.GetType().GetProperty("Result")!.GetValue(batchTask)!)
            .Cast<object>()
            .Select(response => (string?)ReadDataProperty(response, "Greet"))
            .ToArray();

        client = CreateClient(clientType, out var anonymousHandler);
        var anonymousType = assembly.GetType($"Parity.{backend}.AnonymousOperation", throwOnError: true)!;
        var anonymousOperation = Activator.CreateInstance(anonymousType)!;
        _ = await InvokeAsync(client, anonymousOperation, cancellationToken);

        client = CreateClient(clientType, out var longQueryHandler);
        var longQueryType = assembly.GetType($"Parity.{backend}.LongQueryOperation", throwOnError: true)!;
        var longQuery = Activator.CreateInstance(longQueryType)!;
        _ = await InvokeAsync(client, longQuery, cancellationToken);

        client = CreateClient(clientType, out var noVariablesHandler);
        var noVariablesType = assembly.GetType($"Parity.{backend}.NamedQueryWithoutVariablesOperation", throwOnError: true)!;
        _ = await InvokeAsync(client, Activator.CreateInstance(noVariablesType)!, cancellationToken);

        client = CreateClient(clientType, out var emptyVariablesHandler);
        var emptyVariablesOperation = CreateOperation(assembly, backend, "Greet", "Ada");
        emptyVariablesOperation.GetType().GetProperty("Variables")!.PropertyType.GetProperty("Name")!
            .SetValue(emptyVariablesOperation.GetType().GetProperty("Variables")!.GetValue(emptyVariablesOperation), null);
        _ = await InvokeAsync(client, emptyVariablesOperation, cancellationToken);

        client = CreateClient(clientType, out var getUrlAtLimitHandler);
        var boundaryOperationType = assembly.GetType($"Parity.{backend}.UrlLengthBoundaryOperation", throwOnError: true)!;
        var getUrlAtLimit = Activator.CreateInstance(boundaryOperationType)!;
        boundaryOperationType.GetProperty("Query")!.SetValue(getUrlAtLimit, QueryForGetUriLength(2048));
        _ = await InvokeAsync(client, getUrlAtLimit, cancellationToken);

        client = CreateClient(clientType, out var getUrlOverLimitHandler);
        var getUrlOverLimit = Activator.CreateInstance(boundaryOperationType)!;
        boundaryOperationType.GetProperty("Query")!.SetValue(getUrlOverLimit, QueryForGetUriLength(2049));
        _ = await InvokeAsync(client, getUrlOverLimit, cancellationToken);

        client = CreateClient(clientType, out var incrementalHandler, MultipartResponse(), "multipart/mixed; boundary=graphql");
        var incrementalMethod = clientType.GetMethods().Single(method => method.Name == "ExecuteIncrementalAsync")
            .MakeGenericMethod(responseType, variablesType);
        var incremental = incrementalMethod.Invoke(client, [greetOperation, cancellationToken])!;
        var drain = typeof(GeneratedClientBehaviorTests).GetMethod(nameof(DrainAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(assembly.GetType($"Parity.{backend}.GraphQLResponse`1", throwOnError: true)!.MakeGenericType(responseType));
        var drainTask = (Task)drain.Invoke(null, [incremental, cancellationToken])!;
        await drainTask.WaitAsync(cancellationToken);
        var parts = (ICollection)drainTask.GetType().GetProperty("Result")!.GetValue(drainTask)!;
        var incrementalValues = string.Join("|", parts.Cast<object>().Select(response => ReadDataProperty(response, "Greet")));

        client = CreateClient(clientType, out var singleIncrementalHandler);
        var singleIncremental = incrementalMethod.Invoke(client, [greetOperation, cancellationToken])!;
        var singleDrainTask = (Task)drain.Invoke(null, [singleIncremental, cancellationToken])!;
        await singleDrainTask.WaitAsync(cancellationToken);
        var singleParts = (ICollection)singleDrainTask.GetType().GetProperty("Result")!.GetValue(singleDrainTask)!;
        var singleIncrementalValues = string.Join("|", singleParts.Cast<object>().Select(response => ReadDataProperty(response, "Greet")));

        client = CreateClient(clientType, out var quotedBoundaryHandler, MultipartResponse("QuotedGraphql"), "Multipart/Mixed; boundary=\"QuotedGraphql\"");
        var quotedBoundaryIncremental = incrementalMethod.Invoke(client, [greetOperation, cancellationToken])!;
        var quotedBoundaryDrainTask = (Task)drain.Invoke(null, [quotedBoundaryIncremental, cancellationToken])!;
        await quotedBoundaryDrainTask.WaitAsync(cancellationToken);
        var quotedBoundaryParts = (ICollection)quotedBoundaryDrainTask.GetType().GetProperty("Result")!.GetValue(quotedBoundaryDrainTask)!;
        var quotedBoundaryIncrementalValues = string.Join("|", quotedBoundaryParts.Cast<object>().Select(response => ReadDataProperty(response, "Greet")));

        client = CreateClient(clientType, out _, statusCode: HttpStatusCode.BadRequest);
        var httpFailure = await CaptureFailureAsync(() => InvokeAsync(client, greetOperation, cancellationToken));

        return new(
            RequestSnapshot.From(getHandler),
            RequestSnapshot.From(enumHandler),
            RequestSnapshot.From(mutationHandler),
            RequestSnapshot.From(nestedInputHandler),
            RequestSnapshot.From(subscriptionHandler),
            RequestSnapshot.From(batchHandler),
            RequestSnapshot.From(anonymousHandler),
            RequestSnapshot.From(longQueryHandler),
            RequestSnapshot.From(noVariablesHandler),
            RequestSnapshot.From(emptyVariablesHandler),
            RequestSnapshot.From(getUrlAtLimitHandler),
            RequestSnapshot.From(getUrlOverLimitHandler),
            RequestSnapshot.From(incrementalHandler),
            parts.Count,
            incrementalValues,
            RequestSnapshot.From(quotedBoundaryHandler),
            quotedBoundaryParts.Count,
            quotedBoundaryIncrementalValues,
            RequestSnapshot.From(singleIncrementalHandler),
            singleParts.Count,
            singleIncrementalValues,
            greetResponse,
            enumResponse,
            mutationResponse,
            string.Join("|", batchResponses),
            graphqlErrorMessage,
            nullResponse is not null,
            nullResponseType.GetProperty("Data")!.GetValue(nullResponse) is null,
            nullResponseType.GetProperty("Errors")!.GetValue(nullResponse) is null,
            graphqlErrorPartialData,
            httpFailure);
    }

    private static object CreateClient(Type clientType, out RecordingHandler handler, string response = "{\"data\":{\"greet\":\"Hello Ada\",\"rename\":\"Updated Ada\"}}", string contentType = "application/json", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        handler = new(response, contentType, statusCode);
        var httpClient = new HttpClient(handler);
        return Activator.CreateInstance(clientType, httpClient, new Uri("https://example.test/graphql"))!;
    }

    private static object CreateOperation(Assembly assembly, string backend, string operationName, string name)
    {
        var variablesType = assembly.GetType($"Parity.{backend}.{operationName}Variables", throwOnError: true)!;
        var variables = Activator.CreateInstance(variablesType)!;
        variablesType.GetProperty("Name")!.SetValue(variables, name);
        var operationType = assembly.GetType($"Parity.{backend}.{operationName}Operation", throwOnError: true)!;
        return Activator.CreateInstance(operationType, variables)!;
    }

    private static object CreateEnumOperation(Assembly assembly, string backend)
    {
        var variablesType = assembly.GetType($"Parity.{backend}.ReadStatusVariables", throwOnError: true)!;
        var variables = Activator.CreateInstance(variablesType)!;
        var inputType = variablesType.GetProperty("Input")!.PropertyType;
        variablesType.GetProperty("Input")!.SetValue(variables, Enum.Parse(inputType, "ACTIVE"));
        var operationType = assembly.GetType($"Parity.{backend}.ReadStatusOperation", throwOnError: true)!;
        return Activator.CreateInstance(operationType, variables)!;
    }

    private static object CreateNestedInputOperation(Assembly assembly, string backend)
    {
        var variablesType = assembly.GetType($"Parity.{backend}.SearchByFilterVariables", throwOnError: true)!;
        var variables = Activator.CreateInstance(variablesType)!;
        var filterProperty = variablesType.GetProperty("Filter")!;
        var filter = Activator.CreateInstance(filterProperty.PropertyType)!;
        var statusProperty = filterProperty.PropertyType.GetProperty("Status")!;
        statusProperty.SetValue(filter, Enum.Parse(statusProperty.PropertyType, "ACTIVE"));
        var labelsProperty = filterProperty.PropertyType.GetProperty("Labels")!;
        var labels = (IList)Activator.CreateInstance(labelsProperty.PropertyType)!;
        labels.Add("one");
        labels.Add("two");
        labelsProperty.SetValue(filter, labels);
        filterProperty.SetValue(variables, filter);
        var operationType = assembly.GetType($"Parity.{backend}.SearchByFilterOperation", throwOnError: true)!;
        return Activator.CreateInstance(operationType, variables)!;
    }

    private static async Task<object> InvokeAsync(object client, object operation, CancellationToken cancellationToken)
    {
        var method = client.GetType().GetMethods().Single(candidate => candidate.Name == "ExecuteAsync")
            .MakeGenericMethod(operation.GetType().GetInterfaces().Single(type =>
                    type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("IGraphQLOperation", StringComparison.Ordinal))
                .GetGenericArguments());
        var task = (Task)method.Invoke(client, [operation, cancellationToken])!;
        await task.WaitAsync(cancellationToken);
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static object? ReadDataProperty(object response, string name)
    {
        var data = response.GetType().GetProperty("Data")!.GetValue(response)!;
        return data.GetType().GetProperty(name)!.GetValue(data);
    }

    private static string? ReadFirstErrorMessage(object response)
    {
        var errors = (Array?)response.GetType().GetProperty("Errors")!.GetValue(response);
        return errors?.Length > 0 ? (string?)errors.GetValue(0)!.GetType().GetProperty("Message")!.GetValue(errors.GetValue(0)) : null;
    }

    private static async Task<string> CaptureFailureAsync(Func<Task<object>> operation)
    {
        try
        {
            _ = await operation();
            return "No exception";
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().FullName}: {exception.Message}";
        }
    }

    private static async Task<string> CaptureTaskFailureAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return "No exception";
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().FullName}: {exception.Message}";
        }
    }

    private static async Task<List<T>> DrainAsync<T>(IAsyncEnumerable<T> sequence, CancellationToken cancellationToken)
    {
        var values = new List<T>();
        await foreach (var value in sequence.WithCancellation(cancellationToken)) values.Add(value);
        return values;
    }

    private static string MultipartResponse(string boundary = "graphql") => string.Join("\r\n",
        "--" + boundary, "Content-Type: application/json", "", "{\"data\":{\"greet\":\"Hello Ada\"}}",
        "--" + boundary, "Content-Type: application/json", "", "{\"data\":{\"greet\":\"Hello again\"}}", "--" + boundary + "--", "");

    private static string QueryForGetUriLength(int targetLength)
    {
        for (var valueLength = 0; valueLength < targetLength; valueLength++)
        {
            var query = "query Boundary { " + new string('x', valueLength) + " }";
            var builder = new UriBuilder(new Uri("https://example.test/graphql"))
            {
                Query = "query=" + WebUtility.UrlEncode(query) + "&operationName=Boundary"
            };
            if (builder.Uri.AbsoluteUri.Length == targetLength) return query;
        }

        throw new InvalidOperationException($"Unable to create a GET URI of length {targetLength}.");
    }

    private sealed record RequestSnapshot(string Method, string? Uri, string? Body, string? ContentType, bool CancellationTokenCanCancel)
    {
        public static RequestSnapshot From(RecordingHandler handler) => new(
            handler.Method?.Method ?? "", handler.Uri?.ToString(), handler.Body, handler.ContentType, handler.CancellationTokenCanCancel);
    }

    private sealed record TransportObservation(
        RequestSnapshot Get,
        RequestSnapshot EnumQuery,
        RequestSnapshot Mutation,
        RequestSnapshot NestedInputMutation,
        RequestSnapshot Subscription,
        RequestSnapshot Batch,
        RequestSnapshot AnonymousQuery,
        RequestSnapshot LongQueryFallback,
        RequestSnapshot NamedQueryWithoutVariables,
        RequestSnapshot EmptyVariablesQuery,
        RequestSnapshot GetUrlAtLimit,
        RequestSnapshot GetUrlOverLimit,
        RequestSnapshot Incremental,
        int IncrementalParts,
        string IncrementalValues,
        RequestSnapshot QuotedBoundaryIncremental,
        int QuotedBoundaryIncrementalParts,
        string QuotedBoundaryIncrementalValues,
        RequestSnapshot SingleIncremental,
        int SingleIncrementalParts,
        string SingleIncrementalValues,
        string? GreetResponse,
        string? EnumResponse,
        string? MutationResponse,
        string BatchResponses,
        string? GraphQlErrorMessage,
        bool NullResponseWasMaterialized,
        bool NullResponseDataIsNull,
        bool NullResponseErrorsIsNull,
        string? GraphQlErrorPartialData,
        string HttpFailure);

    private sealed record DisabledTransportObservation(RequestSnapshot Query, RequestSnapshot BatchRequest, string BatchFailure);

    private sealed record InterfaceResponseObservation(RequestSnapshot Request, string ConcreteResponseType, string? Id, string? Sku);

    private sealed record UnionResponseObservation(RequestSnapshot Request, string Variants);

    private sealed class RecordingHandler(string response, string contentType = "application/json", HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? Body { get; private set; }
        public string? ContentType { get; private set; }
        public bool CancellationTokenCanCancel { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            ContentType = request.Content?.Headers.ContentType?.ToString();
            CancellationTokenCanCancel = cancellationToken.CanBeCanceled;
            var content = new StringContent(response, Encoding.UTF8);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            return new HttpResponseMessage(statusCode) { Content = content };
        }
    }

    private sealed class ClientFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "salep-client-behavior-" + Guid.NewGuid().ToString("N"));
        private readonly string schema;
        private readonly string operations;
        private readonly string scribanConfig;
        private readonly string unionRepresentation;
        private readonly string targetFramework;
        private readonly string languageVersion;
        private readonly bool includeInterface;

        public ClientFixture(bool useHttpGet = true, bool enableBatching = true, string unionRepresentation = "dunet", string targetFramework = "net10.0", string languageVersion = "latest", bool includeInterface = false)
        {
            this.unionRepresentation = unionRepresentation;
            this.targetFramework = targetFramework;
            this.languageVersion = languageVersion;
            this.includeInterface = includeInterface;
            Directory.CreateDirectory(root);
            schema = Path.Combine(root, "schema.graphql");
            var interfaceSchema = includeInterface ? "interface Node { id: ID! } type User implements Node { id: ID! name: String! } type Product implements Node { id: ID! sku: String! } union SearchResult = User | Product" : string.Empty;
            var interfaceField = includeInterface ? " node(id: ID!): Node search: [SearchResult!]!" : string.Empty;
            var interfaceOperation = includeInterface ? " query NodeById($id: ID!) { node(id: $id) { __typename ... on User { id name } ... on Product { id sku } } } query SearchByType { search { __typename ... on User { id name } ... on Product { id sku } } }" : string.Empty;
            File.WriteAllText(schema, $"enum Status {{ ACTIVE }} {interfaceSchema} input SearchFilter {{ status: Status! labels: [String!]! optional: String }} type Query {{ greet(name: String!): String! status(input: Status!): Status!{interfaceField} }} type Mutation {{ rename(name: String!): String! search(filter: SearchFilter!): String! }} type Subscription {{ created: String! }}");
            operations = Path.Combine(root, "graphql");
            Directory.CreateDirectory(operations);
            File.WriteAllText(Path.Combine(operations, "operations.graphql"), $"query Greet($name: String!) {{ greet(name: $name) }} query ReadStatus($input: Status!) {{ status(input: $input) }}{interfaceOperation} mutation Rename($name: String!) {{ rename(name: $name) }} mutation SearchByFilter($filter: SearchFilter!) {{ search(filter: $filter) }} subscription Created {{ created }}");
            scribanConfig = WriteConfig("scriban.json", "Scriban", useHttpGet, enableBatching);
        }

        public Assembly GenerateAndCompile()
        {
            ScribanGenerator.Generate(new(scribanConfig, root, new(targetFramework, languageVersion, [scribanConfig])));

            var sources = new List<string>();
            foreach (var backend in new[] { "Scriban" })
                sources.AddRange(Directory.EnumerateFiles(Path.Combine(root, backend), "*.cs").Select(File.ReadAllText));
            sources.Add(HarnessSource("Scriban"));

            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("SalepClientBehavior_" + Guid.NewGuid().ToString("N"), sources.Select(source =>
                    CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion == "preview" ? LanguageVersion.Preview : LanguageVersion.Latest), cancellationToken: TestContext.Current.CancellationToken)),
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            using var image = new MemoryStream();
            var emit = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
            return AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(image.ToArray()));
        }

        private string WriteConfig(string fileName, string backend, bool useHttpGet, bool enableBatching)
        {
            var config = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["version"] = 1,
                ["kind"] = "client",
                ["schema"] = schema,
                ["operations"] = operations,
                ["namespace"] = $"Parity.{backend}",
                ["clientName"] = "GraphQLClient",
                ["output"] = backend,
                ["unionRepresentation"] = unionRepresentation,
                ["scalarPreset"] = "builtin",
                ["useHttpGet"] = useHttpGet,
                ["enableBatching"] = enableBatching,
                ["maxGetUrlLength"] = 2048,
                ["omitUnusedVariables"] = true,
                ["inlineDefaultVariables"] = false
            };
            var path = Path.Combine(root, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(config));
            return path;
        }

        private static string HarnessSource(string backend) => $$"""
            namespace Parity.{{backend}};

            public sealed class AnonymousOperation : IGraphQLOperation<GreetResponse, GreetVariables>
            {
                public string OperationName => string.Empty;
                public string Query => "{ __typename }";
                public GreetVariables? Variables => null;
            }

            public sealed class LongQueryOperation : IGraphQLOperation<GreetResponse, GreetVariables>
            {
                public string OperationName => "LongQuery";
                public string Query => "query LongQuery { " + new string('x', 2200) + " }";
                public GreetVariables? Variables => null;
            }

            public sealed class NamedQueryWithoutVariablesOperation : IGraphQLOperation<GreetResponse, GreetVariables>
            {
                public string OperationName => "NamedQueryWithoutVariables";
                public string Query => "query NamedQueryWithoutVariables { __typename }";
                public GreetVariables? Variables => null;
            }

            public sealed class UrlLengthBoundaryOperation : IGraphQLOperation<GreetResponse, GreetVariables>
            {
                public string OperationName => "Boundary";
                public string Query { get; set; } = string.Empty;
                public GreetVariables? Variables => null;
            }
            """;

        public void Dispose() => Directory.Delete(root, recursive: true);
    }
}
