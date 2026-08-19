using System.Security.Claims;
using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeTaskTracker.Api.Controllers;

/// <summary>
/// Shared base for the API controllers. Exposes the caller's identity as read
/// from the validated JWT, so no endpoint has to trust an id sent in the body.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The authenticated user's id, taken from the token.</summary>
    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("The access token does not contain a valid user id.");

    protected string CurrentUserRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    protected bool IsAdmin =>
        string.Equals(CurrentUserRole, Roles.Admin, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The assignee filter to apply for the caller: NULL for an Admin, who sees
    /// everything, and the caller's own id for an Employee, who sees only their
    /// own tasks. Enforcing visibility here means the rule lives in one place.
    /// </summary>
    protected int? AssigneeScope => IsAdmin ? null : CurrentUserId;
}
