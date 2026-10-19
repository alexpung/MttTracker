using MttTracker.Shared;

namespace MttTracker.Client.Services;

/// <summary>
/// What's been typed into the ICM calculator. Registered as a scoped service
/// so the numbers survive switching tabs - the page component itself is
/// recreated on every navigation. A blank (null) stack means an empty seat; a
/// blank prize pays nothing.
/// </summary>
public sealed class IcmInputs
{
    public string?[] Names { get; } = new string?[IcmCalculator.MaxPlayers];

    public decimal?[] Stacks { get; } = new decimal?[IcmCalculator.MaxPlayers];

    public decimal?[] Prizes { get; } = new decimal?[IcmCalculator.MaxPlayers];

    public decimal?[] AlternatePrizes { get; } = new decimal?[IcmCalculator.MaxPlayers];

    public bool CompareAlternate { get; set; }

    /// <summary>Partial chop: the percentage of every prize left to play for.</summary>
    public decimal? PlayForPercent { get; set; }

    public decimal? SetAside { get; set; }

    public string Currency { get; set; } = Currencies.Home;

    public void Clear()
    {
        Array.Clear(Names);
        Array.Clear(Stacks);
        Array.Clear(Prizes);
        Array.Clear(AlternatePrizes);
        CompareAlternate = false;
        PlayForPercent = null;
        SetAside = null;
    }
}
