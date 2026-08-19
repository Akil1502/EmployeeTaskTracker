using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskTracker.Shared;

/// <summary>Credentials posted to POST /api/auth/login.</summary>
public sealed class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// A successful login. The token is a JWT carrying the user's id, name, email
/// and role; the same values are echoed here so the UI does not have to decode
/// the token to render a greeting or decide what to show.
/// </summary>
public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public bool IsAdmin => string.Equals(Role, Roles.Admin, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A user as exposed by the API. Never carries the password hash.</summary>
public sealed class UserDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
