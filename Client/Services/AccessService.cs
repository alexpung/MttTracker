using System.Net;

namespace MttTracker.Client.Services;

/// <summary>
/// Confirms the signed-in user is allowed to use the app, for pages that never
/// call the entries API (which is where the allowlist is normally enforced).
/// In API mode it asks <c>/api/access</c>, the same check the tracker's data
/// goes through; a 403 surfaces as <see cref="NotAuthorizedException"/>. In
/// browser-storage mode there is no server or allowlist, so every visitor passes.
/// </summary>
public sealed class AccessService(HttpClient http, StorageOptions storage)
{
    // Remembered for the session so revisiting the page doesn't re-check.
    private bool allowed;

    public async Task EnsureAllowedAsync()
    {
        if (storage.UseBrowserStorage || allowed)
        {
            return;
        }

        var response = await http.GetAsync("api/access");
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new NotAuthorizedException();
        }
        response.EnsureSuccessStatusCode();
        allowed = true;
    }
}
