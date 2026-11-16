using System.Net;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

using MttTracker.Shared;

namespace MttTracker.Api;

/// <summary>
/// Persists <see cref="TournamentEntry"/> documents in a Cosmos DB container
/// partitioned by <c>/userId</c>. All operations are scoped to a single user.
/// </summary>
public sealed class CosmosEntryStore
{
    private readonly CosmosClient _client;
    private readonly string _databaseId;
    private readonly string _containerId;
    private readonly Container _container;

    public CosmosEntryStore(CosmosClient client, IConfiguration config)
    {
        _client = client;
        _databaseId = config["CosmosDatabase"] ?? "MttTracker";
        _containerId = config["CosmosContainer"] ?? "entries";
        _container = client.GetContainer(_databaseId, _containerId);
    }

    // The database and container are only created when a write finds them
    // missing (a brand-new deployment). Checking for them up front would cost
    // two extra Cosmos round trips on every cold start.
    private async Task EnsureCreatedAsync()
    {
        var db = await _client.CreateDatabaseIfNotExistsAsync(_databaseId);
        await db.Database.CreateContainerIfNotExistsAsync(_containerId, "/userId");
    }

    public async Task<List<TournamentEntry>> ListAsync(string userId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.date DESC")
            .WithParameter("@userId", userId);

        var results = new List<TournamentEntry>();
        using var iterator = _container.GetItemQueryIterator<TournamentEntry>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
        try
        {
            while (iterator.HasMoreResults)
            {
                results.AddRange(await iterator.ReadNextAsync());
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No container yet, so nothing has been saved.
        }

        return results;
    }

    public async Task<TournamentEntry?> GetAsync(string userId, string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<TournamentEntry>(
                id, new PartitionKey(userId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TournamentEntry> CreateAsync(TournamentEntry entry)
    {
        entry.Id = Guid.NewGuid().ToString("N");
        try
        {
            return (await _container.CreateItemAsync(
                entry, new PartitionKey(entry.UserId))).Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            await EnsureCreatedAsync();
            return (await _container.CreateItemAsync(
                entry, new PartitionKey(entry.UserId))).Resource;
        }
    }

    public async Task<TournamentEntry> ReplaceAsync(TournamentEntry entry)
    {
        var response = await _container.ReplaceItemAsync(
            entry, entry.Id, new PartitionKey(entry.UserId));
        return response.Resource;
    }

    public async Task DeleteAsync(string userId, string id)
    {
        try
        {
            await _container.DeleteItemAsync<TournamentEntry>(id, new PartitionKey(userId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already gone — treat delete as idempotent.
        }
    }
}
