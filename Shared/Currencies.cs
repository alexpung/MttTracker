namespace MttTracker.Shared;

/// <summary>A supported currency: ISO 4217 code, display symbol, and name.</summary>
public sealed record CurrencyInfo(string Code, string Symbol, string Name);

/// <summary>
/// The currencies the app can record and convert. The set is limited to those
/// the Frankfurter exchange-rate API (ECB data) supports, so every currency
/// here can be converted to <see cref="Home"/> for any tournament date.
/// </summary>
public static class Currencies
{
    /// <summary>Home / reporting currency. All statistics are expressed in this.</summary>
    public const string Home = "GBP";

    public static readonly IReadOnlyList<CurrencyInfo> All = new[]
    {
        new CurrencyInfo("GBP", "£", "British Pound"),
        new CurrencyInfo("USD", "$", "US Dollar"),
        new CurrencyInfo("EUR", "€", "Euro"),
        new CurrencyInfo("AUD", "A$", "Australian Dollar"),
        new CurrencyInfo("CAD", "C$", "Canadian Dollar"),
        new CurrencyInfo("CHF", "CHF ", "Swiss Franc"),
        new CurrencyInfo("CNY", "CN¥", "Chinese Yuan"),
        new CurrencyInfo("CZK", "Kč ", "Czech Koruna"),
        new CurrencyInfo("DKK", "kr ", "Danish Krone"),
        new CurrencyInfo("HKD", "HK$", "Hong Kong Dollar"),
        new CurrencyInfo("JPY", "¥", "Japanese Yen"),
        new CurrencyInfo("MXN", "Mex$", "Mexican Peso"),
        new CurrencyInfo("NOK", "kr ", "Norwegian Krone"),
        new CurrencyInfo("NZD", "NZ$", "New Zealand Dollar"),
        new CurrencyInfo("PLN", "zł ", "Polish Złoty"),
        new CurrencyInfo("SEK", "kr ", "Swedish Krona"),
        new CurrencyInfo("SGD", "S$", "Singapore Dollar"),
        new CurrencyInfo("ZAR", "R ", "South African Rand"),
    };

    private static readonly Dictionary<string, CurrencyInfo> ByCode =
        All.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public static CurrencyInfo? Find(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var c) ? c : null;

    /// <summary>Display symbol for a code, falling back to the code itself.</summary>
    public static string Symbol(string? code) =>
        Find(code)?.Symbol ?? (string.IsNullOrEmpty(code) ? "" : code + " ");

    public static bool IsSupported(string? code) => Find(code) is not null;

    /// <summary>True when the code is (or defaults to) the home currency.</summary>
    public static bool IsHome(string? code) =>
        string.IsNullOrEmpty(code) || string.Equals(code, Home, StringComparison.OrdinalIgnoreCase);
}
