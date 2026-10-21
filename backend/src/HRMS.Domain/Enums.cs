namespace HRMS.Domain;

public enum UserRole
{
    /// <summary>Platform owner: manages all companies and reads every audit log. Belongs to no company.</summary>
    SuperAdmin = 0,
    CompanyAdmin = 1,
    HrManager = 2,
    Manager = 3,
    Employee = 4,
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3,
}

public enum ShiftAssignmentSource
{
    Roster = 1,
    /// <summary>Written by an approved shift change request.</summary>
    Request = 2,
    /// <summary>Written by an approved shift swap.</summary>
    Swap = 3,
}

public enum ShiftRequestKind
{
    Change = 1,
    Swap = 2,
}

public enum ShiftRequestStatus
{
    /// <summary>Swap only: the colleague has not agreed yet.</summary>
    AwaitingColleague = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    /// <summary>Swap only: the colleague said no.</summary>
    Declined = 6,
}

public enum MaritalStatus
{
    Single = 1,
    Married = 2,
    Divorced = 3,
    Widowed = 4,
}

public enum JobChangeKind
{
    Transfer = 1,
    Promotion = 2,
    Demotion = 3,
    /// <summary>New title at the same level.</summary>
    Redesignation = 4,
    ManagerChange = 5,
    Rehire = 6,
    /// <summary>Recorded automatically when HR edits the department, designation or manager on the profile.</summary>
    ProfileEdit = 7,
}

public enum EmploymentStatus
{
    Probation = 1,
    Active = 2,
    Resigned = 3,
    Terminated = 4,
}

public enum AttendanceStatus
{
    Present = 1,
    Late = 2,
    HalfDay = 3,
    Absent = 4,
    OnLeave = 5,
}

public enum AttendanceSource
{
    Web = 1,
    Mobile = 2,
    Biometric = 3,
    Manual = 4,
    /// <summary>From an approved attendance request (missed punch, work from home, ...).</summary>
    Request = 5,
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

public enum LeaveDuration
{
    FullDay = 1,
    /// <summary>Half of one working day.</summary>
    HalfDay = 2,
    /// <summary>A few hours of one working day.</summary>
    ShortLeave = 3,
}

public enum HalfDayPart
{
    FirstHalf = 1,
    SecondHalf = 2,
}

public enum LeaveAccrual
{
    /// <summary>The whole year's quota is available from January (prorated for joiners).</summary>
    Upfront = 1,
    /// <summary>One twelfth of the quota is earned each month.</summary>
    Monthly = 2,
}

public enum SalaryComponentKind
{
    Earning = 1,
    Deduction = 2,
}

public enum BillingCycle
{
    Monthly = 1,
    Quarterly = 3,
    HalfYearly = 6,
    Yearly = 12,
}

public enum TenantAccess
{
    Allowed = 0,
    Inactive = 1,
    Expired = 2,
}

public enum AuditCategory
{
    /// <summary>Sign-in, sign-out and failed sign-in attempts.</summary>
    Auth = 1,
    /// <summary>An API request: who called what, from where, with which result.</summary>
    Request = 2,
    /// <summary>A row added, changed or deleted, with old and new values.</summary>
    Data = 3,
}

public enum PayrollRunStatus
{
    Draft = 1,
    Finalized = 2,
}

public enum LetterCategory
{
    Offer = 1,
    Appointment = 2,
    Confirmation = 3,
    Increment = 4,
    Experience = 5,
    Relieving = 6,
    SalaryCertificate = 7,
    Warning = 8,
    Other = 9,
}

public enum SeparationType
{
    Resignation = 1,
    Termination = 2,
    Retirement = 3,
    ContractEnd = 4,
}

public enum SeparationStatus
{
    /// <summary>The employee resigned; waiting for HR.</summary>
    Requested = 1,
    /// <summary>Accepted: clearance, exit interview and settlement are under way.</summary>
    InProgress = 2,
    Completed = 3,
    Rejected = 4,
    Withdrawn = 5,
}

public enum ExpenseCategory
{
    Travel = 1,
    Fuel = 2,
    Meals = 3,
    Accommodation = 4,
    Medical = 5,
    Mobile = 6,
    OfficeSupplies = 7,
    Training = 8,
    Other = 9,
}

/// <summary>Requests that go through an approval chain. Add new modules here.</summary>
public enum ApprovalRequestType
{
    Leave = 1,
    Expense = 2,
    Attendance = 3,
    CompOff = 4,
    PayRequest = 5,
    Loan = 6,
    ProfileChange = 7,
    ShiftChange = 8,
}

public enum PayRequestKind
{
    Allowance = 1,
    Bonus = 2,
    Overtime = 3,
    Deduction = 4,
    /// <summary>Paid out now, recovered in full from the chosen month's salary.</summary>
    AdvanceSalary = 5,
}

/// <summary>Status of a simple request that goes through an approval chain.</summary>
public enum RequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

public enum AttendanceRequestKind
{
    /// <summary>Forgot to check in or out.</summary>
    MissedPunch = 1,
    WorkFromHome = 2,
    /// <summary>Client visit, field work, training outside the office.</summary>
    OfficialDuty = 3,
    /// <summary>The recorded times are wrong.</summary>
    TimeCorrection = 4,
}

public enum NotificationKind
{
    /// <summary>Something waits for the user's approval.</summary>
    ApprovalNeeded = 1,
    /// <summary>The user's own request was approved or rejected.</summary>
    RequestDecided = 2,
    Separation = 3,
    Letter = 4,
    Payslip = 5,
}

/// <summary>What an attachment belongs to. Expense receipts are stored with their claim items instead.</summary>
public enum AttachmentOwner
{
    Leave = 1,
    Separation = 2,
    /// <summary>The employee's own document folder (owner id = employee id).</summary>
    EmployeeDocument = 3,
}

public enum DocumentCategory
{
    Cnic = 1,
    Passport = 2,
    Degree = 3,
    ExperienceLetter = 4,
    Contract = 5,
    Medical = 6,
    Photo = 7,
    Other = 8,
}

public enum ApproverKind
{
    LineManager = 1,
    /// <summary>The line manager's own manager.</summary>
    SecondLevelManager = 2,
    /// <summary>Anyone with HR rights (HR manager or company admin).</summary>
    Hr = 3,
    SpecificEmployee = 4,
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

public enum ApprovalStepStatus
{
    /// <summary>Later in the chain; not this approver's turn yet.</summary>
    Waiting = 1,
    /// <summary>The step that needs a decision now.</summary>
    Current = 2,
    Approved = 3,
    Rejected = 4,
    /// <summary>Never reached because an earlier step rejected or the request was cancelled.</summary>
    Skipped = 5,
}

public enum ExpenseStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    /// <summary>Reimbursed through a finalized payroll or a final settlement.</summary>
    Paid = 5,
}
