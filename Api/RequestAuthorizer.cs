using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MttTracker.Api;

/// <summary>
/// Enforces that a request comes from an authenticated, allow-listed user.
/// This is the real security boundary: Static Web Apps gates who can reach the
/// site, but the API independently verifies the principal so that even another
/// authenticated GitHub user cannot read or write data.
/// </summary>
public sealed class RequestAuthorizer(IConfiguration config)
{
    private readonly string? _allowedUserDetails = config["AllowedUserDetails"];
    private readonly string? _allowedUserId = config["AllowedUserId"];

    public enum Result { Ok, Unauthenticated, Forbidden }

    /// <summary>
    /// Validates the caller. On success returns the user id to scope data by
    /// (the Cosmos partition key).
    /// </summary>
    public Result Authorize(HttpRequest request, out string userId)
    {
        userId = "";
        var principal = ClientPrincipal.FromRequest(request);
        if (principal is null || !principal.IsAuthenticated || string.IsNullOrEmpty(principal.UserId))
        {
            return Result.Unauthenticated;
        }

        if (!IsAllowed(principal))
        {
            return Result.Forbidden;
        }

        userId = principal.UserId;
        return Result.Ok;
    }

    private bool IsAllowed(ClientPrincipal principal)
    {
        var byId = !string.IsNullOrWhiteSpace(_allowedUserId);
        var byDetails = !string.IsNullOrWhiteSpace(_allowedUserDetails);

        // If no allowlist is configured, fail closed rather than open.
        if (!byId && !byDetails)
        {
            return false;
        }

        var idMatch = byId &&
            string.Equals(principal.UserId, _allowedUserId, StringComparison.OrdinalIgnoreCase);
        var detailsMatch = byDetails &&
            string.Equals(principal.UserDetails, _allowedUserDetails, StringComparison.OrdinalIgnoreCase);

        return idMatch || detailsMatch;
    }
}
