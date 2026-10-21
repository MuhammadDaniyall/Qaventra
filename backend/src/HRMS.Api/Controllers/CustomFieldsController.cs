using System.ComponentModel.DataAnnotations;
using System.Globalization;
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

public record CustomFieldDto(
    Guid Id, string Label, CustomFieldType Type, List<string> Options, bool IsRequired, bool VisibleToEmployee, int Order, bool IsActive, int Filled);

public record SaveCustomFieldRequest(
    [Required, StringLength(100)] string Label, CustomFieldType Type, List<string>? Options, bool IsRequired, bool VisibleToEmployee, bool IsActive = true);

/// <summary>A field with this employee's value (empty string when not filled).</summary>
public record CustomValueDto(Guid FieldId, string Label, CustomFieldType Type, List<string> Options, bool IsRequired, string Value);

public record SaveCustomValuesRequest([Required] Dictionary<Guid, string?> Values);

[ApiController]
[Authorize(Policy = Policies.CompanyUser)]
public class CustomFieldsController(HrmsDbContext db, ICurrentUser me) : ControllerBase
{
    [HttpGet("api/setup/custom-fields"), Authorize(Policy = Policies.Hr)]
    public async Task<List<CustomFieldDto>> List()
    {
        var filled = await db.CustomFieldValues.GroupBy(v => v.FieldId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return (await db.CustomFields.OrderBy(f => f.Order).ThenBy(f => f.Label).ToListAsync())
            .Select(f => new CustomFieldDto(f.Id, f.Label, f.Type, OptionsOf(f), f.IsRequired, f.VisibleToEmployee, f.Order, f.IsActive, filled.GetValueOrDefault(f.Id)))
            .ToList();
    }

    [HttpPost("api/setup/custom-fields"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Create(SaveCustomFieldRequest req)
    {
        if (await db.CustomFields.CountAsync() >= 30)
            throw new DomainException("A company can have up to 30 custom fields.");
        var field = new CustomField { Order = (await db.CustomFields.MaxAsync(f => (int?)f.Order) ?? 0) + 1 };
        await Apply(field, req);
        db.CustomFields.Add(field);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("api/setup/custom-fields/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Update(Guid id, SaveCustomFieldRequest req)
    {
        var field = await db.CustomFields.FirstOrDefaultAsync(f => f.Id == id) ?? throw new NotFoundException("Custom field");
        if (field.Type != req.Type && await db.CustomFieldValues.AnyAsync(v => v.FieldId == id))
            throw new DomainException("This field already has values, so its type cannot change. Add a new field instead.");
        await Apply(field, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Deleting a field deletes every employee's value for it.</summary>
    [HttpDelete("api/setup/custom-fields/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var field = await db.CustomFields.FirstOrDefaultAsync(f => f.Id == id) ?? throw new NotFoundException("Custom field");
        db.CustomFields.Remove(field);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("api/setup/custom-fields/order"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Reorder(List<Guid> ids)
    {
        var fields = await db.CustomFields.ToListAsync();
        foreach (var f in fields)
            f.Order = ids.IndexOf(f.Id) is var i and >= 0 ? i + 1 : 1000 + f.Order;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>The signed-in employee's fields that HR made visible to them.</summary>
    [HttpGet("api/employees/me/custom-fields")]
    public Task<List<CustomValueDto>> Mine() =>
        ValuesAsync(me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record."), onlyVisible: true);

    [HttpGet("api/employees/{id:guid}/custom-fields"), Authorize(Policy = Policies.Approver)]
    public async Task<List<CustomValueDto>> Values(Guid id)
    {
        if (me.Role == UserRole.Manager && !await db.Employees.AnyAsync(e => e.Id == id && (e.ManagerId == me.EmployeeId || e.Id == me.EmployeeId)))
            throw new ForbiddenException("You can only view your own team.");
        return await ValuesAsync(id, onlyVisible: false);
    }

    [HttpPut("api/employees/{id:guid}/custom-fields"), Authorize(Policy = Policies.Hr)]
    public async Task<List<CustomValueDto>> SaveValues(Guid id, SaveCustomValuesRequest req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == id))
            throw new NotFoundException("Employee");
        var fields = await db.CustomFields.Where(f => f.IsActive).ToDictionaryAsync(f => f.Id);
        var existing = await db.CustomFieldValues.Where(v => v.EmployeeId == id).ToDictionaryAsync(v => v.FieldId);
        foreach (var (fieldId, raw) in req.Values)
        {
            if (!fields.TryGetValue(fieldId, out var field))
                throw new NotFoundException("Custom field");
            var value = Normalize(field, raw);
            existing.TryGetValue(fieldId, out var row);
            if (value is null)
            {
                if (row is not null)
                    db.CustomFieldValues.Remove(row);
            }
            else if (row is null)
                db.CustomFieldValues.Add(new CustomFieldValue { EmployeeId = id, FieldId = fieldId, Value = value });
            else
                row.Value = value;
        }
        var missing = fields.Values.Where(f => f.IsRequired)
            .Where(f => req.Values.TryGetValue(f.Id, out var v) ? string.IsNullOrWhiteSpace(v) : !existing.ContainsKey(f.Id))
            .Select(f => f.Label).ToList();
        if (missing.Count > 0)
            throw new DomainException($"Fill in {string.Join(", ", missing)}.");
        await db.SaveChangesAsync();
        return await ValuesAsync(id, onlyVisible: false);
    }

    private async Task<List<CustomValueDto>> ValuesAsync(Guid employeeId, bool onlyVisible)
    {
        var fields = await db.CustomFields.Where(f => f.IsActive && (!onlyVisible || f.VisibleToEmployee)).OrderBy(f => f.Order).ThenBy(f => f.Label).ToListAsync();
        var values = await db.CustomFieldValues.Where(v => v.EmployeeId == employeeId).ToDictionaryAsync(v => v.FieldId, v => v.Value);
        return fields.Select(f => new CustomValueDto(f.Id, f.Label, f.Type, OptionsOf(f), f.IsRequired, values.GetValueOrDefault(f.Id) ?? "")).ToList();
    }

    private async Task Apply(CustomField field, SaveCustomFieldRequest req)
    {
        var label = req.Label.Trim();
        if (await db.CustomFields.AnyAsync(f => f.Id != field.Id && f.Label == label))
            throw new DomainException($"There is already a field called {label}.");
        if (!Enum.IsDefined(req.Type))
            throw new DomainException("Choose a field type.");
        var options = (req.Options ?? []).Select(o => o?.Trim() ?? "").Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (req.Type == CustomFieldType.Dropdown && options.Count < 2)
            throw new DomainException("A dropdown needs at least two choices.");
        if (options.Any(o => o.Length > 100) || options.Count > 50)
            throw new DomainException("Use up to 50 choices of up to 100 characters.");
        field.Label = label;
        field.Type = req.Type;
        field.Options = req.Type == CustomFieldType.Dropdown ? string.Join('\n', options) : null;
        field.IsRequired = req.IsRequired;
        field.VisibleToEmployee = req.VisibleToEmployee;
        field.IsActive = req.IsActive;
    }

    private static List<string> OptionsOf(CustomField f) => f.Options?.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];

    /// <summary>The value as stored (dates as yyyy-MM-dd, Yes/No), or null to clear it.</summary>
    private static string? Normalize(CustomField field, string? raw)
    {
        var v = raw?.Trim();
        if (string.IsNullOrEmpty(v))
            return null;
        string Bad(string what) => throw new DomainException($"{field.Label}: {what}");
        return field.Type switch
        {
            CustomFieldType.Number => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                ? n.ToString(CultureInfo.InvariantCulture) : Bad("enter a number."),
            CustomFieldType.Date => DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Bad("enter a date."),
            CustomFieldType.Dropdown => OptionsOf(field).FirstOrDefault(o => o.Equals(v, StringComparison.OrdinalIgnoreCase)) ?? Bad("choose one of the options."),
            CustomFieldType.YesNo => v.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Yes"
                : v.Equals("no", StringComparison.OrdinalIgnoreCase) ? "No" : Bad("choose Yes or No."),
            _ => v.Length <= 500 ? v : Bad("keep it under 500 characters."),
        };
    }
}
