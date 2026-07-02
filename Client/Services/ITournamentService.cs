using MttTracker.Shared;

namespace MttTracker.Client.Services;

/// <summary>
/// Storage backend for tournament entries. Two implementations exist:
/// <see cref="ApiTournamentService"/> (Azure Functions + Cosmos DB — the
/// private, GitHub-locked deployment) and
/// <see cref="BrowserStorageTournamentService"/> (browser localStorage — the
/// public/static deployment, e.g. GitHub Pages). Pages depend only on this
/// interface; <c>Program.cs</c> picks the implementation from configuration.
/// </summary>
public interface ITournamentService
{
    Task<List<TournamentEntry>> GetEntriesAsync();

    /// <summary>Creates the entry and returns the stored copy (with its assigned id).</summary>
    Task<TournamentEntry> AddAsync(TournamentEntry entry);

    /// <summary>Updates the entry and returns the stored copy.</summary>
    Task<TournamentEntry> UpdateAsync(TournamentEntry entry);

    Task DeleteAsync(string id);

    Task<TournamentStats> GetStatsAsync();

    /// <summary>
    /// Merges entries from a JSON export into the store: an entry whose id is
    /// already present replaces the stored one, anything else is added.
    /// </summary>
    Task<ImportResult> ImportAsync(IReadOnlyList<TournamentEntry> entries);
}

/// <summary>Outcome of <see cref="ITournamentService.ImportAsync"/>.</summary>
public readonly record struct ImportResult(int Added, int Updated);
