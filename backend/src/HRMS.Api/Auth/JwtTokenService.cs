using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HRMS.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace HRMS.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "hrms";
    public string Audience { get; set; } = "hrms";
    /// <summary>HMAC signing key, at least 32 characters. Keep it out of source control in production.</summary>
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;
}

public class JwtTokenService(JwtOptions options)
{
    /// <param name="actingTenantId">For SuperAdmin only: the company they have opened. Its data becomes visible to them.</param>
    public (string Token, DateTime ExpiresAtUtc) Create(User user, Guid? actingTenantId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (user.TenantId != Guid.Empty)
            claims.Add(new(HrmsClaims.TenantId, user.TenantId.ToString()));
        else if (actingTenantId is { } acting)
            claims.Add(new(HrmsClaims.TenantId, acting.ToString()));
        if (user.EmployeeId is { } employeeId)
            claims.Add(new(HrmsClaims.EmployeeId, employeeId.ToString()));

        var expires = DateTime.UtcNow.AddMinutes(options.ExpiryMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, expires: expires, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
