using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Http;

namespace MttTracker.Api;

/// <summary>
/// The authenticated user injected by Azure Static Web Apps via the
/// <c>x-ms-client-principal</c> request header (base64-encoded JSON). See
/// https://learn.microsoft.com/azure/static-web-apps/user-information
/// </summary>
public sealed class ClientPrincipal
{
    [JsonPropertyName("identityProvider")]
    public string? IdentityProvider { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("userDetails")]
    public string? UserDetails { get; set; }

    [JsonPropertyName("userRoles")]
    public string[] UserRoles { get; set; } = Array.Empty<string>();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Parse the SWA principal header, or null if absent/invalid.</summary>
    public static ClientPrincipal? FromRequest(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("x-ms-client-principal", out var header))
        {
            return null;
        }

        var encoded = header.ToString();
        if (string.IsNullOrEmpty(encoded))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return JsonSerializer.Deserialize<ClientPrincipal>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public bool IsAuthenticated =>
        UserRoles.Contains("authenticated", StringComparer.OrdinalIgnoreCase);
}
