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
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Container? _container;

    public CosmosEntryStore(CosmosClient client, IConfiguration config)
    {
        _client = client;
        _databaseId = config["CosmosDatabase"] ?? "MttTracker";
        _containerId = config["CosmosContainer"] ?? "entries";
    }

    private async Task<Container> GetContainerAsync()
    {
        if (_container is not null)
        {
            return _container;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_container is null)
            {
                var db = await _client.CreateDatabaseIfNotExistsAsync(_databaseId);
                _container = await db.Database.CreateContainerIfNotExistsAsync(
                    _containerId, "/userId");
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _container;
    }

    public async Task<List<TournamentEntry>> ListAsync(string userId)
    {
        var container = await GetContainerAsync();
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.date DESC")
            .WithParameter("@userId", userId);

        var results = new List<TournamentEntry>();
        using var iterator = container.GetItemQueryIterator<TournamentEntry>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync());
        }

        return results;
    }

    public async Task<TournamentEntry?> GetAsync(string userId, string id)
    {
        var container = await GetContainerAsync();
        try
        {
            var response = await container.ReadItemAsync<TournamentEntry>(
                id, new PartitionKey(userId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TournamentEntry> CreateAsync(TournamentEntry entry)
    {
        var container = await GetContainerAsync();
        entry.Id = Guid.NewGuid().ToString("N");
        var response = await container.CreateItemAsync(
            entry, new PartitionKey(entry.UserId));
        return response.Resource;
    }

    public async Task<TournamentEntry> ReplaceAsync(TournamentEntry entry)
    {
        var container = await GetContainerAsync();
        var response = await container.ReplaceItemAsync(
            entry, entry.Id, new PartitionKey(entry.UserId));
        return response.Resource;
    }

    public async Task DeleteAsync(string userId, string id)
    {
        var container = await GetContainerAsync();
        try
        {
            await container.DeleteItemAsync<TournamentEntry>(id, new PartitionKey(userId));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — treat delete as idempotent.
        }
    }
}
