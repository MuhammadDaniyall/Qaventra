using System.Text.RegularExpressions;

namespace HRMS.Domain.Services;

public record LetterPlaceholder(string Key, string Label, string Example);

/// <summary>Fills {{Key}} placeholders in letter templates. Unknown keys are left as typed so HR can spot them.</summary>
public static partial class LetterRenderer
{
    public static readonly IReadOnlyList<LetterPlaceholder> Placeholders =
    [
        new("EmployeeName", "Employee full name", "Ayesha Khan"),
        new("FirstName", "First name", "Ayesha"),
        new("EmployeeCode", "Employee code", "EMP-0012"),
        new("Designation", "Designation", "Senior Accountant"),
        new("Department", "Department", "Finance"),
        new("JoiningDate", "Joining date", "1 March 2024"),
        new("LeavingDate", "Leaving date", "31 October 2026"),
        new("Cnic", "CNIC", "35202-1234567-1"),
        new("ManagerName", "Line manager", "Faisal Ahmed"),
        new("BasicSalary", "Basic salary", "Rs 120,000"),
        new("GrossSalary", "Gross salary", "Rs 165,000"),
        new("CompanyName", "Company name", "Qaventra Pvt Ltd"),
        new("CompanyAddress", "Company address", "Gulberg III, Lahore"),
        new("Today", "Today's date", "20 October 2026"),
    ];

    [GeneratedRegex(@"\{\{\s*([A-Za-z]+)\s*\}\}")]
    private static partial Regex Token();

    public static string Render(string template, IReadOnlyDictionary<string, string?> values) =>
        Token().Replace(template, m =>
            values.TryGetValue(m.Groups[1].Value, out var v) ? (string.IsNullOrWhiteSpace(v) ? "—" : v) : m.Value);

    /// <summary>Placeholder keys used in the text that the renderer does not know.</summary>
    public static IReadOnlyList<string> UnknownKeys(string template) =>
        Token().Matches(template).Select(m => m.Groups[1].Value)
            .Where(k => Placeholders.All(p => !p.Key.Equals(k, StringComparison.Ordinal)))
            .Distinct().ToList();

    public static string CategoryCode(LetterCategory c) => c switch
    {
        LetterCategory.Offer => "OFR",
        LetterCategory.Appointment => "APT",
        LetterCategory.Confirmation => "CNF",
        LetterCategory.Increment => "INC",
        LetterCategory.Experience => "EXP",
        LetterCategory.Relieving => "REL",
        LetterCategory.SalaryCertificate => "SAL",
        LetterCategory.Warning => "WRN",
        _ => "GEN",
    };
}
