namespace MttTracker.Client.Services;

/// <summary>
/// Thrown when the API rejects an authenticated caller (HTTP 403) because they
/// are not on the single-user allowlist. The user signed in with GitHub but is
/// not the owner of this app.
/// </summary>
public sealed class NotAuthorizedException : Exception
{
    public NotAuthorizedException()
        : base("This account is not authorized to use this app.")
    {
    }
}
