using HRMS.Api.Auth;
using HRMS.Domain;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>Who may approve an employee's requests: HR and admins for anyone, a manager for direct reports. Nobody for themselves.</summary>
public class ApprovalPolicy(HrmsDbContext db, ICurrentUser me)
{
    public async Task<bool> CanApproveForAsync(Guid employeeId)
    {
        if (employeeId == me.EmployeeId)
            return false;
        if (me.IsHr())
            return true;
        if (me.Role == UserRole.Manager && me.EmployeeId is { } managerId)
            return await db.Employees.AnyAsync(e => e.Id == employeeId && e.ManagerId == managerId);
        return false;
    }
}
