namespace MttTracker.Client.Services;

/// <summary>
/// Which storage backend this deployment uses, resolved once at startup from
/// <c>wwwroot/appsettings.json</c> (<c>"Storage": "api"</c> or <c>"browser"</c>).
/// </summary>
public sealed record StorageOptions(bool UseBrowserStorage);
