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
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
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

public enum ExpenseStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    /// <summary>Reimbursed through a finalized payroll or a final settlement.</summary>
    Paid = 5,
}
