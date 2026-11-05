using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>The jobs an AI robot can do for a company. Each company gets one robot of each kind, switched off.</summary>
public enum RobotKind
{
    PayrollChecker = 1,
    AttendanceWatcher = 2,
    CvScreener = 3,
    HelpdeskResponder = 4,
    PeopleRisk = 5,
    OnboardingCoach = 6,
    LeavePlanner = 7,
    DocumentWatcher = 8,
}

/// <summary>How far a robot may go on its own. Robots never approve requests, change pay or release payroll at any level.</summary>
public enum RobotAutonomy
{
    /// <summary>Looks and reports. Changes nothing, proposes nothing.</summary>
    Advisory = 1,
    /// <summary>Also prepares proposals; a person approves each one before anything happens.</summary>
    Supervised = 2,
    /// <summary>Sends low-risk reminders on its own; everything else still waits for a person.</summary>
    Routine = 3,
}

public enum RobotRunStatus
{
    Running = 1,
    Done = 2,
    Failed = 3,
}

public enum RobotProposalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Failed = 4,
    /// <summary>Done by the robot itself (a low-risk reminder at the Routine level).</summary>
    AutoDone = 5,
    /// <summary>No longer needed (the ticket was answered, the candidate moved on…).</summary>
    Expired = 6,
}

/// <summary>An AI robot employee of the company: what it does, how far it may go, who owns it and its monthly budget.</summary>
public class AiRobot : TenantEntity
{
    public RobotKind Kind { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Robots start switched off and run on their schedule only when switched on.</summary>
    public bool Enabled { get; set; }
    public RobotAutonomy Autonomy { get; set; } = RobotAutonomy.Advisory;
    /// <summary>The person answerable for the robot; they hear about its findings.</summary>
    public Guid? OwnerUserId { get; set; }
    /// <summary>Local hour (0-23) of the daily run.</summary>
    public int RunHour { get; set; } = 9;
    /// <summary>Runs allowed a month (scheduled and manual together).</summary>
    public int RunsPerMonth { get; set; } = 40;
    public DateTime? LastRunAtUtc { get; set; }
    public string? LastSummary { get; set; }
}

/// <summary>One piece of work by a robot: every step it took and what it found, for the audit trail.</summary>
public class AiRobotRun : TenantEntity
{
    public Guid RobotId { get; set; }
    public AiRobot? Robot { get; set; }
    public bool Manual { get; set; }
    public string? StartedByName { get; set; }
    public RobotRunStatus Status { get; set; } = RobotRunStatus.Running;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string? Summary { get; set; }
    /// <summary>JSON list of the steps taken, in order.</summary>
    public string StepsJson { get; set; } = "[]";
    /// <summary>JSON list of findings: title, detail, level (good, watch, act), link.</summary>
    public string FindingsJson { get; set; } = "[]";
    public int Proposals { get; set; }
    /// <summary>The AI wrote the summary (false: the robot's own plain summary, when the AI was busy).</summary>
    public bool UsedAi { get; set; }
    public string? Error { get; set; }
}

/// <summary>A change a robot wants to make, waiting for a person (or done on its own when it is a low-risk reminder).</summary>
public class AiRobotProposal : TenantEntity
{
    public Guid RobotId { get; set; }
    public AiRobot? Robot { get; set; }
    public Guid RunId { get; set; }
    /// <summary>notify_employee, helpdesk_reply, shortlist or remind_task.</summary>
    public string Action { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    /// <summary>The record the proposal is about, so the same one is not proposed twice.</summary>
    public string SubjectKey { get; set; } = "";
    public string? Link { get; set; }
    public RobotProposalStatus Status { get; set; } = RobotProposalStatus.Pending;
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? Note { get; set; }
}
