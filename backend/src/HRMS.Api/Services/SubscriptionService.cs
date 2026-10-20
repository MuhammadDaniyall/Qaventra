using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewPayment(decimal Amount, DateOnly PaidOn, int Periods, string? Method, string? Reference, string? Notes);

public class SubscriptionService(HrmsDbContext db, TenantAccessChecker access, ICurrentUser me)
{
    /// <summary>Months covered by one billing cycle.</summary>
    public static int Months(BillingCycle cycle) => (int)cycle;

    /// <summary>
    /// Records a payment and extends the subscription by whole billing cycles: from the day after the current end date,
    /// or from today if the subscription has already lapsed (or never started).
    /// </summary>
    public async Task<TenantPayment> RecordPaymentAsync(Guid tenantId, NewPayment input)
    {
        if (input.Amount <= 0)
            throw new DomainException("Amount must be greater than zero.");
        if (input.Periods is < 1 or > 36)
            throw new DomainException("Periods must be between 1 and 36.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId) ?? throw new NotFoundException("Company");
        var cycle = tenant.BillingCycle ?? throw new DomainException("Set the company's billing cycle (monthly, quarterly, yearly) before recording a payment.");

        var today = access.LocalToday(tenant.TimeZoneId);
        var from = tenant.SubscriptionEndsOn is { } end && end >= today ? end.AddDays(1) : today;
        var to = from.AddMonths(Months(cycle) * input.Periods).AddDays(-1);

        var payment = new TenantPayment
        {
            TenantId = tenantId,
            Amount = input.Amount,
            PaidOn = input.PaidOn,
            PeriodFrom = from,
            PeriodTo = to,
            Method = input.Method?.Trim(),
            Reference = input.Reference?.Trim(),
            Notes = input.Notes?.Trim(),
            RecordedByUserId = me.UserId,
        };
        db.TenantPayments.Add(payment);
        tenant.SubscriptionEndsOn = to;
        await db.SaveChangesAsync();
        access.Invalidate(tenantId);
        return payment;
    }

    /// <summary>Permanently deletes the company and every row it owns. Audit logs are kept as the permanent record.</summary>
    public async Task DeleteCompanyAsync(Guid tenantId, string confirmName)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId) ?? throw new NotFoundException("Company");
        if (!string.Equals(tenant.Name.Trim(), confirmName?.Trim(), StringComparison.Ordinal))
            throw new DomainException("Type the company name exactly to confirm deletion.");

        await using var tx = await db.Database.BeginTransactionAsync();

        IQueryable<T> Of<T>() where T : class, ITenantEntity => db.Set<T>().IgnoreQueryFilters().Where(x => x.TenantId == tenantId);

        var payslipIds = Of<Payslip>().Select(p => p.Id);
        await db.Set<PayslipLine>().Where(l => payslipIds.Contains(l.PayslipId)).ExecuteDeleteAsync();
        await Of<Payslip>().ExecuteDeleteAsync();
        await Of<PayrollRun>().ExecuteDeleteAsync();
        var salaryIds = Of<EmployeeSalary>().Select(s => s.Id);
        await db.Set<SalaryComponent>().Where(c => salaryIds.Contains(c.EmployeeSalaryId)).ExecuteDeleteAsync();
        await Of<EmployeeSalary>().ExecuteDeleteAsync();
        await Of<LeaveRequest>().ExecuteDeleteAsync();
        await Of<LeaveBalance>().ExecuteDeleteAsync();
        await Of<AttendanceRecord>().ExecuteDeleteAsync();
        await Of<User>().ExecuteDeleteAsync();
        await Of<Employee>().ExecuteUpdateAsync(s => s.SetProperty(e => e.ManagerId, (Guid?)null));
        await Of<Employee>().ExecuteDeleteAsync();
        await Of<Holiday>().ExecuteDeleteAsync();
        await Of<LeaveType>().ExecuteDeleteAsync();
        await Of<Shift>().ExecuteDeleteAsync();
        await Of<OfficeLocation>().ExecuteDeleteAsync();
        await Of<Designation>().ExecuteDeleteAsync();
        await Of<Department>().ExecuteDeleteAsync();
        await Of<PayrollSettings>().ExecuteDeleteAsync();
        await db.TenantPayments.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();

        await tx.CommitAsync();
        access.Invalidate(tenantId);
    }
}
