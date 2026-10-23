using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A reminder the system sends by itself, e.g. birthdays or documents about to expire.</summary>
public class ScheduledAlert : TenantEntity
{
    public string Name { get; set; } = "";
    public AlertKind Kind { get; set; }
    /// <summary>Send this many days before the event (0 = on the day). Ignored for "not checked in".</summary>
    public int DaysBefore { get; set; }
    /// <summary>Probation end only: probation length in months.</summary>
    public int ProbationMonths { get; set; } = 3;
    /// <summary>Company-local hour of the day to send.</summary>
    public int SendHour { get; set; } = 9;
    public bool ToEmployee { get; set; }
    public bool ToManager { get; set; }
    public bool ToHr { get; set; }
    /// <summary>Everyone in the company (e.g. birthday wishes).</summary>
    public bool ToEveryone { get; set; }
    /// <summary>Comma-separated employee ids who also get it.</summary>
    public string ExtraRecipientIds { get; set; } = "";
    /// <summary>Only people in these departments; empty means all.</summary>
    public string DepartmentIds { get; set; } = "";
    /// <summary>With placeholders such as {name}, {date}, {years}, {document}, {department}, {days}.</summary>
    public string TitleTemplate { get; set; } = "";
    public string? BodyTemplate { get; set; }
    public bool SendEmail { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateOnly? LastRunOn { get; set; }
}

/// <summary>A report emailed on a schedule as a table with an Excel copy.</summary>
public class ScheduledReport : TenantEntity
{
    public string Name { get; set; } = "";
    public ReportKind Kind { get; set; }
    public ReportFrequency Frequency { get; set; } = ReportFrequency.Weekly;
    /// <summary>Weekly: 0 = Sunday … 6 = Saturday. Monthly: day 1–28.</summary>
    public int Day { get; set; } = 1;
    public int SendHour { get; set; } = 9;
    public string RecipientIds { get; set; } = "";
    /// <summary>Comma-separated addresses outside the system.</summary>
    public string ExtraEmails { get; set; } = "";
    public string DepartmentIds { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime? LastSentAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
}

/// <summary>One run of an alert or a report, for the history list.</summary>
public class ScheduledRun : TenantEntity
{
    public Guid? AlertId { get; set; }
    public Guid? ReportId { get; set; }
    public string Name { get; set; } = "";
    public int Recipients { get; set; }
    public int Items { get; set; }
    public string? Note { get; set; }
    public bool Manual { get; set; }
}

/// <summary>How the company's printed reports look.</summary>
public class ReportSettings : TenantEntity
{
    /// <summary>Signature line titles, one per line, up to five.</summary>
    public string Signatures { get; set; } = "";
}
