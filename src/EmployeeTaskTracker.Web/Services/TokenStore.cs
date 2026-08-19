using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace EmployeeTaskTracker.Web.Services;

/// <summary>
/// Holds the signed-in user's JWT for the lifetime of the Blazor circuit and
/// mirrors it into the browser's local storage so a page refresh does not sign
/// the user out.
///
/// ProtectedLocalStorage encrypts the value with the server's data-protection
/// key, so the token is not readable by client-side script from another origin
/// and cannot be tampered with.
/// </summary>
public sealed class TokenStore
{
    private const string StorageKey = "employee-task-tracker.session";

    private readonly ProtectedLocalStorage _localStorage;
    private LoginResponse? _session;
    private bool _loaded;

    public TokenStore(ProtectedLocalStorage localStorage) => _localStorage = localStorage;

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
            var result = await _localStorage.GetAsync<LoginResponse>(StorageKey);
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
        await _localStorage.SetAsync(StorageKey, session);
        SessionChanged?.Invoke();
    }

    public async Task ClearAsync()
    {
        _session = null;
        _loaded = true;
        await _localStorage.DeleteAsync(StorageKey);
        SessionChanged?.Invoke();
    }

    /// <summary>The bearer token, or null when signed out or expired.</summary>
    public async Task<string?> GetTokenAsync() => (await GetSessionAsync())?.Token;
}
