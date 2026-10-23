using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;


public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record ChangePasswordRequest([Required] string CurrentPassword, [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public record SessionDto(string Token, DateTime ExpiresAtUtc, MeDto User);

/// <summary>For a SuperAdmin who has opened a company, TenantId/CompanyName describe that company and IsActingAsOwner is true.</summary>
public record MeDto(Guid Id, string Email, string FullName, UserRole Role, Guid? TenantId, string? CompanyName, Guid? EmployeeId, bool IsActingAsOwner, DateOnly? SubscriptionEndsOn, bool MustChangePassword,
    List<string>? Modules = null);

[ApiController]
[Route("api/auth")]
public class AuthController(
    HrmsDbContext db,
    IPasswordHasher<User> hasher,
    SessionService sessions,
    TenantAccessChecker access,
    AuditWriter audit,
    ICurrentUser me) : ControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<ActionResult<SessionDto>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
        {
            await audit.AuthAsync("LoginFailed", user, email);
            return Problem(statusCode: 401, title: "Email or password is incorrect.");
        }
        if (!user.IsActive)
        {
            await audit.AuthAsync("LoginBlocked:UserInactive", user);
            return Problem(statusCode: 403, title: "Your account is disabled. Contact your administrator.");
        }
        if (user.Role != UserRole.SuperAdmin)
        {
            var tenantAccess = await access.TenantAsync(user.TenantId);
            if (tenantAccess != TenantAccess.Allowed)
            {
                var (code, message) = TenantAccessChecker.Describe(tenantAccess);
                await audit.AuthAsync($"LoginBlocked:{code}", user);
                var problem = Problem(statusCode: 403, title: message);
                ((ProblemDetails)((ObjectResult)problem).Value!).Extensions["code"] = code;
                return problem;
            }
        }

        await audit.AuthAsync("LoginSucceeded", user);
        return await sessions.CreateAsync(user);
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == me.UserId);
        await audit.AuthAsync("Logout", user);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public async Task<MeDto> Me()
    {
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == me.UserId);
        return await sessions.ToMeAsync(user, me.TenantId);
    }

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == me.UserId);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new DomainException("Current password is incorrect.");
        if (req.NewPassword == req.CurrentPassword)
            throw new DomainException("Choose a password different from the current one.");
        user.PasswordHash = hasher.HashPassword(user, req.NewPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync();
        await audit.AuthAsync("PasswordChanged", user);
        return NoContent();
    }
}
