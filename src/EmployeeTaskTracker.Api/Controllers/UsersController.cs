using EmployeeTaskTracker.Api.Data;
using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeTaskTracker.Api.Controllers;

/// <summary>Employee lookups, used to populate the "Assign to" dropdown.</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserRepository _users;

    public UsersController(IUserRepository users) => _users = users;

    /// <summary>Active employees who can have work assigned to them.</summary>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetEmployees(CancellationToken cancellationToken)
        => Ok(await _users.GetEmployeesAsync(cancellationToken));
}
