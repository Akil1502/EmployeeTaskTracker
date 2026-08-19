using System.Security.Claims;
using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Components.Authorization;

namespace EmployeeTaskTracker.Web.Services;

/// <summary>
/// Projects the JWT held by <see cref="TokenStore"/> into a ClaimsPrincipal so
/// that &lt;AuthorizeView&gt; and [Authorize] work throughout the UI.
///
/// The claims are built from the login response rather than by decoding the
/// token, because the token is only ever a bearer credential for the API - the
/// API is what actually validates its signature. Nothing security-sensitive is
/// decided from these claims alone; the server re-checks the role on every call.
/// </summary>
public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState SignedOut =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStore _tokenStore;

    public JwtAuthenticationStateProvider(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        _tokenStore.SessionChanged += OnSessionChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = await _tokenStore.GetSessionAsync();
        if (session is null)
            return SignedOut;

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new Claim(ClaimTypes.Name, session.Name),
                new Claim(ClaimTypes.Email, session.Email),
                new Claim(ClaimTypes.Role, session.Role)
            ],
            authenticationType: "jwt");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private void OnSessionChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _tokenStore.SessionChanged -= OnSessionChanged;
}
