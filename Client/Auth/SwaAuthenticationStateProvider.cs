using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Components.Authorization;

namespace MttTracker.Client.Auth;

/// <summary>
/// Derives the authentication state from the Azure Static Web Apps auth
/// endpoint <c>/.auth/me</c>, which returns the GitHub-authenticated principal
/// (or none). SWA itself owns the login/logout flow.
/// </summary>
public sealed class SwaAuthenticationStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var response = await http.GetFromJsonAsync<AuthMeResponse>(".auth/me");
            var principal = response?.ClientPrincipal;
            if (principal is null || string.IsNullOrEmpty(principal.UserId))
            {
                return Anonymous;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, principal.UserId),
                new(ClaimTypes.Name, principal.UserDetails ?? principal.UserId),
            };
            claims.AddRange(principal.UserRoles.Select(r => new Claim(ClaimTypes.Role, r)));

            var identity = new ClaimsIdentity(claims, authenticationType: "swa");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous;
        }
    }

    /// <summary>Re-query <c>/.auth/me</c>, e.g. after returning from a login redirect.</summary>
    public void Reload() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private sealed class AuthMeResponse
    {
        [JsonPropertyName("clientPrincipal")]
        public SwaClientPrincipal? ClientPrincipal { get; set; }
    }

    private sealed class SwaClientPrincipal
    {
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("userDetails")]
        public string? UserDetails { get; set; }

        [JsonPropertyName("userRoles")]
        public string[] UserRoles { get; set; } = Array.Empty<string>();
    }
}
