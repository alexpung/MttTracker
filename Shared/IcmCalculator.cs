using System.Numerics;

namespace MttTracker.Shared;

/// <summary>
/// Independent Chip Model (Malmuth-Harville): a player's chance of finishing
/// next is their share of the chips still in play. Pure and side-effect free,
/// like <see cref="StatsCalculator"/>.
/// </summary>
public static class IcmCalculator
{
    /// <summary>Most players (and paid places) the calculator accepts.</summary>
    public const int MaxPlayers = 9;

    /// <summary>
    /// Returns <c>p[i, k]</c> = the chance player <c>i</c> finishes in place
    /// <c>k + 1</c>, for the first <paramref name="places"/> places (capped at
    /// the player count). Every stack must be positive.
    /// </summary>
    public static double[,] FinishProbabilities(IReadOnlyList<decimal> stacks, int places)
    {
        var n = stacks.Count;
        if (n > MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(stacks), $"At most {MaxPlayers} players are supported.");
        }
        if (stacks.Any(s => s <= 0))
        {
            throw new ArgumentException("Every stack must be positive.", nameof(stacks));
        }

        places = Math.Clamp(places, 0, n);
        var chips = stacks.Select(s => (double)s).ToArray();
        var result = new double[n, places];

        // A mask is the set of players who have taken places 1..popcount(mask),
        // in some order. reach[mask] is the chance of that happening; chipsIn[mask]
        // is their combined stack. Adding a player only ever sets a higher bit
        // pattern, so walking masks in increasing order sees every mask after
        // all the masks that lead into it.
        var reach = new double[1 << n];
        var chipsIn = new double[1 << n];
        var total = chips.Sum();
        reach[0] = 1d;

        for (var mask = 0; mask < reach.Length; mask++)
        {
            if (mask != 0)
            {
                var lowest = BitOperations.TrailingZeroCount(mask);
                chipsIn[mask] = chipsIn[mask & (mask - 1)] + chips[lowest];
            }

            var place = BitOperations.PopCount((uint)mask);
            if (reach[mask] == 0d || place >= places)
            {
                continue;
            }

            var remaining = total - chipsIn[mask];
            for (var i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    continue;
                }

                var p = reach[mask] * chips[i] / remaining;
                result[i, place] += p;
                reach[mask | (1 << i)] += p;
            }
        }

        return result;
    }

    /// <summary>
    /// Each player's ICM equity: the sum over places of their chance of
    /// finishing there times that place's prize. Prizes beyond the places in
    /// <paramref name="probabilities"/> (more prizes than players) are ignored.
    /// </summary>
    public static decimal[] Equities(double[,] probabilities, IReadOnlyList<decimal> prizes)
    {
        var n = probabilities.GetLength(0);
        var places = Math.Min(probabilities.GetLength(1), prizes.Count);
        var equities = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            var equity = 0d;
            for (var k = 0; k < places; k++)
            {
                equity += probabilities[i, k] * (double)prizes[k];
            }
            equities[i] = (decimal)equity;
        }
        return equities;
    }

    /// <summary>
    /// The prizes still played for after a deal that leaves
    /// <paramref name="playForShare"/> (0 to 1) of every prize in the pool -
    /// the same payout structure, scaled down (a partial chop) - plus
    /// <paramref name="setAside"/> for the winner on top of their share.
    /// </summary>
    public static decimal[] PlayedFor(IReadOnlyList<decimal> prizes, decimal playForShare, decimal setAside)
    {
        var playedFor = prizes.Select(p => p * playForShare).ToArray();
        if (playedFor.Length > 0)
        {
            playedFor[0] += setAside;
        }
        return playedFor;
    }

    /// <summary>
    /// Fair deal payouts when <paramref name="playedFor"/> (see
    /// <see cref="PlayedFor"/>) stays in the pool and the rest is paid out
    /// now: each player's equity minus their ICM equity in what's still played
    /// for. ICM is linear in the prizes, so that's ICM run on the money being
    /// split, <c>prizes[k] - playedFor[k]</c>. For money set aside for the
    /// winner alone, a player's share of it is just their chip share.
    /// </summary>
    public static decimal[] DealPayouts(double[,] probabilities, IReadOnlyList<decimal> prizes, IReadOnlyList<decimal> playedFor)
    {
        var split = prizes.Select((p, k) => p - (k < playedFor.Count ? playedFor[k] : 0m)).ToArray();
        return Equities(probabilities, split);
    }
}
