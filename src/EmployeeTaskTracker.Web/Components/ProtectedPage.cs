using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EmployeeTaskTracker.Web.Components;

/// <summary>
/// Base class for any page that requires a signed-in user.
///
/// Why not [Authorize] on the component? In a Blazor Web App a page's
/// [Authorize] attribute is also picked up as *endpoint* metadata, so ASP.NET
/// Core's authorization middleware tries to authenticate the plain HTTP GET
/// that loads the page. This application deliberately has no server-side
/// authentication scheme - the JWT lives in the browser's protected storage and
/// is only ever presented to the Web API - so that challenge fails and a direct
/// navigation to the URL returns a 500.
///
/// Gating in the component instead means the check runs once the circuit is up
/// and the session can actually be read. The real enforcement is on the API,
/// which validates the token's signature and role on every single request.
/// </summary>
public abstract class ProtectedPage : ComponentBase
{
    [CascadingParameter] protected Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    /// <summary>True once the signed-in user has been confirmed.</summary>
    protected bool IsAuthenticated { get; private set; }

    protected bool IsAdmin { get; private set; }

    protected string? UserName { get; private set; }

    protected sealed override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask is not null)
        {
            var state = await AuthenticationStateTask;
            var user = state.User;

            IsAuthenticated = user.Identity?.IsAuthenticated == true;
            IsAdmin = user.IsInRole(Roles.Admin);
            UserName = user.Identity?.Name;
        }

        if (!IsAuthenticated)
        {
            var returnUrl = Navigation.ToBaseRelativePath(Navigation.Uri);

            var target = string.IsNullOrWhiteSpace(returnUrl)
                ? "login"
                : $"login?returnUrl={Uri.EscapeDataString(returnUrl)}";

            Navigation.NavigateTo(target, replace: true);
            return;
        }

        await OnAuthenticatedAsync();
    }

    /// <summary>
    /// Runs in place of OnInitializedAsync, once the user is known to be signed
    /// in. Override this instead of OnInitializedAsync.
    /// </summary>
    protected virtual Task OnAuthenticatedAsync() => Task.CompletedTask;
}
