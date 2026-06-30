using System.Globalization;

namespace MttTracker.Shared;

/// <summary>Display formatting helpers shared across pages.</summary>
public static class Format
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Home-currency (GBP) money with thousands separators, no decimals.</summary>
    public static string Money(decimal value) => Money(value, Currencies.Home);

    /// <summary>Money in a given currency with thousands separators, no decimals.</summary>
    public static string Money(decimal value, string currency) =>
        (value < 0 ? "-" : "") + Currencies.Symbol(currency) + Math.Abs(value).ToString("#,##0", Inv);

    /// <summary>Home-currency (GBP) money keeping cents.</summary>
    public static string Money2(decimal value) => Money2(value, Currencies.Home);

    /// <summary>Money in a given currency keeping cents.</summary>
    public static string Money2(decimal value, string currency) =>
        (value < 0 ? "-" : "") + Currencies.Symbol(currency) + Math.Abs(value).ToString("#,##0.00", Inv);

    /// <summary>A ratio (0.25 -> "25.0%").</summary>
    public static string Percent(double value) =>
        value.ToString("P1", Inv);

    /// <summary>
    /// An exchange rate in the home currency, with enough decimals to stay
    /// meaningful for small rates (e.g. "£0.0052" for JPY).
    /// </summary>
    public static string Rate(decimal value) =>
        Currencies.Symbol(Currencies.Home) + value.ToString("0.####", Inv);

    /// <summary>Bootstrap text color class for a signed amount.</summary>
    public static string SignClass(decimal value) =>
        value > 0 ? "text-success" : value < 0 ? "text-danger" : "";
}
