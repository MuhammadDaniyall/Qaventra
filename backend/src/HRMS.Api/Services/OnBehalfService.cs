using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>
/// Requests made for someone else: HR may file for anyone, a manager for their direct reports. Everything else goes
/// through the same rules and approval chain as if the person had filed it.
/// </summary>
public class OnBehalfService(HrmsDbContext db, ICurrentUser me)
{
    /// <summary>The employee a request is for: the signed-in person, or the one chosen when allowed.</summary>
    public async Task<Guid> ResolveAsync(Guid? employeeId)
    {
        if (employeeId is not { } id || id == me.EmployeeId)
            return me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record. Choose the employee.");
        if (me.IsHr())
        {
            if (!await db.Employees.AnyAsync(e => e.Id == id)) throw new NotFoundException("Employee");
            return id;
        }
        if (me.Role == UserRole.Manager && me.EmployeeId is { } mine && await db.Employees.AnyAsync(e => e.Id == id && e.ManagerId == mine))
            return id;
        throw new ForbiddenException("You can only file requests for yourself or your direct reports.");
    }
}
