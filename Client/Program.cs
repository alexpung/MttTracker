using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

using MttTracker.Client;
using MttTracker.Client.Auth;
using MttTracker.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// All requests (API + /.auth/me) go to the app's own origin, which Static Web
// Apps serves and routes.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

// Storage backend, from wwwroot/appsettings.json: "api" (Azure Functions +
// Cosmos DB, the private deployment — default) or "browser" (localStorage, no
// backend and no login — the public GitHub Pages build).
var useBrowserStorage = string.Equals(
    builder.Configuration["Storage"], "browser", StringComparison.OrdinalIgnoreCase);
builder.Services.AddSingleton(new StorageOptions(useBrowserStorage));

builder.Services.AddAuthorizationCore();
if (useBrowserStorage)
{
    builder.Services.AddScoped<AuthenticationStateProvider, LocalAuthenticationStateProvider>();
    builder.Services.AddScoped<ITournamentService, BrowserStorageTournamentService>();
}
else
{
    builder.Services.AddScoped<SwaAuthenticationStateProvider>();
    builder.Services.AddScoped<AuthenticationStateProvider>(
        sp => sp.GetRequiredService<SwaAuthenticationStateProvider>());
    builder.Services.AddScoped<ITournamentService, ApiTournamentService>();
}

// Historical FX lookups go directly to Frankfurter (a different origin than the
// app), so this service gets its own HttpClient rather than the app-origin one.
builder.Services.AddScoped(_ => new ExchangeRateService(new HttpClient
{
    BaseAddress = new Uri("https://api.frankfurter.dev/v1/"),
}));

await builder.Build().RunAsync();
