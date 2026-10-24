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

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4,
}

public enum BudgetStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Closed = 5,
}

public enum SpaceKind
{
    Desk = 1,
    MeetingRoom = 2,
    Cabin = 3,
}

public enum BookingStatus
{
    Booked = 1,
    CheckedIn = 2,
    Cancelled = 3,
    /// <summary>Freed because nobody checked in.</summary>
    Released = 4,
}

public enum AlertKind
{
    Birthday = 1,
    WorkAnniversary = 2,
    ProbationEnd = 3,
    DocumentExpiry = 4,
    NewJoiner = 5,
    LeaveStarting = 6,
    /// <summary>Working today, not on leave, and no check-in by the send hour.</summary>
    NotCheckedIn = 7,
}

public enum ReportKind
{
    Headcount = 1,
    DailyAttendance = 2,
    OnLeave = 3,
    PendingApprovals = 4,
    ExpiringDocuments = 5,
    JoinersAndLeavers = 6,
    ProbationDue = 7,
}

public enum ReportFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
}

public enum TrainingMode
{
    Classroom = 1,
    Online = 2,
    OnTheJob = 3,
    External = 4,
}

public enum TrainingSessionStatus
{
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum EnrollmentStatus
{
    Enrolled = 1,
    Attended = 2,
    /// <summary>Attended and passed (or no assessment).</summary>
    Completed = 3,
    NoShow = 4,
    Failed = 5,
    Withdrawn = 6,
}

public enum TripType
{
    OneWay = 1,
    Return = 2,
    MultiCity = 3,
}

public enum TravelMode
{
    Air = 1,
    Train = 2,
    Bus = 3,
    Car = 4,
    CompanyVehicle = 5,
    Other = 6,
}

public enum TravelStatus
{
    Pending = 1,
    Approved = 2,
    /// <summary>Tickets and hotel booked by the travel desk.</summary>
    Booked = 3,
    Rejected = 4,
    Cancelled = 5,
}

/// <summary>Where the after-trip expense settlement is.</summary>
public enum SettlementStatus
{
    None = 1,
    Submitted = 2,
    /// <summary>Sent back to the traveller to fix.</summary>
    Returned = 3,
    Settled = 4,
}

public enum EmploymentType
{
    FullTime = 1,
    PartTime = 2,
    Contract = 3,
    Internship = 4,
}

public enum JobPostStatus
{
    Draft = 1,
    Open = 2,
    /// <summary>Not taking applications; the pipeline goes on.</summary>
    Closed = 3,
    Filled = 4,
}

public enum CandidateSource
{
    CareersPage = 1,
    Referral = 2,
    WalkIn = 3,
    Advertisement = 4,
    Agency = 5,
    Manual = 6,
}

/// <summary>A candidate's place in a job's pipeline.</summary>
public enum ApplicationStage
{
    Applied = 1,
    Shortlisted = 2,
    Test = 3,
    Interview = 4,
    Offer = 5,
    Hired = 6,
    Rejected = 7,
    Withdrawn = 8,
    OnHold = 9,
}

public enum OfferStatus
{
    Draft = 1,
    Sent = 2,
    Accepted = 3,
    Declined = 4,
}

public enum InterviewMode
{
    Onsite = 1,
    Online = 2,
    Phone = 3,
}

public enum InterviewStatus
{
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4,
}

public enum HireRecommendation
{
    StrongNo = 1,
    No = 2,
    Yes = 3,
    StrongYes = 4,
}

public enum ReviewCycleStatus
{
    Draft = 1,
    Active = 2,
    Closed = 3,
}

/// <summary>Where one person's review is. Each stage is someone's turn.</summary>
public enum ReviewStage
{
    /// <summary>The employee writes goals; the manager approves them.</summary>
    Goals = 1,
    GoalsSubmitted = 2,
    SelfReview = 3,
    ManagerReview = 4,
    /// <summary>HR checks ratings across people before releasing them.</summary>
    Calibration = 5,
    /// <summary>Released to the employee to accept or disagree.</summary>
    SignOff = 6,
    Done = 7,
}

public enum ReviewSectionKind
{
    /// <summary>Each person's own goals, written at the start of the cycle.</summary>
    Goals = 1,
    /// <summary>The same items for everyone, listed in the template.</summary>
    Competencies = 2,
}

public enum OnboardingTaskCategory
{
    Paperwork = 1,
    ItSetup = 2,
    Workspace = 3,
    Training = 4,
    Introduction = 5,
    Other = 6,
}

/// <summary>Who an onboarding task falls to; resolved to a person when onboarding starts.</summary>
public enum OnboardingAssignee
{
    NewJoiner = 1,
    LineManager = 2,
    Buddy = 3,
    Hr = 4,
    SpecificEmployee = 5,
}

public enum OnboardingStatus
{
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum AssetCondition
{
    New = 1,
    Good = 2,
    Fair = 3,
    Damaged = 4,
}

public enum AssetStatus
{
    Available = 1,
    Assigned = 2,
    InRepair = 3,
    Retired = 4,
    Lost = 5,
}

public enum AssetRequestKind
{
    New = 1,
    Return = 2,
}

public enum AssetRequestStatus
{
    Pending = 1,
    /// <summary>Approved; waiting for a handler to hand it over or take it back.</summary>
    Approved = 2,
    Fulfilled = 3,
    Rejected = 4,
    Cancelled = 5,
}

public enum TicketAssignment
{
    /// <summary>Agents take tickets or HR assigns them.</summary>
    Manual = 1,
    /// <summary>Each new ticket goes to the next agent in turn.</summary>
    RoundRobin = 2,
    /// <summary>Each new ticket goes to the agent with the fewest open tickets.</summary>
    LeastBusy = 3,
}

public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    /// <summary>The agent asked the employee something.</summary>
    WaitingOnEmployee = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6,
}

public enum AnnouncementKind
{
    News = 1,
    Policy = 2,
    Event = 3,
}

public enum PollResultsVisibility
{
    /// <summary>Employees see the results once they have voted.</summary>
    AfterVoting = 1,
    /// <summary>Employees see the results when the poll closes.</summary>
    AfterClose = 2,
    HrOnly = 3,
}

public enum CustomFieldType
{
    Text = 1,
    Number = 2,
    Date = 3,
    Dropdown = 4,
    YesNo = 5,
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
    ShortDay = 6,
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
    /// <summary>Sent to the company admin for approval (when the company requires it).</summary>
    Submitted = 3,
    /// <summary>Approved by the company admin; HR can now finalize.</summary>
    Approved = 4,
}

public enum SalaryRevisionKind
{
    Increment = 1,
    Promotion = 2,
    Decrement = 3,
    Correction = 4,
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
    Asset = 9,
    JobRequest = 10,
    Travel = 11,
    Training = 12,
    Exemption = 13,
    OffDay = 14,
    Timesheet = 15,
    FundWithdrawal = 16,
    ExpenseAdvance = 17,
    FinalSettlement = 18,
    Letter = 19,
}

public enum PayRequestKind
{
    Allowance = 1,
    Bonus = 2,
    Overtime = 3,
    Deduction = 4,
    /// <summary>Paid out now, recovered in full from the chosen month's salary.</summary>
    AdvanceSalary = 5,
    Commission = 6,
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
    Announcement = 6,
    Poll = 7,
    Ticket = 8,
    Asset = 9,
    Onboarding = 10,
    Performance = 11,
    Recruitment = 12,
    Travel = 13,
    Training = 14,
    Alert = 15,
    Report = 16,
    /// <summary>A colleague's birthday or work anniversary wish.</summary>
    Celebration = 17,
}

/// <summary>What an attachment belongs to. Expense receipts are stored with their claim items instead.</summary>
public enum AttachmentOwner
{
    Leave = 1,
    Separation = 2,
    /// <summary>The employee's own document folder (owner id = employee id).</summary>
    EmployeeDocument = 3,
    Announcement = 4,
    Ticket = 5,
    Candidate = 6,
    Travel = 7,
    TrainingRequest = 8,
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
    /// <summary>The head of the requester's department (skipped when none is set).</summary>
    DepartmentHead = 5,
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

public enum LookupKind
{
    Division = 1,
    Grade = 2,
    CostCenter = 3,
    Region = 4,
    EmployeeGroup = 5,
    SubDepartment = 6,
    ExitType = 7,
}

public enum HolidayKind
{
    /// <summary>Gazetted / public holiday.</summary>
    Public = 1,
    /// <summary>Company or special holiday.</summary>
    Special = 2,
}

public enum PenaltyFlag
{
    Late = 1,
    EarlyLeave = 2,
    HalfDay = 3,
    ShortDay = 4,
    Absent = 5,
    MissingCheckOut = 6,
}

public enum PenaltyAction
{
    DeductPay = 1,
    DeductLeave = 2,
}

public enum ProjectStatus
{
    Active = 1,
    OnHold = 2,
    Closed = 3,
}

public enum TimesheetStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
}

public enum SuggestionStatus
{
    New = 1,
    UnderReview = 2,
    Accepted = 3,
    Implemented = 4,
    Declined = 5,
}

public enum FundKind
{
    ProvidentFund = 1,
    Gratuity = 2,
}

public enum FundEntryKind
{
    Opening = 1,
    Contribution = 2,
    Profit = 3,
    Withdrawal = 4,
    /// <summary>Gratuity paid out at exit or other correction.</summary>
    Adjustment = 5,
}

public enum TaxAdjustmentKind
{
    /// <summary>Taxable salary from a previous employer this tax year.</summary>
    PreviousIncome = 1,
    /// <summary>Tax already deducted by a previous employer this tax year.</summary>
    PreviousTax = 2,
    /// <summary>Tax credit (donations, pension fund, etc.) reducing the year's tax.</summary>
    TaxCredit = 3,
    /// <summary>Other taxable income to include in the year.</summary>
    OtherIncome = 4,
}

public enum PaymentMethod
{
    BankTransfer = 1,
    Cash = 2,
    Cheque = 3,
}

public enum ExpenseCalculation
{
    /// <summary>The person enters the amount.</summary>
    Amount = 1,
    /// <summary>Rate × units entered (e.g. nights, meals, km driven).</summary>
    PerUnit = 2,
    /// <summary>Rate × distance between two cities from the distance list.</summary>
    Distance = 3,
}

public enum ExitQuestionKind
{
    /// <summary>1 to 5.</summary>
    Rating = 1,
    YesNo = 2,
    Text = 3,
}

public enum LetterStatus
{
    Issued = 1,
    /// <summary>Waiting for the letter approval chain.</summary>
    Pending = 2,
    Rejected = 3,
}

/// <summary>Events that issue a letter automatically from a template.</summary>
public enum LetterAutoUse
{
    /// <summary>Offer to a candidate.</summary>
    Offer = 1,
    /// <summary>A candidate is hired (appointment letter).</summary>
    Hire = 2,
    /// <summary>Transfer, promotion or other job change.</summary>
    JobChange = 3,
    /// <summary>Salary revision.</summary>
    SalaryRevision = 4,
    /// <summary>Separation completed (experience / relieving letter).</summary>
    Exit = 5,
    /// <summary>Confirmed after probation.</summary>
    Confirmation = 6,
}
