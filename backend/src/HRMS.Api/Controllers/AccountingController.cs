using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record GlAccountDto(Guid Id, string Code, string Name);
public record SaveGlAccountRequest([Required, StringLength(50)] string Code, [Required, StringLength(150)] string Name);
public record GlMappingDto(PayrollGlLine Line, string Label, Guid? GlAccountId);
public record SaveGlMappingRequest(List<GlMappingItem> Items);
public record GlMappingItem(PayrollGlLine Line, Guid? GlAccountId);

/// <summary>One journal line. Department is set on the expense lines, which are split by department.</summary>
public record JournalLineDto(string AccountCode, string AccountName, string Description, string? Department, decimal Debit, decimal Credit);
public record JournalDto(int Year, int Month, DateOnly Date, List<JournalLineDto> Lines, decimal TotalDebit, decimal TotalCredit, List<string> Unmapped);

/// <summary>Chart of accounts, payroll GL mapping and the monthly payroll journal voucher for the accounting system.</summary>
[ApiController]
[Route("api/payroll")]
[Authorize(Policy = Policies.Hr)]
public class AccountingController(HrmsDbContext db) : ControllerBase
{
    public static readonly Dictionary<PayrollGlLine, string> Labels = new()
    {
        [PayrollGlLine.SalaryExpense] = "Salaries expense",
        [PayrollGlLine.EmployerContributionExpense] = "Employer contributions expense (EOBI, provident fund, social security)",
        [PayrollGlLine.IncomeTaxPayable] = "Income tax payable",
        [PayrollGlLine.EobiPayable] = "EOBI payable",
        [PayrollGlLine.ProvidentFundPayable] = "Provident fund payable",
        [PayrollGlLine.SocialSecurityPayable] = "Social security payable",
        [PayrollGlLine.OtherDeductionsPayable] = "Loans, advances and other deductions",
        [PayrollGlLine.SalaryPayable] = "Salaries payable (net pay)",
    };

    [HttpGet("gl/accounts")]
    public Task<List<GlAccountDto>> Accounts() =>
        db.GlAccounts.OrderBy(a => a.Code).Select(a => new GlAccountDto(a.Id, a.Code, a.Name)).ToListAsync();

    [HttpPost("gl/accounts")]
    public async Task<GlAccountDto> AddAccount(SaveGlAccountRequest req)
    {
        var code = req.Code.Trim();
        if (await db.GlAccounts.AnyAsync(a => a.Code == code))
            throw new DomainException($"Account {code} already exists.");
        var a = new GlAccount { Code = code, Name = req.Name.Trim() };
        db.GlAccounts.Add(a);
        await db.SaveChangesAsync();
        return new(a.Id, a.Code, a.Name);
    }

    [HttpPut("gl/accounts/{id:guid}")]
    public async Task<GlAccountDto> UpdateAccount(Guid id, SaveGlAccountRequest req)
    {
        var a = await db.GlAccounts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Account");
        var code = req.Code.Trim();
        if (await db.GlAccounts.AnyAsync(x => x.Id != id && x.Code == code))
            throw new DomainException($"Account {code} already exists.");
        a.Code = code;
        a.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return new(a.Id, a.Code, a.Name);
    }

    [HttpDelete("gl/accounts/{id:guid}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var a = await db.GlAccounts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Account");
        if (await db.PayrollGlMappings.AnyAsync(m => m.GlAccountId == id))
            throw new DomainException("This account is used in the payroll mapping. Change the mapping first.");
        db.GlAccounts.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("gl/mapping")]
    public async Task<List<GlMappingDto>> Mapping()
    {
        var map = await db.PayrollGlMappings.ToDictionaryAsync(m => m.Line, m => m.GlAccountId);
        return Labels.Select(l => new GlMappingDto(l.Key, l.Value, map.TryGetValue(l.Key, out var id) ? id : null)).ToList();
    }

    [HttpPut("gl/mapping")]
    public async Task<List<GlMappingDto>> SaveMapping(SaveGlMappingRequest req)
    {
        var ids = req.Items.Where(i => i.GlAccountId is not null).Select(i => i.GlAccountId!.Value).Distinct().ToList();
        if (await db.GlAccounts.CountAsync(a => ids.Contains(a.Id)) != ids.Count)
            throw new NotFoundException("Account");
        var existing = await db.PayrollGlMappings.ToListAsync();
        foreach (var item in req.Items.Where(i => Labels.ContainsKey(i.Line)))
        {
            var row = existing.FirstOrDefault(m => m.Line == item.Line);
            if (item.GlAccountId is null)
            {
                if (row is not null) db.PayrollGlMappings.Remove(row);
            }
            else if (row is null)
                db.PayrollGlMappings.Add(new PayrollGlMapping { Line = item.Line, GlAccountId = item.GlAccountId.Value });
            else
                row.GlAccountId = item.GlAccountId.Value;
        }
        await db.SaveChangesAsync();
        return await Mapping();
    }

    /// <summary>The payroll journal voucher for a run: expenses by department on the debit side, what is owed on the credit side.</summary>
    [HttpGet("runs/{id:guid}/journal")]
    public Task<JournalDto> Journal(Guid id) => BuildAsync(id);

    /// <summary>The same voucher as CSV for import into the accounting software.</summary>
    [HttpGet("runs/{id:guid}/journal.csv")]
    public async Task<IActionResult> JournalCsv(Guid id)
    {
        var j = await BuildAsync(id);
        if (j.Unmapped.Count > 0)
            throw new DomainException($"Map every payroll line to an account first: {string.Join(", ", j.Unmapped)}.");
        var sb = new StringBuilder("Date,Account code,Account name,Description,Department,Debit,Credit\r\n");
        static string C(string? s) => s is null ? "" : s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        foreach (var l in j.Lines)
            sb.Append(CultureInfo.InvariantCulture, $"{j.Date:yyyy-MM-dd},{C(l.AccountCode)},{C(l.AccountName)},{C(l.Description)},{C(l.Department)},{l.Debit:0.00},{l.Credit:0.00}\r\n");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv",
            $"payroll-journal-{j.Year}-{j.Month:00}.csv");
    }

    private async Task<JournalDto> BuildAsync(Guid runId)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        var slips = await db.Payslips.Where(p => p.PayrollRunId == runId)
            .Select(p => new
            {
                Department = p.Employee!.Department != null ? p.Employee.Department.Name : "Unassigned",
                p.GrossEarnings, p.IncomeTax, p.EobiEmployee, p.EobiEmployer, p.PfEmployee, p.PfEmployer, p.SocialSecurityEmployer,
                p.OtherDeductions, p.NetPay,
            })
            .ToListAsync();
        var accounts = await db.GlAccounts.ToDictionaryAsync(a => a.Id);
        var map = await db.PayrollGlMappings.ToDictionaryAsync(m => m.Line, m => accounts.GetValueOrDefault(m.GlAccountId));
        var unmapped = Labels.Where(l => map.GetValueOrDefault(l.Key) is null).Select(l => l.Value).ToList();
        var period = new DateOnly(run.Year, run.Month, 1);
        var date = period.AddMonths(1).AddDays(-1);
        var month = period.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var lines = new List<JournalLineDto>();

        void Add(PayrollGlLine line, string description, string? department, decimal debit, decimal credit)
        {
            if (debit == 0 && credit == 0) return;
            var a = map.GetValueOrDefault(line);
            lines.Add(new(a?.Code ?? "", a?.Name ?? Labels[line], description, department, Math.Round(debit, 2), Math.Round(credit, 2)));
        }

        foreach (var d in slips.GroupBy(s => s.Department).OrderBy(g => g.Key))
        {
            Add(PayrollGlLine.SalaryExpense, $"Salaries {month}", d.Key, d.Sum(s => s.GrossEarnings), 0);
            Add(PayrollGlLine.EmployerContributionExpense, $"Employer contributions {month}", d.Key,
                d.Sum(s => s.EobiEmployer + s.PfEmployer + s.SocialSecurityEmployer), 0);
        }
        Add(PayrollGlLine.IncomeTaxPayable, $"Income tax withheld {month}", null, 0, slips.Sum(s => s.IncomeTax));
        Add(PayrollGlLine.EobiPayable, $"EOBI {month}", null, 0, slips.Sum(s => s.EobiEmployee + s.EobiEmployer));
        Add(PayrollGlLine.ProvidentFundPayable, $"Provident fund {month}", null, 0, slips.Sum(s => s.PfEmployee + s.PfEmployer));
        Add(PayrollGlLine.SocialSecurityPayable, $"Social security {month}", null, 0, slips.Sum(s => s.SocialSecurityEmployer));
        // Provident fund is inside "other deductions" on the payslip; it already has its own line above.
        Add(PayrollGlLine.OtherDeductionsPayable, $"Other deductions {month}", null, 0, slips.Sum(s => s.OtherDeductions - s.PfEmployee));
        Add(PayrollGlLine.SalaryPayable, $"Net salaries payable {month}", null, 0, slips.Sum(s => s.NetPay));

        // Rounding on individual payslips can leave the voucher a rupee out; show it rather than hide it.
        var diff = lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit);
        if (diff != 0)
            Add(PayrollGlLine.SalaryPayable, "Rounding difference", null, diff < 0 ? -diff : 0, diff > 0 ? diff : 0);

        return new JournalDto(run.Year, run.Month, date, lines, lines.Sum(l => l.Debit), lines.Sum(l => l.Credit), unmapped);
    }
}
