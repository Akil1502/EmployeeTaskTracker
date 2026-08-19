using EmployeeTaskTracker.Web.Components;
using EmployeeTaskTracker.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Authentication state
//
// The session is kept per Blazor circuit, so both the token store and the
// authentication state provider are scoped.
// ---------------------------------------------------------------------------
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// ---------------------------------------------------------------------------
// API client
// ---------------------------------------------------------------------------
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException(
        "ApiBaseUrl is not configured. Set it in appsettings.json to the address the Web API is listening on.");

builder.Services.AddHttpClient<TaskApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");

    // The specification requires API responses inside two seconds; failing fast
    // keeps a stalled backend from hanging the UI indefinitely.
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

// A URL matching no endpoint 404s in ASP.NET routing before the Blazor Router
// ever sees it, so the request is re-executed into the /not-found page to give
// the visitor something better than an empty response.
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
