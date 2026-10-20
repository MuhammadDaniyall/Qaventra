using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>An employee's exit: resignation or termination through clearance, exit interview and final settlement.</summary>
public class Separation : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public SeparationType Type { get; set; }
    public SeparationStatus Status { get; set; } = SeparationStatus.Requested;

    /// <summary>Date notice was given; the notice period is counted from here.</summary>
    public DateOnly NoticeDate { get; set; }
    public DateOnly ProposedLastDay { get; set; }
    /// <summary>Agreed last working day, set when HR accepts.</summary>
    public DateOnly? LastWorkingDay { get; set; }
    public int NoticePeriodDays { get; set; } = 30;
    public string? Reason { get; set; }

    public Guid? InitiatedByUserId { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }

    public string? ExitReason { get; set; }
    /// <summary>1–5, how the employee rates their time at the company.</summary>
    public int? ExitRating { get; set; }
    public bool? WouldRecommend { get; set; }
    public string? ExitFeedback { get; set; }
    public DateTime? ExitInterviewAtUtc { get; set; }

    public decimal EncashedLeaveDays { get; set; }
    public bool RecoverNoticeShortfall { get; set; }
    public decimal OtherAdditions { get; set; }
    public decimal OtherDeductions { get; set; }
    public string? SettlementNote { get; set; }
    public DateTime? SettlementSavedAtUtc { get; set; }

    /// <summary>Amounts frozen when the separation is completed.</summary>
    public decimal LeaveEncashmentAmount { get; set; }
    public decimal NoticeRecoveryAmount { get; set; }
    public decimal ExpenseReimbursement { get; set; }
    public decimal NetSettlement { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public List<ClearanceItem> Clearance { get; set; } = [];

    public bool IsOpen => Status is SeparationStatus.Requested or SeparationStatus.InProgress;
}

public class ClearanceItem : BaseEntity
{
    public Guid SeparationId { get; set; }
    public string Department { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsCleared { get; set; }
    public string? ClearedByName { get; set; }
    public DateTime? ClearedAtUtc { get; set; }
    public string? Note { get; set; }
}
