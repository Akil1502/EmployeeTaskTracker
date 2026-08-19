using EmployeeTaskTracker.Api.Data;
using EmployeeTaskTracker.Api.Security;
using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeTaskTracker.Api.Controllers;

/// <summary>
/// Login and identity endpoints. Anonymous access is opened on the login action
/// alone - putting [AllowAnonymous] on the controller would silently override
/// the [Authorize] on /me and leave it unprotected.
/// </summary>
public sealed class AuthController : ApiControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenService tokenService,
        ILogger<AuthController> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>Exchanges email and password for a signed JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim(), cancellationToken);

        // The same generic message is returned whether the email is unknown or
        // the password is wrong, so the endpoint cannot be used to enumerate
        // which email addresses exist.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}.", request.Email);
            return Unauthorized(new ApiError { Message = "Invalid email or password." });
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for deactivated account {Email}.", request.Email);
            return Unauthorized(new ApiError { Message = "This account has been deactivated." });
        }

        var (token, expiresAtUtc) = _tokenService.CreateToken(user);

        _logger.LogInformation("User {UserId} ({Role}) signed in.", user.UserId, user.Role);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role
        });
    }

    /// <summary>Returns the profile behind the supplied token.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(CurrentUserId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }
}
