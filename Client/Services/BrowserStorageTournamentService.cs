using System.Text.Json;

using Microsoft.JSInterop;

using MttTracker.Shared;

namespace MttTracker.Client.Services;

/// <summary>
/// Stores tournament entries in the browser's <c>localStorage</c> instead of a
/// backend, so the app can run as a purely static site (e.g. GitHub Pages)
/// with no database and no login. Data never leaves the browser — clearing
/// site data deletes it — so the JSON export/import on the Tournaments page is
/// the backup story.
/// </summary>
public sealed class BrowserStorageTournamentService(IJSRuntime js) : ITournamentService
{
    private const string StorageKey = "mtttracker.entries";
    private const string LocalUserId = "local";

    // Property names come from the [JsonPropertyName] attributes on
    // TournamentEntry, so the stored JSON matches the API/export format.
    private static readonly JsonSerializerOptions Json = new();

    public async Task<List<TournamentEntry>> GetEntriesAsync()
    {
        var entries = await LoadAsync();
        // Newest first, matching the order the API returns.
        return entries.OrderByDescending(e => e.Date).ToList();
    }

    public async Task<TournamentEntry> AddAsync(TournamentEntry entry)
    {
        var entries = await LoadAsync();
        entry.Id = Guid.NewGuid().ToString("N");
        entry.UserId = LocalUserId;
        Normalize(entry);
        entries.Add(entry);
        await SaveAsync(entries);
        return entry;
    }

    public async Task<TournamentEntry> UpdateAsync(TournamentEntry entry)
    {
        var entries = await LoadAsync();
        var index = entries.FindIndex(e => e.Id == entry.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Entry '{entry.Id}' not found.");
        }
        Normalize(entry);
        entries[index] = entry;
        await SaveAsync(entries);
        return entry;
    }

    public async Task DeleteAsync(string id)
    {
        var entries = await LoadAsync();
        if (entries.RemoveAll(e => e.Id == id) > 0)
        {
            await SaveAsync(entries);
        }
    }

    public async Task<TournamentStats> GetStatsAsync()
    {
        var entries = await GetEntriesAsync();
        return StatsCalculator.ComputeStats(entries);
    }

    public async Task<ImportResult> ImportAsync(IReadOnlyList<TournamentEntry> imported)
    {
        var entries = await LoadAsync();
        var added = 0;
        var updated = 0;
        foreach (var entry in imported)
        {
            Normalize(entry);
            entry.UserId = LocalUserId;
            var index = string.IsNullOrEmpty(entry.Id)
                ? -1
                : entries.FindIndex(e => e.Id == entry.Id);
            if (index >= 0)
            {
                entries[index] = entry;
                updated++;
            }
            else
            {
                if (string.IsNullOrEmpty(entry.Id))
                {
                    entry.Id = Guid.NewGuid().ToString("N");
                }
                entries.Add(entry);
                added++;
            }
        }
        await SaveAsync(entries);
        return new ImportResult(added, updated);
    }

    /// <summary>
    /// Same invariants the API enforces server-side: canonical currency code,
    /// and a home-currency entry always has a rate of exactly 1.
    /// </summary>
    private static void Normalize(TournamentEntry entry)
    {
        entry.Currency = entry.Currency.Trim().ToUpperInvariant();
        if (entry.IsHomeCurrency)
        {
            entry.ExchangeRate = 1m;
        }
    }

    // A corrupt store throws instead of returning an empty list: the pages show
    // their load-error state, and no subsequent save can wipe the raw data.
    private async Task<List<TournamentEntry>> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        return string.IsNullOrEmpty(json)
            ? new List<TournamentEntry>()
            : JsonSerializer.Deserialize<List<TournamentEntry>>(json, Json) ?? new List<TournamentEntry>();
    }

    private ValueTask SaveAsync(List<TournamentEntry> entries) =>
        js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(entries, Json));
}
