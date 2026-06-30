using System.Net;
using System.Net.Http.Json;

using MttTracker.Shared;

namespace MttTracker.Client.Services;

/// <summary>
/// Talks to the <c>/api/entries</c> backend. The API scopes every operation to
/// the authenticated user, so the client never passes a user id. Statistics are
/// computed locally from the fetched entries via <see cref="StatsCalculator"/>.
/// A 403 from the API (caller not on the allowlist) surfaces as
/// <see cref="NotAuthorizedException"/>.
/// </summary>
public sealed class TournamentService(HttpClient http)
{
    public async Task<List<TournamentEntry>> GetEntriesAsync()
    {
        var response = await http.GetAsync("api/entries");
        await EnsureAllowedAsync(response);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TournamentEntry>>()
            ?? new List<TournamentEntry>();
    }

    public async Task AddAsync(TournamentEntry entry)
    {
        var response = await http.PostAsJsonAsync("api/entries", entry);
        await EnsureAllowedAsync(response);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(TournamentEntry entry)
    {
        var response = await http.PutAsJsonAsync($"api/entries/{entry.Id}", entry);
        await EnsureAllowedAsync(response);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await http.DeleteAsync($"api/entries/{id}");
        await EnsureAllowedAsync(response);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TournamentStats> GetStatsAsync()
    {
        var entries = await GetEntriesAsync();
        return StatsCalculator.ComputeStats(entries);
    }

    private static Task EnsureAllowedAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new NotAuthorizedException();
        }
        return Task.CompletedTask;
    }
}
