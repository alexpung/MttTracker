using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MttTracker.Api;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// JSON options shared by the HTTP layer and the Cosmos serializer. Property
// names come from explicit [JsonPropertyName] attributes on the model, so no
// naming policy is applied; reads are case-insensitive for resilience.
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

builder.Services.AddSingleton(jsonOptions);

builder.Services.AddSingleton(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()["CosmosConnectionString"]
        ?? throw new InvalidOperationException("CosmosConnectionString is not configured.");
    return new CosmosClient(connectionString, new CosmosClientOptions
    {
        Serializer = new CosmosSystemTextJsonSerializer(jsonOptions),
    });
});

builder.Services.AddSingleton<CosmosEntryStore>();
builder.Services.AddSingleton<RequestAuthorizer>();

builder.Build().Run();
