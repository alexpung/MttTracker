using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

using MttTracker.Shared;

namespace MttTracker.Client.Services;

/// <summary>
/// Talks to the <c>/api/entries</c> backend. The API scopes every operation to
/// the authenticated user, so the client never passes a user id. A 403 from the
/// API (caller not on the allowlist) surfaces as <see cref="NotAuthorizedException"/>.
/// </summary>
/// <remarks>
/// The backend can be slow or briefly unavailable while it cold-starts, so the
/// last successfully loaded entries are kept in memory and mirrored to
/// <c>localStorage</c>. Pages show that copy immediately
/// (<see cref="GetCachedEntriesAsync"/>) while <see cref="GetEntriesAsync"/>
/// refreshes it from the server, retrying transient failures.
/// </remarks>
public sealed class ApiTournamentService(
    HttpClient http, IJSRuntime js, AuthenticationStateProvider auth) : ITournamentService
{
    // Also removed by logged-out.html, so signing out clears the saved copy.
    private const string CacheKey = "mtttracker.cache.entries";

    // Waits before the 2nd and 3rd attempt at loading the entries.
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];

    // Property names come from the [JsonPropertyName] attributes on the model.
    private static readonly JsonSerializerOptions Json = new();

    private List<TournamentEntry>? _entries;
    private bool _snapshotRead;
    private Task<List<TournamentEntry>>? _inFlight;

    // Bumped by every local change, so a fetch that was already in the air when
    // the change happened is recognised as out of date and repeated.
    private int _version;

    public async Task<List<TournamentEntry>?> GetCachedEntriesAsync()
    {
        if (_entries is null && !_snapshotRead)
        {
            _snapshotRead = true;
            var version = _version;
            var snapshot = await ReadSnapshotAsync();
            if (_entries is null && version == _version)
            {
                _entries = snapshot;
            }
        }
        return _entries?.ToList();
    }

    public async Task<List<TournamentEntry>> GetEntriesAsync()
    {
        // Callers that overlap (e.g. switching tabs mid-load) share one request.
        var task = _inFlight ??= FetchAsync();
        try
        {
            return (await task).ToList();
        }
        finally
        {
            if (ReferenceEquals(_inFlight, task))
            {
                _inFlight = null;
            }
        }
    }

    private async Task<List<TournamentEntry>> FetchAsync()
    {
        List<TournamentEntry> fetched;
        int version;
        do
        {
            version = _version;
            fetched = await GetWithRetryAsync();
        }
        while (version != _version);

        _entries = fetched;
        await WriteSnapshotAsync();
        return fetched;
    }

    private async Task<List<TournamentEntry>> GetWithRetryAsync()
    {
        for (var attempt = 0; ; attempt++)
        {
            var lastAttempt = attempt == RetryDelays.Length;
            try
            {
                using var response = await http.GetAsync("api/entries");
                await EnsureAllowedAsync(response);
                if (lastAttempt || !IsTransient(response.StatusCode))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadFromJsonAsync<List<TournamentEntry>>()
                        ?? new List<TournamentEntry>();
                }
            }
            // No status code means the request never got an answer (network
            // drop, gateway gave up) — worth another go.
            catch (HttpRequestException ex) when (ex.StatusCode is null && !lastAttempt)
            {
            }

            await Task.Delay(RetryDelays[attempt]);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || (int)status >= 500;

    /// <summary>Creates the entry and returns the server's copy (with its assigned id).</summary>
    public async Task<TournamentEntry> AddAsync(TournamentEntry entry)
    {
        using var response = await http.PostAsJsonAsync("api/entries", entry);
        await EnsureAllowedAsync(response);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TournamentEntry>()
            ?? throw new InvalidOperationException("Create response had no body.");
        await ApplyAsync(list => list.Add(created));
        return created;
    }

    /// <summary>Updates the entry and returns the server's copy.</summary>
    public async Task<TournamentEntry> UpdateAsync(TournamentEntry entry)
    {
        using var response = await http.PutAsJsonAsync($"api/entries/{entry.Id}", entry);
        await EnsureAllowedAsync(response);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TournamentEntry>()
            ?? throw new InvalidOperationException("Update response had no body.");
        await ApplyAsync(list =>
        {
            list.RemoveAll(e => e.Id == updated.Id);
            list.Add(updated);
        });
        return updated;
    }

    public async Task DeleteAsync(string id)
    {
        using var response = await http.DeleteAsync($"api/entries/{id}");
        await EnsureAllowedAsync(response);
        response.EnsureSuccessStatusCode();
        await ApplyAsync(list => list.RemoveAll(e => e.Id == id));
    }

    public async Task<ImportResult> ImportAsync(IReadOnlyList<TournamentEntry> entries)
    {
        var existingIds = (await GetEntriesAsync()).Select(e => e.Id).ToHashSet();
        var added = 0;
        var updated = 0;
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Id) && existingIds.Contains(entry.Id))
            {
                await UpdateAsync(entry);
                updated++;
            }
            else
            {
                // The API assigns a fresh id on create.
                await AddAsync(entry);
                added++;
            }
        }
        return new ImportResult(added, updated);
    }

    /// <summary>Mirrors a change the server has accepted into the cached copy.</summary>
    private async Task ApplyAsync(Action<List<TournamentEntry>> change)
    {
        _version++;
        if (_entries is null)
        {
            return;
        }

        var list = _entries.ToList();
        change(list);
        // Newest first, matching the order the API returns.
        _entries = list.OrderByDescending(e => e.Date).ToList();
        await WriteSnapshotAsync();
    }

    private async Task EnsureAllowedAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            // Whoever is signed in may not see this data, saved copy included.
            _version++;
            _entries = null;
            _snapshotRead = true;
            try
            {
                await js.InvokeVoidAsync("localStorage.removeItem", CacheKey);
            }
            catch
            {
                // Storage unavailable — nothing was saved there either.
            }
            throw new NotAuthorizedException();
        }
    }

    // The saved copy is tagged with the account it belongs to, so a different
    // GitHub user signing in on the same browser is never shown it.
    private async Task<string?> GetUserIdAsync() =>
        (await auth.GetAuthenticationStateAsync()).User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private async Task<List<TournamentEntry>?> ReadSnapshotAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", CacheKey);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json, Json);
            var userId = await GetUserIdAsync();
            return snapshot?.Entries is not null && !string.IsNullOrEmpty(userId) && snapshot.UserId == userId
                ? snapshot.Entries
                : null;
        }
        catch
        {
            // Unreadable or blocked storage just means there is no saved copy.
            return null;
        }
    }

    private async Task WriteSnapshotAsync()
    {
        try
        {
            var userId = await GetUserIdAsync();
            if (_entries is null || string.IsNullOrEmpty(userId))
            {
                return;
            }
            await js.InvokeVoidAsync("localStorage.setItem", CacheKey,
                JsonSerializer.Serialize(new Snapshot(userId, _entries), Json));
        }
        catch
        {
            // Saving the copy is best-effort (private mode, quota).
        }
    }

    private sealed record Snapshot(string UserId, List<TournamentEntry> Entries);
}
