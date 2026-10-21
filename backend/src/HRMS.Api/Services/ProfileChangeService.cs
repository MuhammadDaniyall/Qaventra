using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record FieldChange(string Field, string Label, string? From, string? To);

/// <summary>
/// Employees ask to correct their own personal details (name, CNIC, phone, bank...). HR edits them directly;
/// everyone else goes through the profile-change approval chain, and the change is applied once approved.
/// </summary>
public class ProfileChangeService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record ProfileField(string Label, Func<Employee, string?> Get, Action<Employee, string?> Set, Func<string?, string?> Normalize);

    private static readonly Regex CnicPattern = new(@"^\d{5}-\d{7}-\d$");
    private static readonly string[] BloodGroups = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];

    /// <summary>The details an employee may ask to change, by field key (camelCase, as the API uses).</summary>
    private static readonly Dictionary<string, ProfileField> Fields = new()
    {
        ["firstName"] = new("First name", e => e.FirstName, (e, v) => e.FirstName = v!, v => Required(v, 100, "First name")),
        ["lastName"] = new("Last name", e => e.LastName, (e, v) => e.LastName = v!, v => Required(v, 100, "Last name")),
        ["phone"] = new("Phone", e => e.Phone, (e, v) => e.Phone = v, v => Optional(v, 30)),
        ["personalEmail"] = new("Personal email", e => e.PersonalEmail, (e, v) => e.PersonalEmail = v, NormalizeEmail),
        ["address"] = new("Address", e => e.Address, (e, v) => e.Address = v, v => Optional(v, 500)),
        ["cnic"] = new("CNIC", e => e.Cnic, (e, v) => e.Cnic = v, NormalizeCnic),
        ["dateOfBirth"] = new("Date of birth", e => e.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            (e, v) => e.DateOfBirth = v is null ? null : DateOnly.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture), NormalizeDate),
        ["maritalStatus"] = new("Marital status", e => e.MaritalStatus?.ToString(),
            (e, v) => e.MaritalStatus = v is null ? null : Enum.Parse<MaritalStatus>(v), NormalizeMarital),
        ["bloodGroup"] = new("Blood group", e => e.BloodGroup, (e, v) => e.BloodGroup = v, NormalizeBloodGroup),
        ["bankName"] = new("Bank", e => e.BankName, (e, v) => e.BankName = v, v => Optional(v, 100)),
        ["bankAccount"] = new("Bank account / IBAN", e => e.BankAccount, (e, v) => e.BankAccount = v, v => Optional(v, 50)),
    };

    public static IReadOnlyList<FieldChange> Read(string json) =>
        JsonSerializer.Deserialize<List<FieldChange>>(json, Json) ?? [];

    public async Task<ProfileChangeRequest> SubmitAsync(Guid employeeId, IReadOnlyDictionary<string, string?> requested, string? reason)
    {
        if (employeeId != me.EmployeeId)
            throw new ForbiddenException("You can only ask to change your own details.");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId) ?? throw new NotFoundException("Employee");
        if (await db.ProfileChangeRequests.AnyAsync(r => r.EmployeeId == employeeId && r.Status == RequestStatus.Pending))
            throw new DomainException("You already have a change request waiting for approval. Cancel it to send a new one.");

        var changes = new List<FieldChange>();
        foreach (var (key, raw) in requested)
        {
            if (!Fields.TryGetValue(key, out var field))
                throw new DomainException($"'{key}' cannot be changed through a request.");
            var to = field.Normalize(raw);
            var from = field.Get(employee);
            if (!string.Equals(from ?? "", to ?? "", StringComparison.Ordinal))
                changes.Add(new FieldChange(key, field.Label, from, to));
        }
        if (changes.Count == 0)
            throw new DomainException("Nothing has changed. Edit at least one detail.");

        var request = new ProfileChangeRequest
        {
            EmployeeId = employeeId,
            ChangesJson = JsonSerializer.Serialize(changes, Json),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        };
        db.ProfileChangeRequests.Add(request);
        var summary = "Update " + string.Join(", ", changes.Select(c => c.Label.ToLowerInvariant()));
        var approval = await approvals.StartAsync(ApprovalRequestType.ProfileChange, request.Id, employeeId,
            summary.Length > 300 ? summary[..297] + "..." : summary);
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyAsync(request, employee, approved: true, "Approved automatically");
        await db.SaveChangesAsync();
        return request;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var request = await db.ProfileChangeRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Change request");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.ProfileChange, request.Id, request.EmployeeId, approve, note);
        if (outcome != ApprovalStatus.Pending)
        {
            var employee = await db.Employees.FirstAsync(e => e.Id == request.EmployeeId);
            await ApplyAsync(request, employee, outcome == ApprovalStatus.Approved, note);
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id)
    {
        var request = await db.ProfileChangeRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Change request");
        if (request.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this request.");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException("Only a pending request can be cancelled.");
        request.Status = RequestStatus.Cancelled;
        await approvals.CancelAsync(ApprovalRequestType.ProfileChange, request.Id);
        await db.SaveChangesAsync();
    }

    /// <summary>Approved: writes each new value. The login's display name follows the employee's name.</summary>
    private async Task ApplyAsync(ProfileChangeRequest request, Employee employee, bool approved, string? note)
    {
        request.Status = approved ? RequestStatus.Approved : RequestStatus.Rejected;
        request.DecidedAtUtc = clock.UtcNow;
        request.DecisionNote = note;
        if (!approved)
            return;
        foreach (var c in Read(request.ChangesJson))
        {
            if (Fields.TryGetValue(c.Field, out var field))
                field.Set(employee, c.To);
        }
        var login = await db.Users.FirstOrDefaultAsync(u => u.EmployeeId == employee.Id);
        if (login is not null)
            login.FullName = employee.FullName;
    }

    private static string? Optional(string? v, int max)
    {
        v = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        if (v is not null && v.Length > max)
            throw new DomainException($"Keep it under {max} characters.");
        return v;
    }

    private static string Required(string? v, int max, string label) =>
        Optional(v, max) ?? throw new DomainException($"{label} cannot be empty.");

    private static string? NormalizeEmail(string? v)
    {
        v = Optional(v, 256)?.ToLowerInvariant();
        if (v is not null && !MailAddress.TryCreate(v, out _))
            throw new DomainException("Enter a valid personal email.");
        return v;
    }

    private static string? NormalizeCnic(string? v)
    {
        v = Optional(v, 15);
        if (v is not null && !CnicPattern.IsMatch(v))
            throw new DomainException("CNIC must look like 12345-1234567-1.");
        return v;
    }

    private static string? NormalizeDate(string? v)
    {
        v = Optional(v, 10);
        if (v is null)
            return null;
        if (!DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) || d.Year < 1930 || d > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-14))
            throw new DomainException("Enter a valid date of birth.");
        return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? NormalizeMarital(string? v)
    {
        v = Optional(v, 20);
        if (v is null)
            return null;
        if (!Enum.TryParse<MaritalStatus>(v, true, out var m) || !Enum.IsDefined(m))
            throw new DomainException("Choose a marital status.");
        return m.ToString();
    }

    private static string? NormalizeBloodGroup(string? v)
    {
        v = Optional(v, 5)?.ToUpperInvariant();
        if (v is not null && !BloodGroups.Contains(v))
            throw new DomainException("Choose a blood group such as B+ or O-.");
        return v;
    }
}
