using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using MttTracker.Shared;

namespace MttTracker.Client.Services;

/// <summary>
/// Looks up the home-currency (GBP) value of one unit of a foreign currency on a
/// given date, using the free, key-less Frankfurter API (ECB reference rates).
/// Results are cached in memory: historical rates never change, so a date+currency
/// pair is fetched at most once per session.
/// </summary>
/// <remarks>
/// Frankfurter only publishes rates on business days. Requesting a weekend or
/// holiday returns the most recent prior business day's rate, which is the
/// conventional fill for FX. Uses a dedicated <see cref="HttpClient"/> pointed at
/// the Frankfurter origin (not the app's own API).
/// </remarks>
public sealed class ExchangeRateService(HttpClient http)
{
    private readonly Dictionary<string, decimal> _cache = new();

    /// <summary>
    /// GBP value of one unit of <paramref name="currency"/> on <paramref name="date"/>.
    /// Returns 1 for the home currency, or null if the rate can't be fetched.
    /// </summary>
    public async Task<decimal?> GetHomeRateAsync(string currency, DateOnly date)
    {
        if (Currencies.IsHome(currency))
        {
            return 1m;
        }

        var code = currency.ToUpperInvariant();
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var key = $"{day}:{code}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var response = await http.GetFromJsonAsync<FrankfurterResponse>(
                $"{day}?from={code}&to={Currencies.Home}");
            if (response?.Rates is not null &&
                response.Rates.TryGetValue(Currencies.Home, out var rate) && rate > 0)
            {
                _cache[key] = rate;
                return rate;
            }
        }
        catch
        {
            // Network/parse failure — caller treats null as "rate unavailable".
        }

        return null;
    }

    private sealed class FrankfurterResponse
    {
        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
