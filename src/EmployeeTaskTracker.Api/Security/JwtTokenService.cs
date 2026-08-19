using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EmployeeTaskTracker.Api.Data;
using Microsoft.IdentityModel.Tokens;

namespace EmployeeTaskTracker.Api.Security;

/// <summary>Binds to the "Jwt" section of appsettings.json.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 120;
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(UserRecord user);
}

/// <summary>
/// Issues the signed JWTs the API accepts as credentials. The role is written
/// as a <see cref="ClaimTypes.Role"/> claim, which is what makes
/// [Authorize(Roles = ...)] work on the controllers.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 characters. " +
                "See the Configuration section of the README.");
        }
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(UserRecord user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
