using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

namespace MttTracker.Client.Auth;

/// <summary>
/// Used in browser-storage mode, where there is no server and nothing to log
/// in to: every visitor is a permanently-authenticated "local" user whose data
/// lives in their own browser, so the pages' <c>[Authorize]</c> attributes
/// always pass.
/// </summary>
public sealed class LocalAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> Authenticated = Task.FromResult(
        new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "local"),
                new Claim(ClaimTypes.Name, "Local user"),
                new Claim(ClaimTypes.Role, "authenticated"),
            },
            authenticationType: "local"))));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Authenticated;
}
