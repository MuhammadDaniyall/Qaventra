using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewExpenseItem(DateOnly Date, ExpenseCategory Category, string Description, decimal Amount, NewReceipt? Receipt,
    Guid? ExpenseTypeId = null, decimal? Quantity = null, string? FromCity = null, string? ToCity = null);

public record NewReceipt(string FileName, string ContentType, string Base64);

public class ExpenseService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals)
{
    public const int MaxItems = 20;
    public const int MaxReceiptBytes = 3 * 1024 * 1024;
    public static readonly string[] ReceiptTypes = ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    public async Task<ExpenseClaim> SubmitAsync(Guid employeeId, string title, IReadOnlyList<NewExpenseItem> items, Guid? advanceId = null)
    {
        if (items.Count is 0 or > MaxItems)
            throw new DomainException($"A claim needs between 1 and {MaxItems} items.");
        var today = await clock.TodayAsync();
        if (items.Any(i => i.Date > today))
            throw new DomainException("Expense dates cannot be in the future.");
        if (items.Any(i => i.Date < today.AddDays(-365)))
            throw new DomainException("Expenses older than one year cannot be claimed.");
        if (advanceId is { } aid && !await db.ExpenseAdvances.AnyAsync(a => a.Id == aid && a.EmployeeId == employeeId && a.Status == RequestStatus.Approved && a.ClosedAtUtc == null))
            throw new DomainException("That advance is not open for this person.");

        if (items.Any(i => i.ExpenseTypeId is null && !Enum.IsDefined(i.Category)))
            throw new DomainException("Choose a category for every item.");

        var claim = new ExpenseClaim { EmployeeId = employeeId, Title = title.Trim(), AdvanceId = advanceId };
        // Typed amounts per type and calendar month: each line counts against its own month's and year's limit.
        var typed = new Dictionary<(Guid TypeId, int Year, int Month), decimal>();
        foreach (var i in items)
        {
            var (category, amount) = (i.Category, i.Amount);
            if (i.ExpenseTypeId is { } typeId)
            {
                (category, amount) = await TypedAmountAsync(employeeId, typeId, i);
                typed[(typeId, i.Date.Year, i.Date.Month)] = typed.GetValueOrDefault((typeId, i.Date.Year, i.Date.Month)) + amount;
            }
            if (amount <= 0)
                throw new DomainException("Every item needs an amount greater than zero.");
            var item = new ExpenseItem
            {
                Date = i.Date,
                Category = category,
                Description = i.Description.Trim(),
                Amount = Math.Round(amount, 2),
                ExpenseTypeId = i.ExpenseTypeId,
                Quantity = i.Quantity,
                FromCity = string.IsNullOrWhiteSpace(i.FromCity) ? null : i.FromCity.Trim(),
                ToCity = string.IsNullOrWhiteSpace(i.ToCity) ? null : i.ToCity.Trim(),
            };
            if (i.Receipt is { } r)
            {
                var receipt = new ExpenseReceipt
                {
                    ExpenseClaimId = claim.Id,
                    FileName = Path.GetFileName(r.FileName.Trim()) is { Length: > 0 } n ? n[..Math.Min(n.Length, 200)] : "receipt",
                    ContentType = r.ContentType,
                    Data = DecodeReceipt(r),
                };
                db.ExpenseReceipts.Add(receipt);
                item.ReceiptId = receipt.Id;
                item.ReceiptFileName = receipt.FileName;
            }
            claim.Items.Add(item);
        }
        claim.Total = claim.Items.Sum(i => i.Amount);
        await CheckYearlyLimitsAsync(employeeId, typed);
        db.ExpenseClaims.Add(claim);

        var approval = await approvals.StartAsync(ApprovalRequestType.Expense, claim.Id, employeeId, $"{claim.Title} · Rs {claim.Total:N0}");
        if (approval.Status == ApprovalStatus.Approved)
        {
            claim.Status = ExpenseStatus.Approved;
            claim.DecidedAtUtc = clock.UtcNow;
            claim.DecisionNote = "Approved automatically";
            await SetAgainstAdvanceAsync(claim);
        }
        await db.SaveChangesAsync();
        return claim;
    }

    /// <summary>Works out a typed item's amount (rate × units or distance) and checks the type's rules.</summary>
    private async Task<(ExpenseCategory, decimal)> TypedAmountAsync(Guid employeeId, Guid typeId, NewExpenseItem i)
    {
        var type = await db.ExpenseTypes.Include(t => t.GradeRates).FirstOrDefaultAsync(t => t.Id == typeId && t.IsActive) ?? throw new NotFoundException("Expense type");
        var who = await db.Employees.Where(e => e.Id == employeeId).Select(e => new { e.GradeId, e.Gender }).FirstAsync();
        var grade = who.GradeId;
        if (type.GradeIds is { Length: > 0 } g && (grade is not { } gr || !HelpdeskService.Has(g, gr)))
            throw new DomainException($"{type.Name} is not open to your grade.");
        if (type.ForGender is { } sex && who.Gender != sex)
            throw new DomainException($"{type.Name} is only for {(sex == Gender.Female ? "women" : "men")}.");
        var rate = type.GradeRates.FirstOrDefault(r => r.GradeId == grade)?.Rate ?? type.Rate;
        decimal amount;
        switch (type.Calculation)
        {
            case ExpenseCalculation.PerUnit:
                if (i.Quantity is not { } q || q <= 0) throw new DomainException($"Enter how many {type.Unit ?? "units"} for {type.Name}.");
                amount = (rate ?? 0) * q;
                break;
            case ExpenseCalculation.Distance:
                if (string.IsNullOrWhiteSpace(i.FromCity) || string.IsNullOrWhiteSpace(i.ToCity)) throw new DomainException($"Choose where {type.Name} was from and to.");
                var km = await DistanceAsync(db, i.FromCity, i.ToCity) ?? throw new DomainException($"There is no distance from {i.FromCity} to {i.ToCity} on the list. Ask HR to add it.");
                amount = (rate ?? 0) * km;
                break;
            default:
                amount = i.Amount;
                break;
        }
        amount = Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        if (type.PerClaimLimit is { } cap && amount > cap)
            throw new DomainException($"{type.Name} is limited to Rs {cap:N0} an item.");
        if (type.ReceiptRequired && i.Receipt is null)
            throw new DomainException($"{type.Name} needs a receipt.");
        return (type.Category, amount);
    }

    /// <summary>Km between two cities, in either direction.</summary>
    public static async Task<decimal?> DistanceAsync(HrmsDbContext db, string from, string to)
    {
        var a = from.Trim();
        var b = to.Trim();
        return await db.DistanceRoutes.Where(r => (r.FromCity == a && r.ToCity == b) || (r.FromCity == b && r.ToCity == a)).Select(r => (decimal?)r.Km).FirstOrDefaultAsync();
    }

    private async Task CheckYearlyLimitsAsync(Guid employeeId, Dictionary<(Guid TypeId, int Year, int Month), decimal> typed)
    {
        if (typed.Count == 0) return;
        var who = await db.Employees.Where(e => e.Id == employeeId).Select(e => new { e.GradeId, e.JoiningDate }).FirstAsync();
        foreach (var month in typed)
        {
            var (typeId, year, m) = month.Key;
            var type = await db.ExpenseTypes.Include(t => t.GradeRates).FirstAsync(t => t.Id == typeId);
            if (type.MonthlyLimit is { } monthCap)
            {
                var from = new DateOnly(year, m, 1);
                var usedMonth = await UsedAsync(db, employeeId, typeId, from, from.AddMonths(1).AddDays(-1));
                if (usedMonth + month.Value > monthCap)
                    throw new DomainException($"{type.Name} is limited to Rs {monthCap:N0} a month; Rs {Math.Max(0, monthCap - usedMonth):N0} is left for {from:MMMM}.");
            }
        }
        foreach (var yearGroup in typed.GroupBy(x => (x.Key.TypeId, x.Key.Year)))
        {
            var (typeId, year) = yearGroup.Key;
            var amount = yearGroup.Sum(x => x.Value);
            var type = await db.ExpenseTypes.Include(t => t.GradeRates).FirstAsync(t => t.Id == typeId);
            if (await AllowanceAsync(db, type, who.GradeId, who.JoiningDate, employeeId, year) is not { } cap) continue;
            var used = await UsedAsync(db, employeeId, typeId, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
            if (used + amount > cap)
                throw new DomainException($"{type.Name} is limited to Rs {cap:N0} a year ({year}); Rs {Math.Max(0, cap - used):N0} is left.");
        }
    }

    /// <summary>What a person has claimed (pending, approved or paid) of a type between two dates.</summary>
    public static async Task<decimal> UsedAsync(HrmsDbContext db, Guid employeeId, Guid typeId, DateOnly from, DateOnly to) =>
        await db.ExpenseClaims.Where(c => c.EmployeeId == employeeId && (c.Status == ExpenseStatus.Pending || c.Status == ExpenseStatus.Approved || c.Status == ExpenseStatus.Paid))
            .SelectMany(c => c.Items).Where(i => i.ExpenseTypeId == typeId && i.Date >= from && i.Date <= to).SumAsync(i => (decimal?)(i.ApprovedAmount ?? i.Amount)) ?? 0;

    /// <summary>
    /// A person's yearly allowance for a type: the grade's or type's yearly limit, cut to the months left for a joiner (pro-rata),
    /// plus what was left unclaimed of last year's (carry forward, one year back). Null when the type has no yearly limit.
    /// </summary>
    public static async Task<decimal?> AllowanceAsync(HrmsDbContext db, ExpenseType type, Guid? grade, DateOnly joined, Guid employeeId, int year)
    {
        if ((type.GradeRates.FirstOrDefault(r => r.GradeId == grade)?.YearlyLimit ?? type.YearlyLimit) is not { } cap) return null;
        decimal Share(int y) => !type.ProRata || joined.Year < y ? cap : joined.Year > y ? 0 : Math.Round(cap * (13 - joined.Month) / 12m, 0, MidpointRounding.AwayFromZero);
        var allowance = Share(year);
        if (type.CarryForward && joined.Year < year)
            allowance += Math.Max(0, Share(year - 1) - await UsedAsync(db, employeeId, type.Id, new DateOnly(year - 1, 1, 1), new DateOnly(year - 1, 12, 31)));
        return allowance;
    }

    /// <summary>The taxable part of each claim: items of taxable types, at most what the claim pays out.</summary>
    public static async Task<Dictionary<Guid, decimal>> TaxablePartAsync(HrmsDbContext db, List<ExpenseClaim> claims)
    {
        var ids = claims.Select(c => c.Id).ToList();
        if (ids.Count == 0) return [];
        var taxable = await db.ExpenseClaims.Where(c => ids.Contains(c.Id)).SelectMany(c => c.Items)
            .Where(i => i.ExpenseTypeId != null && db.ExpenseTypes.Any(t => t.Id == i.ExpenseTypeId && t.Taxable))
            .GroupBy(i => i.ExpenseClaimId).Select(g => new { g.Key, Sum = g.Sum(i => i.ApprovedAmount ?? i.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        return claims.Where(c => taxable.ContainsKey(c.Id)).ToDictionary(c => c.Id, c => Math.Min(taxable[c.Id], Math.Max(0, c.Total - c.AdvanceAdjusted)));
    }

    /// <summary>An approved claim linked to an advance is covered by it first; only the rest is paid.</summary>
    public async Task SetAgainstAdvanceAsync(ExpenseClaim claim)
    {
        if (claim.AdvanceId is not { } id) return;
        var advance = await db.ExpenseAdvances.FirstOrDefaultAsync(a => a.Id == id);
        if (advance is null || advance.ClosedAtUtc is not null) return;
        var covered = Math.Min(claim.Total, Math.Max(0, advance.Amount - advance.Adjusted));
        advance.Adjusted += covered;
        claim.AdvanceAdjusted = covered;
    }

    private static byte[] DecodeReceipt(NewReceipt r)
    {
        if (!ReceiptTypes.Contains(r.ContentType))
            throw new DomainException("Receipts must be a JPG, PNG, WebP image or a PDF.");
        var base64 = r.Base64.Contains(',') ? r.Base64[(r.Base64.IndexOf(',') + 1)..] : r.Base64;
        byte[] data;
        try
        {
            data = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new DomainException("A receipt file could not be read.");
        }
        if (data.Length == 0 || data.Length > MaxReceiptBytes)
            throw new DomainException("Each receipt must be smaller than 3 MB.");
        return data;
    }

    /// <summary>The claimant, anyone who may approve it, and HR may view a claim.</summary>
    public async Task<bool> CanViewAsync(ExpenseClaim claim) =>
        claim.EmployeeId == me.EmployeeId || await approvals.IsApproverAsync(ApprovalRequestType.Expense, claim.Id, claim.EmployeeId);

    public Task<bool> CanDecideAsync(ExpenseClaim claim) =>
        approvals.CanDecideAsync(ApprovalRequestType.Expense, claim.Id, claim.EmployeeId);

    public async Task DecideAsync(Guid claimId, bool approve, string? note)
    {
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId) ?? throw new NotFoundException("Expense claim");
        if (claim.Status != ExpenseStatus.Pending)
            throw new DomainException($"This claim is already {claim.Status.ToString().ToLowerInvariant()}.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Expense, claim.Id, claim.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Pending)
        {
            await db.SaveChangesAsync();
            return;
        }

        claim.Status = outcome == ApprovalStatus.Approved ? ExpenseStatus.Approved : ExpenseStatus.Rejected;
        if (claim.Status == ExpenseStatus.Approved)
            await SetAgainstAdvanceAsync(claim);
        claim.DecidedByUserId = me.UserId;
        claim.DecidedAtUtc = clock.UtcNow;
        claim.DecisionNote = note;
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid claimId)
    {
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId) ?? throw new NotFoundException("Expense claim");
        if (claim.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this claim.");
        if (claim.Status != ExpenseStatus.Pending)
            throw new DomainException("Only a pending claim can be cancelled.");
        claim.Status = ExpenseStatus.Cancelled;
        await approvals.CancelAsync(ApprovalRequestType.Expense, claim.Id);
        await db.SaveChangesAsync();
    }

    /// <summary>Approved claims not yet paid and not locked into another payroll run.</summary>
    public static IQueryable<ExpenseClaim> Payable(HrmsDbContext db, Guid? forRunId = null) =>
        db.ExpenseClaims.Where(c => c.Status == ExpenseStatus.Approved && (c.PayrollRunId == null || c.PayrollRunId == forRunId));
}
