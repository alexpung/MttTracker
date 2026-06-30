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

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<SwaAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<SwaAuthenticationStateProvider>());

builder.Services.AddScoped<TournamentService>();

// Historical FX lookups go directly to Frankfurter (a different origin than the
// app), so this service gets its own HttpClient rather than the app-origin one.
builder.Services.AddScoped(_ => new ExchangeRateService(new HttpClient
{
    BaseAddress = new Uri("https://api.frankfurter.dev/v1/"),
}));

await builder.Build().RunAsync();
