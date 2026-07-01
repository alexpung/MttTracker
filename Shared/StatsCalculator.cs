namespace MttTracker.Shared;

/// <summary>Aggregated profit/loss statistics for a user.</summary>
public record TournamentStats(
    int TournamentCount,
    int TotalEntries,
    decimal TotalBuyin,
    decimal TotalCashes,
    decimal NetProfit,
    double Roi,
    decimal AverageBuyin,
    decimal AverageProfit,
    int CashCount,
    int BestCashStreakDays,
    int WorstNoCashStreakDays,
    IReadOnlyList<YearStat> ByYear,
    IReadOnlyList<PnlPoint> Pnl)
{
    public static readonly TournamentStats Empty = new(
        0, 0, 0m, 0m, 0m, 0d, 0m, 0m, 0, 0, 0,
        Array.Empty<YearStat>(), Array.Empty<PnlPoint>());
}

/// <summary>Profit summary for a single calendar year.</summary>
public record YearStat(int Year, int Entries, decimal Buyin, decimal Cashes, decimal Profit, double Roi);

/// <summary>
/// A point on the cumulative profit/loss curve. <see cref="Location"/> and
/// <see cref="Profit"/> describe the single tournament that produced this
/// point, in home-currency (GBP).
/// </summary>
public record PnlPoint(DateOnly Date, decimal Cumulative, string Location, decimal Profit);

/// <summary>
/// Pure profit/loss aggregation over a list of tournament entries. Side-effect
/// free so it can run on the client (WASM) over data fetched from the API.
/// </summary>
public static class StatsCalculator
{
    public static TournamentStats ComputeStats(IReadOnlyList<TournamentEntry> entries)
    {
        if (entries.Count == 0)
        {
            return TournamentStats.Empty;
        }

        // All money is aggregated in the home currency (GBP) via the entries'
        // frozen per-entry exchange rates, so mixed-currency play sums correctly.
        var totalEntries = entries.Sum(e => e.Entries);
        var totalBuyin = entries.Sum(e => e.TotalBuyinGbp);
        var totalCashes = entries.Sum(e => e.CashGbp);
        var netProfit = totalCashes - totalBuyin;
        var roi = totalBuyin > 0 ? (double)(netProfit / totalBuyin) : 0d;
        var averageBuyin = totalEntries > 0 ? totalBuyin / totalEntries : 0m;
        var averageProfit = netProfit / entries.Count;
        var cashCount = entries.Count(e => e.Cash > 0);

        // Streaks are computed per calendar day rather than per entry, so a
        // multi-entry day (re-buys, multiple events) counts once: a "cash day"
        // if any entry that day cashed, a "non-cash day" otherwise.
        var dailyCashed = entries
            .GroupBy(e => e.Date)
            .OrderBy(g => g.Key)
            .Select(g => g.Any(e => e.Cash > 0))
            .ToList();
        var bestCashStreakDays = LongestStreak(dailyCashed, cashed: true);
        var worstNoCashStreakDays = LongestStreak(dailyCashed, cashed: false);

        var byYear = entries
            .GroupBy(e => e.Date.Year)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var b = g.Sum(e => e.TotalBuyinGbp);
                var c = g.Sum(e => e.CashGbp);
                return new YearStat(
                    g.Key,
                    g.Sum(e => e.Entries),
                    b,
                    c,
                    c - b,
                    b > 0 ? (double)((c - b) / b) : 0d);
            })
            .ToList();

        // Cumulative PnL ordered chronologically.
        decimal running = 0m;
        var pnl = entries
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .Select(e =>
            {
                running += e.ProfitGbp;
                return new PnlPoint(e.Date, running, e.Location, e.ProfitGbp);
            })
            .ToList();

        return new TournamentStats(
            entries.Count, totalEntries, totalBuyin, totalCashes, netProfit,
            roi, averageBuyin, averageProfit, cashCount,
            bestCashStreakDays, worstNoCashStreakDays, byYear, pnl);
    }

    /// <summary>Longest run of consecutive entries in <paramref name="days"/> equal to <paramref name="cashed"/>.</summary>
    private static int LongestStreak(IReadOnlyList<bool> days, bool cashed)
    {
        var best = 0;
        var current = 0;
        foreach (var dayCashed in days)
        {
            current = dayCashed == cashed ? current + 1 : 0;
            best = Math.Max(best, current);
        }
        return best;
    }
}
