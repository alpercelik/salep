using HotChocolate.Language;
using Salep.Samples.GraphQLServer.Directives;
using Salep.Samples.GraphQLServer.Models;
using Salep.Samples.GraphQLServer.Models.Inputs;
using Salep.Samples.GraphQLServer.Resolvers;
using Salep.Samples.GraphQLServer.Services;
using ModelNode = Salep.Samples.GraphQLServer.Models.INode;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SampleData>();
builder.Services.AddGraphQLServer()
    // The sample operations exercise nested lists and union fragments with estimated costs above 1,000.
    .ModifyCostOptions(options => options.MaxFieldCost = 5_000)
    .AddQueryType<QueryType>()
    .AddMutationType<MutationType>()
    .AddSubscriptionType<SubscriptionResolvers>(type => type.Name("Subscription"))
    .AddType<UserType>()
    .AddType<RoleType>()
    .AddType<Post>()
    .AddType<Comment>()
    .AddType<ModelNode>()
    .AddType<IAccount>()
    .AddType<INamed>()
    .AddType<ITimestamped>()
    .AddType<ISearchResult>()
    .AddType<UserFilterInputType>()
    .AddType<MetadataInputType>()
    .AddType(new HotChocolate.Types.AnyType("Json", "A JSON value."))
    .AddType(new HotChocolate.Types.AnyType("BigInt", "An arbitrary precision integer."))
    .AddDirectiveType<OpTagDirectiveType>()
    .AddDirectiveType<SchemaTagDirectiveType>()
    .AddDirectiveType<AuthDirectiveType>()
    .AddDirectiveType<UpperDirectiveType>()
    .ConfigureSchema(schema => schema.SetSchema(descriptor =>
    {
        descriptor.Directive("schemaTag", new ArgumentNode("name", new StringValueNode("base")));
        descriptor.Directive("schemaTag", new ArgumentNode("name", new StringValueNode("extended")));
    }))
    .AddInMemorySubscriptions();

var app = builder.Build();
if (args is ["--export-schema", var outputPath])
{
    var executor = await HotChocolate.Execution.RequestExecutorServiceProviderExtensions.GetRequestExecutorAsync(app.Services);
    var schemaSdl = executor.Schema.ToString()
        .Replace("  | DIRECTIVE_DEFINITION", "", StringComparison.Ordinal);
    // Referencing projects can generate clients while another build exports this schema.
    // Publish a complete file and preserve timestamps when the schema is unchanged.
    if (File.Exists(outputPath) && await File.ReadAllTextAsync(outputPath) == schemaSdl)
        return;
    var temporaryPath = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
        await File.WriteAllTextAsync(temporaryPath, schemaSdl);
        File.Move(temporaryPath, outputPath, overwrite: true);
    }
    finally
    {
        File.Delete(temporaryPath);
    }
    return;
}

app.UseWebSockets();
app.MapGraphQL();
app.MapNitroApp("/graphql/ui");
app.Run();

public partial class Program { }
