using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace EmployeeTaskTracker.Web.Services;

/// <summary>
/// Holds the signed-in user's JWT for the lifetime of the Blazor circuit and
/// mirrors it into the browser's session storage so a page refresh does not
/// sign the user out.
///
/// Session storage rather than local storage is deliberate. Local storage
/// survives closing the browser, so the next visit would skip the login page
/// entirely and land on the dashboard. Session storage is scoped to the browser
/// tab: refreshing keeps you signed in, while a new tab or a restarted browser
/// starts at the login page. That also means an abandoned session on a shared
/// machine does not stay signed in indefinitely.
///
/// ProtectedSessionStorage encrypts the value with the server's data-protection
/// key, so the token cannot be read or tampered with by client-side script.
/// </summary>
public sealed class TokenStore
{
    private const string StorageKey = "employee-task-tracker.session";

    private readonly ProtectedSessionStorage _sessionStorage;
    private LoginResponse? _session;
    private bool _loaded;

    public TokenStore(ProtectedSessionStorage sessionStorage) => _sessionStorage = sessionStorage;

    /// <summary>Raised when the user signs in or out, so the UI can re-render.</summary>
    public event Action? SessionChanged;

    /// <summary>
    /// The current session, read from local storage the first time it is asked
    /// for. Returns null when nobody is signed in.
    /// </summary>
    public async Task<LoginResponse?> GetSessionAsync()
    {
        if (_loaded)
            return _session;

        try
        {
            var result = await _sessionStorage.GetAsync<LoginResponse>(StorageKey);
            _session = result.Success ? result.Value : null;
        }
        catch (Exception)
        {
            // Thrown when JS interop is not available yet (during prerender) or
            // when the stored payload cannot be decrypted, for example after the
            // data-protection keys were rotated. Either way, treat it as
            // "no session" rather than failing the render.
            _session = null;
        }

        // An expired token is no better than no token at all.
        if (_session is not null && _session.ExpiresAtUtc <= DateTime.UtcNow)
            _session = null;

        _loaded = true;
        return _session;
    }

    public async Task SetSessionAsync(LoginResponse session)
    {
        _session = session;
        _loaded = true;
        await _sessionStorage.SetAsync(StorageKey, session);
        SessionChanged?.Invoke();
    }

    public async Task ClearAsync()
    {
        _session = null;
        _loaded = true;
        await _sessionStorage.DeleteAsync(StorageKey);
        SessionChanged?.Invoke();
    }

    /// <summary>The bearer token, or null when signed out or expired.</summary>
    public async Task<string?> GetTokenAsync() => (await GetSessionAsync())?.Token;
}
