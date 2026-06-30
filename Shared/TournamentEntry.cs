using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MttTracker.Shared;

/// <summary>
/// A single poker tournament played by a user. A re-entry means the player
/// busted and bought back in to the same event, so each re-entry costs another
/// buy-in. Total cost = Buyin * (1 + ReEntries).
/// </summary>
/// <remarks>
/// Stored as a document in Cosmos DB. <see cref="Id"/> is the document id and
/// <see cref="UserId"/> is the partition key (<c>/userId</c>).
/// <para>
/// <see cref="Buyin"/> and <see cref="Cash"/> are recorded in the entry's own
/// <see cref="Currency"/>. <see cref="ExchangeRate"/> captures the home-currency
/// value of that currency on the tournament date, so the <c>*Gbp</c> computed
/// values (used for all statistics) stay fixed even as rates move later.
/// </para>
/// </remarks>
public class TournamentEntry
{
    /// <summary>Cosmos document id. Empty for a not-yet-saved record.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// Owner of this record (partition key). Set server-side from the
    /// authenticated principal, never by the form, so it is not validated.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [Required]
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }

    [Required]
    [MaxLength(200)]
    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    /// <summary>
    /// ISO 4217 code of the currency <see cref="Buyin"/> and <see cref="Cash"/>
    /// are recorded in (e.g. "USD"). Defaults to the home currency.
    /// </summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = Currencies.Home;

    /// <summary>
    /// Home-currency (GBP) value of one unit of <see cref="Currency"/> on the
    /// tournament date. 1 for home-currency entries. Fetched when the entry is
    /// recorded and then frozen, so historical statistics never change.
    /// </summary>
    [Range(0.0000001, 1_000_000)]
    [JsonPropertyName("exchangeRate")]
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>Cost of a single entry/buy-in, in <see cref="Currency"/>.</summary>
    [Range(0, 1_000_000)]
    [JsonPropertyName("buyin")]
    public decimal Buyin { get; set; }

    /// <summary>Number of re-entries (extra buy-ins) beyond the initial entry.</summary>
    [Range(0, 1000)]
    [JsonPropertyName("reEntries")]
    public int ReEntries { get; set; }

    /// <summary>Amount cashed / won, in <see cref="Currency"/>.</summary>
    [Range(0, 100_000_000)]
    [JsonPropertyName("cash")]
    public decimal Cash { get; set; }

    /// <summary>Finishing place (1 = winner). Null if not recorded.</summary>
    [Range(1, 1_000_000)]
    [JsonPropertyName("place")]
    public int? Place { get; set; }

    /// <summary>True when this entry is already in the home currency.</summary>
    [JsonIgnore]
    public bool IsHomeCurrency => Currencies.IsHome(Currency);

    /// <summary>Number of entries this record represents (initial + re-entries).</summary>
    [JsonIgnore]
    public int Entries => 1 + ReEntries;

    /// <summary>Total amount paid in buy-ins, in <see cref="Currency"/>.</summary>
    [JsonIgnore]
    public decimal TotalBuyin => Buyin * Entries;

    /// <summary>Net profit for this record, in <see cref="Currency"/>.</summary>
    [JsonIgnore]
    public decimal Profit => Cash - TotalBuyin;

    /// <summary>Total buy-ins converted to the home currency (GBP).</summary>
    [JsonIgnore]
    public decimal TotalBuyinGbp => TotalBuyin * ExchangeRate;

    /// <summary>Amount cashed converted to the home currency (GBP).</summary>
    [JsonIgnore]
    public decimal CashGbp => Cash * ExchangeRate;

    /// <summary>Net profit converted to the home currency (GBP).</summary>
    [JsonIgnore]
    public decimal ProfitGbp => CashGbp - TotalBuyinGbp;
}
