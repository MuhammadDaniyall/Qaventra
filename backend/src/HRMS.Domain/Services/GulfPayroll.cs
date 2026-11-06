using System.Globalization;
using System.Text;

namespace HRMS.Domain.Services;

/// <summary>
/// Payroll countries. PK is the default and keeps the Pakistan engine (FBR tax, EOBI, PF, VPS, PESSI) exactly as it was.
/// AE and SA switch income tax off and use GPSSA / GOSI, Gulf gratuity and WPS files instead. QA, OM, BH and KW are
/// listed so the setting, currency and gratuity hooks are ready; their social insurance rules are not built yet.
/// </summary>
public static class PayrollRegime
{
    public const string Pakistan = "PK";
    public const string Uae = "AE";
    public const string Saudi = "SA";

    /// <summary>Countries a company can choose today.</summary>
    public static readonly IReadOnlyList<string> Supported = [Pakistan, Uae, Saudi];

    public static bool IsPakistan(string? country) => string.IsNullOrEmpty(country) || country == Pakistan;
    public static bool IsGulf(string? country) => !IsPakistan(country);

    public static string Normalize(string? country) =>
        string.IsNullOrWhiteSpace(country) ? Pakistan : country.Trim().ToUpperInvariant();

    public static string DefaultCurrency(string? country) => Normalize(country) switch
    {
        Uae => "AED",
        Saudi => "SAR",
        "QA" => "QAR",
        "OM" => "OMR",
        "BH" => "BHD",
        "KW" => "KWD",
        _ => "PKR",
    };

    /// <summary>Amount as shown on payslips and reports: "PKR 12,500" / "AED 12,500.50".</summary>
    public static string Money(decimal amount, string? currency)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? "PKR" : currency;
        var text = code == "PKR" ? amount.ToString("N0", CultureInfo.InvariantCulture) : amount.ToString("#,0.##", CultureInfo.InvariantCulture);
        return $"{code} {text}";
    }

    /// <summary>A salary component counts as housing for GOSI and the gratuity wage when its name says so.</summary>
    public static bool IsHousing(string? componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName))
            return false;
        var n = componentName.ToLowerInvariant();
        return n.Contains("housing") || n.Contains("accommodation") || n.Contains("house rent") || n.Contains("سكن");
    }
}

/// <summary>
/// Saudi GOSI rates. Defaults are the official rates published by GOSI (gosi.gov.sa, "Contributions" page) and the
/// Social Insurance Law (Royal Decree M/33 of 1421H) as amended by the new Social Insurance Law effective 3 July 2024:
/// <list type="bullet">
/// <item>Saudi nationals: annuities (pension) 9% employee + 9% employer; SANED unemployment 0.75% + 0.75%;
///   occupational hazards 2% employer. Total 21.5%.</item>
/// <item>Non-Saudis: occupational hazards 2% employer only, no employee share.</item>
/// <item>Contribution wage = basic + housing allowance, at least SAR 1,500 and at most SAR 45,000 a month.</item>
/// <item>July 2024 reform: Saudis first registered on or after 3 July 2024 ("new entrants") pay a rising annuity rate,
///   9% each side to start, then +0.5% each side every July from July 2025 until 11% each side (July 2028). People
///   registered before then stay on 9%.</item>
/// </list>
/// Every number is a company setting so HR can follow later GOSI circulars without a release.
/// </summary>
public record GosiRates(
    decimal AnnuityEmployeePercent = 9m,
    decimal AnnuityEmployerPercent = 9m,
    decimal SanedEmployeePercent = 0.75m,
    decimal SanedEmployerPercent = 0.75m,
    decimal HazardEmployerPercent = 2m,
    decimal WageFloor = 1500m,
    decimal WageCeiling = 45000m,
    decimal NewEntrantStepPercent = 0.5m,
    decimal NewEntrantMaxPercent = 11m);

public record GosiResult(decimal WageBase, decimal AnnuityEmployee, decimal AnnuityEmployer, decimal SanedEmployee, decimal SanedEmployer,
    decimal HazardEmployer, decimal AnnuityPercent)
{
    public decimal Employee => AnnuityEmployee + SanedEmployee;
    public decimal Employer => AnnuityEmployer + SanedEmployer + HazardEmployer;
}

public static class GosiCalculator
{
    /// <summary>The day the new Social Insurance Law took effect.</summary>
    public static readonly DateOnly ReformDate = new(2024, 7, 3);

    /// <summary>
    /// Annuity percent (each side) for a Saudi in the month of <paramref name="on"/>: the base rate, or for a new entrant
    /// the base rate plus one step for every July from 2025 that has passed, up to the maximum.
    /// </summary>
    public static decimal AnnuityPercent(GosiRates r, bool newEntrant, DateOnly on, bool employer)
    {
        var baseRate = employer ? r.AnnuityEmployerPercent : r.AnnuityEmployeePercent;
        if (!newEntrant || r.NewEntrantStepPercent <= 0)
            return baseRate;
        var steps = on.Year - 2025 + (on.Month >= 7 ? 1 : 0);
        return steps <= 0 ? baseRate : Math.Min(Math.Max(baseRate, r.NewEntrantMaxPercent), baseRate + steps * r.NewEntrantStepPercent);
    }

    /// <summary>Monthly GOSI on basic + housing for one person. <paramref name="share"/> scales the floor and ceiling for shorter pay periods.</summary>
    public static GosiResult Calculate(GosiRates r, decimal basic, decimal housing, bool isSaudi, bool newEntrant, DateOnly on, decimal share = 1m)
    {
        var wage = basic + housing;
        if (wage <= 0)
            return new GosiResult(0, 0, 0, 0, 0, 0, 0);
        if (r.WageCeiling > 0)
            wage = Math.Min(wage, r.WageCeiling * share);
        if (isSaudi && r.WageFloor > 0)
            wage = Math.Max(wage, r.WageFloor * share);
        static decimal Pct(decimal amount, decimal pct) => Math.Round(amount * pct / 100m, 2, MidpointRounding.AwayFromZero);
        var hazard = Pct(wage, r.HazardEmployerPercent);
        if (!isSaudi)
            return new GosiResult(wage, 0, 0, 0, 0, hazard, 0);
        var empPct = AnnuityPercent(r, newEntrant, on, false);
        var erPct = AnnuityPercent(r, newEntrant, on, true);
        return new GosiResult(wage, Pct(wage, empPct), Pct(wage, erPct), Pct(wage, r.SanedEmployeePercent), Pct(wage, r.SanedEmployerPercent), hazard, empPct);
    }
}

/// <summary>
/// UAE pension for Emirati nationals (GPSSA, Federal Decree-Law 57 of 2023, in force 31 Oct 2023): employee 11%,
/// employer 12.5% of the contribution salary (the state adds 2.5% itself, outside payroll). Contribution salary
/// AED 3,000 to AED 70,000 a month. Abu Dhabi entities use ADPF instead (different rates), so all of it is a setting.
/// Non-nationals: nothing.
/// </summary>
public record PensionRates(decimal EmployeePercent = 11m, decimal EmployerPercent = 12.5m, decimal WageFloor = 3000m, decimal WageCeiling = 70000m);

public static class PensionCalculator
{
    public static (decimal Wage, decimal Employee, decimal Employer) Calculate(PensionRates r, decimal contributionWage, bool isNational, decimal share = 1m)
    {
        if (!isNational || contributionWage <= 0)
            return (0, 0, 0);
        var wage = contributionWage;
        if (r.WageCeiling > 0)
            wage = Math.Min(wage, r.WageCeiling * share);
        if (r.WageFloor > 0)
            wage = Math.Max(wage, r.WageFloor * share);
        return (wage, Math.Round(wage * r.EmployeePercent / 100m, 2, MidpointRounding.AwayFromZero),
            Math.Round(wage * r.EmployerPercent / 100m, 2, MidpointRounding.AwayFromZero));
    }
}

/// <summary>Which monthly wage end-of-service gratuity is worked out on.</summary>
public enum GratuityWage
{
    Basic = 0,
    BasicPlusHousing = 1,
    Gross = 2,
}

public record GulfGratuityResult(decimal ServiceYears, decimal FullAmount, decimal Fraction, decimal Amount, string Explanation);

/// <summary>End-of-service gratuity under UAE and Saudi labour law.</summary>
public static class GulfGratuity
{
    /// <summary>Service in years (days / 365), counting both the first and the last day.</summary>
    public static decimal ServiceYears(DateOnly joined, DateOnly lastDay) =>
        lastDay < joined ? 0 : (lastDay.DayNumber - joined.DayNumber + 1) / 365m;

    /// <summary>
    /// UAE, Federal Decree-Law 33 of 2021, Article 51: nothing under one year of continuous service; 21 days' basic wage
    /// per year for the first five years and 30 days per year after, pro-rata for part years; the total never exceeds two
    /// years' wage. Resignation no longer reduces it under the 2021 law. Daily wage = monthly basic / 30.
    /// </summary>
    public static GulfGratuityResult Uae(decimal monthlyWage, DateOnly joined, DateOnly lastDay)
    {
        var years = ServiceYears(joined, lastDay);
        if (years < 1 || monthlyWage <= 0)
            return new GulfGratuityResult(Math.Round(years, 2), 0, 0, 0, "Under one year of service: no gratuity (UAE law, Art. 51).");
        var daily = monthlyWage / 30m;
        var days = Math.Min(years, 5) * 21m + Math.Max(0, years - 5) * 30m;
        var amount = Math.Min(daily * days, monthlyWage * 24m);
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var capped = daily * days > monthlyWage * 24m ? ", capped at two years' wage" : "";
        return new GulfGratuityResult(Math.Round(years, 2), amount, 1, amount, $"{days:0.##} days' wage for {years:0.##} years{capped}.");
    }

    /// <summary>
    /// Saudi Arabia, Labour Law Articles 84 and 85: half a month's wage per year for the first five years and a full
    /// month per year after, pro-rata for part years. On resignation: nothing under 2 years, one third for 2 to 5 years,
    /// two thirds for 5 to 10 years, all of it from 10 years. Other exits (termination, contract end, retirement) get it
    /// all; dismissal under Article 80 forfeits it and is left to HR as a manual deduction.
    /// </summary>
    public static GulfGratuityResult Saudi(decimal monthlyWage, DateOnly joined, DateOnly lastDay, bool resigned)
    {
        var years = ServiceYears(joined, lastDay);
        if (monthlyWage <= 0 || years <= 0)
            return new GulfGratuityResult(Math.Round(years, 2), 0, 0, 0, "No wage or service.");
        var full = Math.Min(years, 5) * monthlyWage / 2m + Math.Max(0, years - 5) * monthlyWage;
        full = Math.Round(full, 2, MidpointRounding.AwayFromZero);
        var fraction = !resigned ? 1m : years < 2 ? 0m : years < 5 ? 1m / 3m : years < 10 ? 2m / 3m : 1m;
        var amount = Math.Round(full * fraction, 2, MidpointRounding.AwayFromZero);
        var why = !resigned ? "full award (Art. 84)"
            : fraction == 0 ? "resigned under 2 years: nothing (Art. 85)"
            : fraction == 1 ? "resigned after 10 years: full award (Art. 85)"
            : $"resigned: {(fraction < 0.5m ? "one third" : "two thirds")} of the award (Art. 85)";
        return new GulfGratuityResult(Math.Round(years, 2), full, fraction, amount, $"{years:0.##} years of service; {why}.");
    }

    /// <summary>The country's rule; null for Pakistan (which keeps its own gratuity scheme) or a country not built yet.</summary>
    public static GulfGratuityResult? For(string? country, decimal monthlyWage, DateOnly joined, DateOnly lastDay, bool resigned) => PayrollRegime.Normalize(country) switch
    {
        PayrollRegime.Uae => Uae(monthlyWage, joined, lastDay),
        PayrollRegime.Saudi => Saudi(monthlyWage, joined, lastDay, resigned),
        _ => null,
    };
}

/// <summary>One person's line in a WPS / bank salary file.</summary>
public record WpsLine(
    string EmployeeCode, string EmployeeName, string? PersonId, string? RoutingCode, string? Iban, decimal Fixed, decimal Variable,
    decimal Net, decimal Basic, decimal Housing, decimal OtherEarnings, decimal Deductions, int LeaveDays);

public record WpsEmployer(string? EstablishmentId, string? BankRoutingCode, string? Iban, string? AgentId, string? Reference, string Currency);

public static class WpsFiles
{
    static string Amt(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// UAE WPS Salary Information File (SIF), as specified by the Central Bank of the UAE / MOHRE:
    /// one EDR line per employee then one SCR line, comma separated, no header.
    /// <code>
    /// EDR,PersonId(14),AgentRoutingCode(9),IBAN,PayStart(yyyy-MM-dd),PayEnd(yyyy-MM-dd),DaysInPeriod,FixedSalary,VariableSalary,DaysOnLeave
    /// SCR,EmployerId(13),EmployerBankRoutingCode(9),FileDate(yyyy-MM-dd),FileTime(HHmm),SalaryMonth(MMyyyy),EdrCount,TotalSalary,AED,EmployerReference
    /// </code>
    /// File name: EmployerId + yyMMddHHmmss + ".SIF".
    /// </summary>
    public static (string FileName, string Content) UaeSif(WpsEmployer er, IReadOnlyList<WpsLine> lines, DateOnly start, DateOnly end, DateTime createdLocal)
    {
        var sb = new StringBuilder();
        var days = end.DayNumber - start.DayNumber + 1;
        foreach (var l in lines)
            sb.Append("EDR,").Append(Digits(l.PersonId)).Append(',').Append(Digits(l.RoutingCode ?? er.AgentId)).Append(',')
              .Append(Clean(l.Iban)).Append(',').Append(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
              .Append(end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',').Append(days).Append(',')
              .Append(Amt(l.Fixed)).Append(',').Append(Amt(l.Variable)).Append(',').Append(l.LeaveDays).Append("\r\n");
        var total = lines.Sum(l => l.Fixed + l.Variable);
        sb.Append("SCR,").Append(Digits(er.EstablishmentId)).Append(',').Append(Digits(er.BankRoutingCode)).Append(',')
          .Append(createdLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',').Append(createdLocal.ToString("HHmm", CultureInfo.InvariantCulture)).Append(',')
          .Append(end.ToString("MMyyyy", CultureInfo.InvariantCulture)).Append(',').Append(lines.Count).Append(',').Append(Amt(total)).Append(',')
          .Append("AED,").Append(Clean(er.Reference)).Append("\r\n");
        var name = $"{Digits(er.EstablishmentId)}{createdLocal.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture)}.SIF";
        return (name, sb.ToString());
    }

    /// <summary>
    /// Saudi bank payroll file for WPS / Mudad upload, as CSV with a header row. Saudi banks each publish their own
    /// template; these columns are the ones every one of them asks for (Mudad "salary payment" fields), so the file can
    /// be uploaded directly or mapped column by column:
    /// <list type="number">
    /// <item>Employee Number – the company's code.</item>
    /// <item>Employee Name.</item>
    /// <item>National ID / Iqama – 10 digits (1… Saudi, 2… resident).</item>
    /// <item>Employee IBAN – SA + 22 characters.</item>
    /// <item>Bank Code – the employee bank's short code or SWIFT (from the employee's branch code).</item>
    /// <item>Net Salary – amount to transfer.</item>
    /// <item>Basic Salary, Housing Allowance, Other Earnings, Deductions – the breakdown WPS checks against GOSI.</item>
    /// <item>Currency (SAR), Payment Month (yyyy-MM).</item>
    /// <item>Establishment ID (MHRSD / Mudad establishment number) and Employer IBAN – the paying account.</item>
    /// </list>
    /// </summary>
    public static (string FileName, string Content) SaudiCsv(WpsEmployer er, IReadOnlyList<WpsLine> lines, DateOnly end)
    {
        var sb = new StringBuilder();
        sb.Append("Employee Number,Employee Name,National ID / Iqama,Employee IBAN,Bank Code,Net Salary,Basic Salary,Housing Allowance,Other Earnings,Deductions,Currency,Payment Month,Establishment ID,Employer IBAN\r\n");
        foreach (var l in lines)
            sb.Append(Csv(l.EmployeeCode)).Append(',').Append(Csv(l.EmployeeName)).Append(',').Append(Digits(l.PersonId)).Append(',')
              .Append(Clean(l.Iban)).Append(',').Append(Csv(l.RoutingCode ?? "")).Append(',').Append(Amt(l.Net)).Append(',')
              .Append(Amt(l.Basic)).Append(',').Append(Amt(l.Housing)).Append(',').Append(Amt(l.OtherEarnings)).Append(',').Append(Amt(l.Deductions)).Append(',')
              .Append(er.Currency).Append(',').Append(end.ToString("yyyy-MM", CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(er.EstablishmentId ?? "")).Append(',').Append(Clean(er.Iban)).Append("\r\n");
        return ($"payroll-{end:yyyy-MM}-SA.csv", sb.ToString());
    }

    static string Digits(string? s) => new string((s ?? "").Where(char.IsAsciiDigit).ToArray());
    static string Clean(string? s) => new string((s ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
    static string Csv(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
