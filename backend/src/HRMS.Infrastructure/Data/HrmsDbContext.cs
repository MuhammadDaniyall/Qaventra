using System.Linq.Expressions;
using System.Text.Json;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Data;

public class HrmsDbContext(DbContextOptions<HrmsDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    private Guid? _tenantOverride;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<OfficeLocation> OfficeLocations => Set<OfficeLocation>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();
    public DbSet<TaxYear> TaxYears => Set<TaxYear>();
    public DbSet<EmployeeSalary> EmployeeSalaries => Set<EmployeeSalary>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TenantPayment> TenantPayments => Set<TenantPayment>();
    public DbSet<LetterTemplate> LetterTemplates => Set<LetterTemplate>();
    public DbSet<IssuedLetter> IssuedLetters => Set<IssuedLetter>();
    public DbSet<Separation> Separations => Set<Separation>();
    public DbSet<ClearanceItem> ClearanceItems => Set<ClearanceItem>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<ExpenseReceipt> ExpenseReceipts => Set<ExpenseReceipt>();
    public DbSet<ApprovalFlow> ApprovalFlows => Set<ApprovalFlow>();
    public DbSet<ApprovalFlowStep> ApprovalFlowSteps => Set<ApprovalFlowStep>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AttachmentContent> AttachmentContents => Set<AttachmentContent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<BiometricDevice> BiometricDevices => Set<BiometricDevice>();
    public DbSet<AttendanceBreak> AttendanceBreaks => Set<AttendanceBreak>();
    public DbSet<CelebrationWish> CelebrationWishes => Set<CelebrationWish>();
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<Kpi> Kpis => Set<Kpi>();
    public DbSet<ReviewFeedback> ReviewFeedback => Set<ReviewFeedback>();
    public DbSet<ReviewCheckIn> ReviewCheckIns => Set<ReviewCheckIn>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<PayrollGlMapping> PayrollGlMappings => Set<PayrollGlMapping>();
    public DbSet<PushKeys> PushKeys => Set<PushKeys>();
    public DbSet<AttendanceRequest> AttendanceRequests => Set<AttendanceRequest>();
    public DbSet<CompOffRequest> CompOffRequests => Set<CompOffRequest>();
    public DbSet<PayRequest> PayRequests => Set<PayRequest>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<OrgLookup> OrgLookups => Set<OrgLookup>();
    public DbSet<OffDayRequest> OffDayRequests => Set<OffDayRequest>();
    public DbSet<PenaltyRule> PenaltyRules => Set<PenaltyRule>();
    public DbSet<PenaltyCharge> PenaltyCharges => Set<PenaltyCharge>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AccessTemplate> AccessTemplates => Set<AccessTemplate>();
    public DbSet<FundEntry> FundEntries => Set<FundEntry>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<ClearanceTemplate> ClearanceTemplates => Set<ClearanceTemplate>();
    public DbSet<CandidateLetter> CandidateLetters => Set<CandidateLetter>();
    public DbSet<InterviewTemplate> InterviewTemplates => Set<InterviewTemplate>();
    public DbSet<InterviewQuestion> InterviewQuestions => Set<InterviewQuestion>();
    public DbSet<ClearanceTemplateItem> ClearanceTemplateItems => Set<ClearanceTemplateItem>();
    public DbSet<ExitInterviewTemplate> ExitInterviewTemplates => Set<ExitInterviewTemplate>();
    public DbSet<ExitQuestion> ExitQuestions => Set<ExitQuestion>();
    public DbSet<ExpenseTypeRate> ExpenseTypeRates => Set<ExpenseTypeRate>();
    public DbSet<DistanceRoute> DistanceRoutes => Set<DistanceRoute>();
    public DbSet<ExpenseAdvance> ExpenseAdvances => Set<ExpenseAdvance>();
    public DbSet<FundWithdrawal> FundWithdrawals => Set<FundWithdrawal>();
    public DbSet<TaxAdjustment> TaxAdjustments => Set<TaxAdjustment>();
    public DbSet<SalarySplit> SalarySplits => Set<SalarySplit>();
    public DbSet<Suggestion> Suggestions => Set<Suggestion>();
    public DbSet<SuggestionCategory> SuggestionCategories => Set<SuggestionCategory>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<TimesheetLine> TimesheetLines => Set<TimesheetLine>();
    public DbSet<Dependant> Dependants => Set<Dependant>();
    public DbSet<Qualification> Qualifications => Set<Qualification>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<ProfileChangeRequest> ProfileChangeRequests => Set<ProfileChangeRequest>();
    public DbSet<JobChange> JobChanges => Set<JobChange>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftRequest> ShiftRequests => Set<ShiftRequest>();
    public DbSet<AttendancePeriod> AttendancePeriods => Set<AttendancePeriod>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<HelpdeskSettings> HelpdeskSettings => Set<HelpdeskSettings>();
    public DbSet<AssetType> AssetTypes => Set<AssetType>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AssetRequest> AssetRequests => Set<AssetRequest>();
    public DbSet<AssetKit> AssetKits => Set<AssetKit>();
    public DbSet<OnboardingTemplate> OnboardingTemplates => Set<OnboardingTemplate>();
    public DbSet<OnboardingTemplateTask> OnboardingTemplateTasks => Set<OnboardingTemplateTask>();
    public DbSet<Onboarding> Onboardings => Set<Onboarding>();
    public DbSet<OnboardingTask> OnboardingTasks => Set<OnboardingTask>();
    public DbSet<PerformanceSettings> PerformanceSettings => Set<PerformanceSettings>();
    public DbSet<PerformanceTemplate> PerformanceTemplates => Set<PerformanceTemplate>();
    public DbSet<ReviewCycle> ReviewCycles => Set<ReviewCycle>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<ReviewItem> ReviewItems => Set<ReviewItem>();
    public DbSet<JobRequest> JobRequests => Set<JobRequest>();
    public DbSet<TravelSettings> TravelSettings => Set<TravelSettings>();
    public DbSet<TrainingCategory> TrainingCategories => Set<TrainingCategory>();
    public DbSet<ScheduledAlert> ScheduledAlerts => Set<ScheduledAlert>();
    public DbSet<DeskSettings> DeskSettings => Set<DeskSettings>();
    public DbSet<PieceCategory> PieceCategories => Set<PieceCategory>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<BudgetPosition> BudgetPositions => Set<BudgetPosition>();
    public DbSet<PieceItem> PieceItems => Set<PieceItem>();
    public DbSet<PieceRate> PieceRates => Set<PieceRate>();
    public DbSet<PieceEntry> PieceEntries => Set<PieceEntry>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Space> Spaces => Set<Space>();
    public DbSet<SpaceBooking> SpaceBookings => Set<SpaceBooking>();
    public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
    public DbSet<ScheduledRun> ScheduledRuns => Set<ScheduledRun>();
    public DbSet<ReportSettings> ReportSettings => Set<ReportSettings>();
    public DbSet<SalaryRevision> SalaryRevisions => Set<SalaryRevision>();
    public DbSet<LeaveReason> LeaveReasons => Set<LeaveReason>();
    public DbSet<AttendanceExemption> AttendanceExemptions => Set<AttendanceExemption>();
    public DbSet<AttendanceSettings> AttendanceSettings => Set<AttendanceSettings>();
    public DbSet<RecurringPayItem> RecurringPayItems => Set<RecurringPayItem>();
    public DbSet<SalaryHold> SalaryHolds => Set<SalaryHold>();
    public DbSet<TrainingProgram> TrainingPrograms => Set<TrainingProgram>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<TrainingEnrollment> TrainingEnrollments => Set<TrainingEnrollment>();
    public DbSet<TrainingRequest> TrainingRequests => Set<TrainingRequest>();
    public DbSet<TravelReason> TravelReasons => Set<TravelReason>();
    public DbSet<TravelRequest> TravelRequests => Set<TravelRequest>();
    public DbSet<TravelLeg> TravelLegs => Set<TravelLeg>();
    public DbSet<TravelExpense> TravelExpenses => Set<TravelExpense>();
    public DbSet<JobPost> JobPosts => Set<JobPost>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<ApplicationEvent> ApplicationEvents => Set<ApplicationEvent>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<InterviewFeedback> InterviewFeedback => Set<InterviewFeedback>();

    /// <summary>Tenant used by query filters and new rows. Empty when nobody is signed in, so tenant data stays hidden.</summary>
    public Guid CurrentTenantId => _tenantOverride ?? currentUser.TenantId ?? Guid.Empty;

    /// <summary>Scope this context to a tenant for flows with no signed-in user, such as company registration.</summary>
    public void UseTenant(Guid tenantId) => _tenantOverride = tenantId;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.TimeZoneId).HasMaxLength(64);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.CareersSlug).HasMaxLength(60);
            e.HasIndex(x => x.CareersSlug).IsUnique().HasFilter("[CareersSlug] IS NOT NULL");
        });

        b.Entity<TenantPayment>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.PaidOn });
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
            e.Property(x => x.Method).HasMaxLength(50);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<Department>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Code).HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.HeadEmployeeId);
        });

        b.Entity<Designation>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(150);
            e.HasIndex(x => new { x.TenantId, x.Title }).IsUnique();
        });

        b.Entity<Employee>(e =>
        {
            e.Property(x => x.EmployeeCode).HasMaxLength(30);
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Cnic).HasMaxLength(15);
            e.Property(x => x.BankName).HasMaxLength(100);
            e.Property(x => x.BankAccount).HasMaxLength(50);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.PersonalEmail).HasMaxLength(256);
            e.Property(x => x.BloodGroup).HasMaxLength(5);
            e.HasIndex(x => new { x.TenantId, x.EmployeeCode }).IsUnique();
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId);
            foreach (var n in new[] { nameof(Employee.FatherName), nameof(Employee.PlaceOfBirth), nameof(Employee.Nationality), nameof(Employee.Religion),
                         nameof(Employee.InsuranceProvider), nameof(Employee.BankAccountTitle), nameof(Employee.VisaType) })
                e.Property(n).HasMaxLength(100);
            foreach (var n in new[] { nameof(Employee.PassportNo), nameof(Employee.VisaNo), nameof(Employee.DrivingLicenseNo), nameof(Employee.Ntn),
                         nameof(Employee.EobiNo), nameof(Employee.SocialSecurityNo), nameof(Employee.InsurancePolicyNo), nameof(Employee.BankBranchCode),
                         nameof(Employee.PunchCode) })
                e.Property(n).HasMaxLength(40);
            e.Property(x => x.ExitReason).HasMaxLength(500);
            e.Property(x => x.AllowedIps).HasMaxLength(500);
            e.Property(x => x.BlacklistReason).HasMaxLength(500);
            foreach (var n in new[] { nameof(Employee.DivisionId), nameof(Employee.SubDepartmentId), nameof(Employee.GradeId), nameof(Employee.CostCenterId),
                         nameof(Employee.RegionId), nameof(Employee.EmployeeGroupId), nameof(Employee.ExitTypeId) })
                e.HasOne<OrgLookup>().WithMany().HasForeignKey(n).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<OffDayRequest>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<InterviewTemplate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.TemplateId);
        });

        b.Entity<InterviewQuestion>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(500);
            e.Property(x => x.Category).HasMaxLength(100);
        });

        b.Entity<CandidateLetter>(e =>
        {
            e.Property(x => x.CandidateName).HasMaxLength(200);
            e.Property(x => x.ReferenceNo).HasMaxLength(40);
            e.Property(x => x.Subject).HasMaxLength(300);
            e.Property(x => x.IssuedByName).HasMaxLength(150);
            e.Property(x => x.Footer).HasMaxLength(500);
            e.HasOne<JobApplication>().WithMany().HasForeignKey(x => x.ApplicationId);
        });

        b.Entity<ClearanceTemplate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TemplateId);
        });

        b.Entity<ClearanceTemplateItem>(e =>
        {
            e.Property(x => x.Department).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(200);
        });

        b.Entity<ExitInterviewTemplate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.TemplateId);
        });

        b.Entity<ExitQuestion>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(500);
            e.Property(x => x.Category).HasMaxLength(100);
        });

        b.Entity<ExpenseType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.GradeIds).HasMaxLength(2000);
            e.HasMany(x => x.GradeRates).WithOne().HasForeignKey(x => x.ExpenseTypeId);
        });

        b.Entity<ExpenseTypeRate>(e => e.HasOne<OrgLookup>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.NoAction));

        b.Entity<DistanceRoute>(e =>
        {
            e.Property(x => x.FromCity).HasMaxLength(100);
            e.Property(x => x.ToCity).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.FromCity, x.ToCity }).IsUnique();
        });

        b.Entity<ExpenseAdvance>(e =>
        {
            e.Property(x => x.Purpose).HasMaxLength(300);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<FundEntry>(e =>
        {
            e.Property(x => x.EmployeeAmount).HasPrecision(18, 2);
            e.Property(x => x.EmployerAmount).HasPrecision(18, 2);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Fund });
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<FundWithdrawal>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<TaxAdjustment>(e =>
        {
            e.Property(x => x.TaxYearCode).HasMaxLength(20);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasIndex(x => new { x.TenantId, x.EmployeeId, x.TaxYearCode });
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<SalarySplit>(e =>
        {
            e.Property(x => x.Percent).HasPrecision(5, 2);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne<OrgLookup>().WithMany().HasForeignKey(x => x.CostCenterId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<AccessTemplate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Modules).HasMaxLength(1000);
        });

        b.Entity<Suggestion>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Details).HasMaxLength(4000);
            e.Property(x => x.Response).HasMaxLength(2000);
            e.Property(x => x.RespondedByName).HasMaxLength(150);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne<SuggestionCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<SuggestionCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.ReviewerIds).HasMaxLength(2000);
        });

        b.Entity<Project>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Client).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.BudgetHours).HasPrecision(10, 2);
            e.HasMany(x => x.Members).WithOne().HasForeignKey(x => x.ProjectId);
            e.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.ProjectId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerEmployeeId);
        });

        b.Entity<ProjectMember>(e =>
        {
            e.Property(x => x.HourlyRate).HasPrecision(18, 2);
            e.HasIndex(x => new { x.ProjectId, x.EmployeeId }).IsUnique();
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<ProjectTask>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.EstimatedHours).HasPrecision(10, 2);
        });

        b.Entity<Timesheet>(e =>
        {
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.EmployeeId, x.WeekStart }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.TimesheetId);
        });

        b.Entity<TimesheetLine>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<PenaltyRule>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Days).HasPrecision(5, 2);
            e.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<PenaltyCharge>(e =>
        {
            e.Property(x => x.RuleName).HasMaxLength(150);
            e.Property(x => x.LeaveDays).HasPrecision(6, 2);
            e.Property(x => x.PayDays).HasPrecision(6, 2);
            e.HasIndex(x => new { x.TenantId, x.Year, x.Month });
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<OrgLookup>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Code).HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.Kind, x.Name });
        });

        b.Entity<Dependant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Relation).HasMaxLength(50);
            e.Property(x => x.Cnic).HasMaxLength(15);
            e.Property(x => x.NomineeShare).HasPrecision(5, 2);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<Qualification>(e =>
        {
            e.Property(x => x.Degree).HasMaxLength(150);
            e.Property(x => x.Institute).HasMaxLength(200);
            e.Property(x => x.Field).HasMaxLength(150);
            e.Property(x => x.Grade).HasMaxLength(30);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<WorkExperience>(e =>
        {
            e.Property(x => x.Organization).HasMaxLength(200);
            e.Property(x => x.Designation).HasMaxLength(150);
            e.Property(x => x.ReasonForLeaving).HasMaxLength(500);
            e.Property(x => x.LastSalary).HasPrecision(18, 2);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<OfficeLocation>().Property(x => x.Name).HasMaxLength(150);
        b.Entity<Shift>().Property(x => x.Name).HasMaxLength(100);

        b.Entity<Holiday>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Religion).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.Date });
        });

        b.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            e.Property(x => x.Note).HasMaxLength(300);
        });

        b.Entity<AttendanceRequest>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.Date });
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
        });

        b.Entity<LeaveType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.AnnualQuota).HasPrecision(6, 2);
            e.Property(x => x.CarryForwardMax).HasPrecision(6, 2);
            e.Property(x => x.DocumentAfterDays).HasPrecision(6, 1);
        });

        b.Entity<LeaveBalance>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
            e.Property(x => x.Entitled).HasPrecision(6, 2);
            e.Property(x => x.Used).HasPrecision(6, 2);
            e.Property(x => x.CarriedForward).HasPrecision(6, 2);
            e.Property(x => x.Adjustment).HasPrecision(6, 2);
            e.Property(x => x.Credited).HasPrecision(6, 2);
            e.Property(x => x.AdjustmentNote).HasMaxLength(300);
        });

        b.Entity<CompOffRequest>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.WorkedOn });
            e.Property(x => x.Days).HasPrecision(4, 2);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
        });

        b.Entity<LeaveRequest>(e =>
        {
            e.Property(x => x.Days).HasPrecision(6, 2);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne<LeaveReason>().WithMany().HasForeignKey(x => x.ReasonId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<PayrollSettings>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.OvertimeMultiplier).HasPrecision(5, 2);
        });

        b.Entity<TaxYear>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.SurchargeRate).HasPrecision(9, 4);
            e.HasMany(x => x.Slabs).WithOne().HasForeignKey(x => x.TaxYearId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TaxSlab>().Property(x => x.Rate).HasPrecision(9, 4);

        b.Entity<EmployeeSalary>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }).IsUnique();
            e.HasMany(x => x.Components).WithOne().HasForeignKey(x => x.EmployeeSalaryId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<SalaryComponent>().Property(x => x.Name).HasMaxLength(100);

        b.Entity<PayrollRun>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Frequency, x.PeriodStart }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Year, x.Month });
            e.Property(x => x.TaxYearCode).HasMaxLength(10);
            e.Property(x => x.ApprovedByName).HasMaxLength(200);
            e.Property(x => x.ApprovalNote).HasMaxLength(1000);
            e.Property(x => x.DisbursedVia).HasMaxLength(100);
            e.HasMany(x => x.Payslips).WithOne(x => x.PayrollRun).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Payslip>(e =>
        {
            e.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique();
            e.Property(x => x.UnpaidDays).HasPrecision(6, 2);
            e.Property(x => x.PaidDays).HasPrecision(6, 2);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PayslipId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PayslipLine>().Property(x => x.Name).HasMaxLength(100);

        b.Entity<LetterTemplate>(e =>
        {
            e.Property(x => x.Footer).HasMaxLength(500);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Subject).HasMaxLength(300);
        });

        b.Entity<IssuedLetter>(e =>
        {
            e.Property(x => x.Footer).HasMaxLength(500);
            e.Property(x => x.RequestReason).HasMaxLength(500);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.ReferenceNo }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.ReferenceNo).HasMaxLength(40);
            e.Property(x => x.Subject).HasMaxLength(300);
            e.Property(x => x.IssuedByName).HasMaxLength(200);
        });

        b.Entity<Separation>(e =>
        {
            e.Property(x => x.ExitInterviewerIds).HasMaxLength(2000);
            e.Property(x => x.ExitInterviewNotes).HasMaxLength(4000);
            e.Property(x => x.SettlementApprovalNote).HasMaxLength(1000);
            e.Property(x => x.SettlementPaidVia).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.Property(x => x.ExitReason).HasMaxLength(100);
            e.Property(x => x.ExitFeedback).HasMaxLength(4000);
            e.Property(x => x.SettlementNote).HasMaxLength(1000);
            e.Property(x => x.EncashedLeaveDays).HasPrecision(6, 2);
            e.HasMany(x => x.Clearance).WithOne().HasForeignKey(x => x.SeparationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClearanceItem>(e =>
        {
            e.Property(x => x.Department).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.ClearedByName).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(500);
        });

        b.Entity<ExpenseClaim>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.PayrollRunId);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.Property(x => x.PaidVia).HasMaxLength(100);
            e.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExpenseItem>(e =>
        {
            e.Property(x => x.FromCity).HasMaxLength(100);
            e.Property(x => x.ToCity).HasMaxLength(100);
            e.Property(x => x.Quantity).HasPrecision(10, 2);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.ReceiptFileName).HasMaxLength(200);
        });

        b.Entity<ExpenseReceipt>(e =>
        {
            e.HasIndex(x => x.ExpenseClaimId);
            e.HasOne<ExpenseClaim>().WithMany().HasForeignKey(x => x.ExpenseClaimId);
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.ContentType).HasMaxLength(100);
        });

        b.Entity<ApprovalFlow>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.RequestType }).IsUnique();
            e.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.ApprovalFlowId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ApprovalFlowStep>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);

        b.Entity<ApprovalRequest>(e =>
        {
            e.Property(x => x.FiledByName).HasMaxLength(150);
            e.HasIndex(x => new { x.RequestType, x.SubjectId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Summary).HasMaxLength(300);
            e.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApprovalStep>(e =>
        {
            e.HasIndex(x => new { x.ApproverEmployeeId, x.Status });
            e.Property(x => x.DecidedByName).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(1000);
        });

        b.Entity<Attachment>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.OwnerType, x.OwnerId });
            e.HasIndex(x => new { x.TenantId, x.ExpiresOn });
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.UploadedByName).HasMaxLength(200);
            e.HasOne<AttachmentContent>().WithOne().HasForeignKey<AttachmentContent>(x => x.Id).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedAtUtc });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(1000);
            e.Property(x => x.Link).HasMaxLength(300);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<PushSubscription>(e =>
        {
            e.HasIndex(x => x.Endpoint).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Endpoint).HasMaxLength(800);
            e.Property(x => x.P256dh).HasMaxLength(200);
            e.Property(x => x.Auth).HasMaxLength(100);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CelebrationWish>(e => e.HasIndex(x => new { x.FromUserId, x.Date }));

        b.Entity<EmailTemplate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Kind }).IsUnique();
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Intro).HasMaxLength(2000);
        });

        b.Entity<EmailLog>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
            e.Property(x => x.To).HasMaxLength(256);
            e.Property(x => x.Subject).HasMaxLength(300);
            e.Property(x => x.Kind).HasMaxLength(50);
            e.Property(x => x.Error).HasMaxLength(500);
        });

        b.Entity<Kpi>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.DefaultTarget).HasMaxLength(200);
            e.Property(x => x.DefaultWeight).HasPrecision(5, 2);
        });

        b.Entity<ReviewFeedback>(e =>
        {
            e.HasIndex(x => new { x.ReviewId, x.FromEmployeeId }).IsUnique();
            e.HasIndex(x => x.FromEmployeeId);
            e.Property(x => x.Comment).HasMaxLength(4000);
            e.HasOne<PerformanceReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ReviewCheckIn>(e =>
        {
            e.HasIndex(x => x.ReviewId);
            e.Property(x => x.Period).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(4000);
            e.HasOne<PerformanceReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApiToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.Property(x => x.TokenPrefix).HasMaxLength(12);
            e.Property(x => x.CreatedByName).HasMaxLength(200);
        });

        b.Entity<GlAccount>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(150);
        });

        b.Entity<PayrollGlMapping>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Line }).IsUnique();
            e.HasOne<GlAccount>().WithMany().HasForeignKey(x => x.GlAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AttendanceBreak>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Date });
            e.HasOne<AttendanceRecord>().WithMany().HasForeignKey(x => x.AttendanceRecordId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BiometricDevice>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.Property(x => x.TokenPrefix).HasMaxLength(12);
        });

        b.Entity<PushKeys>(e =>
        {
            e.Property(x => x.PublicKey).HasMaxLength(200);
            e.Property(x => x.PrivateKey).HasMaxLength(200);
        });

        b.Entity<PayRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status, x.Year, x.Month });
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.PayrollRunId);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.Property(x => x.Hours).HasPrecision(6, 2);
            e.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId);
        });

        b.Entity<Loan>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasMany(x => x.Repayments).WithOne().HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LoanRepayment>(e =>
        {
            e.HasIndex(x => new { x.LoanId, x.PayrollRunId }).IsUnique();
            e.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId);
        });

        b.Entity<EmergencyContact>(e =>
        {
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Relation).HasMaxLength(50);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.AltPhone).HasMaxLength(30);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<ProfileChangeRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
        });

        b.Entity<JobChange>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveDate });
            e.Property(x => x.FromDepartment).HasMaxLength(150);
            e.Property(x => x.ToDepartment).HasMaxLength(150);
            e.Property(x => x.FromDesignation).HasMaxLength(150);
            e.Property(x => x.ToDesignation).HasMaxLength(150);
            e.Property(x => x.FromManager).HasMaxLength(200);
            e.Property(x => x.ToManager).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.RecordedByName).HasMaxLength(200);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<ShiftAssignment>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Date });
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId);
        });

        b.Entity<ShiftRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.SwapWithEmployeeId);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.SwapWithEmployeeId);
        });

        b.Entity<AttendancePeriod>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Year, x.Month }).IsUnique();
            e.Property(x => x.ClosedByName).HasMaxLength(200);
        });

        b.Entity<Announcement>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.PublishFrom });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(8000);
            e.Property(x => x.DepartmentIds).HasMaxLength(2000);
            e.Property(x => x.LinkUrl).HasMaxLength(500);
            e.Property(x => x.CreatedByName).HasMaxLength(200);
        });

        b.Entity<AnnouncementRead>(e =>
        {
            e.HasIndex(x => new { x.AnnouncementId, x.EmployeeId }).IsUnique();
            e.HasOne<Announcement>().WithMany().HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Poll>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ClosesOn });
            e.Property(x => x.Question).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.DepartmentIds).HasMaxLength(2000);
            e.Property(x => x.CreatedByName).HasMaxLength(200);
            e.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PollOption>().Property(x => x.Text).HasMaxLength(200);

        b.Entity<PollVote>(e =>
        {
            e.HasIndex(x => new { x.PollId, x.EmployeeId }).IsUnique();
            e.HasOne<Poll>().WithMany().HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PollOption>().WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<CustomField>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(100);
            e.Property(x => x.Options).HasMaxLength(2000);
        });

        b.Entity<CustomFieldValue>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.FieldId }).IsUnique();
            e.Property(x => x.Value).HasMaxLength(500);
            e.HasOne<CustomField>().WithMany().HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<TicketCategory>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.AgentIds).HasMaxLength(2000);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.LeadEmployeeId);
        });

        b.Entity<TicketType>(e =>
        {
            e.HasIndex(x => new { x.CategoryId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.AgentIds).HasMaxLength(2000);
            e.HasOne<TicketCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<HelpdeskSettings>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.StatusLabels).HasMaxLength(2000);
            e.Property(x => x.PriorityLabels).HasMaxLength(2000);
        });

        b.Entity<Ticket>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.AssigneeEmployeeId);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(8000);
            e.Property(x => x.InvolvedIds).HasMaxLength(2000);
            e.Property(x => x.Feedback).HasMaxLength(1000);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.AssigneeEmployeeId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.RaisedByEmployeeId);
        });

        b.Entity<TicketComment>(e =>
        {
            e.HasIndex(x => x.TicketId);
            e.Property(x => x.AuthorName).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(8000);
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AssetType>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.HandlerIds).HasMaxLength(2000);
        });

        b.Entity<Asset>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Tag }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Tag).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId);
        });

        b.Entity<AssetAssignment>(e =>
        {
            e.HasIndex(x => x.AssetId);
            e.HasIndex(x => new { x.EmployeeId, x.ReturnedOn });
            e.Property(x => x.HandedOverByName).HasMaxLength(200);
            e.Property(x => x.ReceivedByName).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        });

        b.Entity<AssetRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne<AssetType>().WithMany().HasForeignKey(x => x.TypeId);
            e.HasOne<AssetAssignment>().WithMany().HasForeignKey(x => x.AssignmentId);
        });

        b.Entity<AssetKit>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.TypeIds).HasMaxLength(2000);
        });

        b.Entity<OnboardingTemplate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.WelcomeMessage).HasMaxLength(4000);
            e.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AssetKit>().WithMany().HasForeignKey(x => x.AssetKitId);
        });

        b.Entity<OnboardingTemplateTask>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
        });

        b.Entity<Onboarding>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.TemplateName).HasMaxLength(100);
            e.Property(x => x.ReportingPlace).HasMaxLength(300);
            e.Property(x => x.Instructions).HasMaxLength(4000);
            e.Property(x => x.IntroductionJson).HasMaxLength(4000);
            e.Property(x => x.StartedByName).HasMaxLength(200);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.BuddyEmployeeId);
            e.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.OnboardingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OnboardingTask>(e =>
        {
            e.HasIndex(x => x.AssigneeEmployeeId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.CompletedByName).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(1000);
        });

        b.Entity<PerformanceSettings>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.RatingLabelsJson).HasMaxLength(1000);
            e.Property(x => x.BellCurveJson).HasMaxLength(200);
            e.Property(x => x.RaiseSlabsJson).HasMaxLength(1000);
        });

        b.Entity<PerformanceTemplate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        b.Entity<ReviewCycle>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.DepartmentIds).HasMaxLength(2000);
            e.HasOne<PerformanceTemplate>().WithMany().HasForeignKey(x => x.TemplateId);
        });

        b.Entity<PerformanceReview>(e =>
        {
            e.HasIndex(x => new { x.CycleId, x.EmployeeId }).IsUnique();
            e.HasIndex(x => x.ReviewerEmployeeId);
            e.Property(x => x.SelfScore).HasPrecision(5, 2);
            e.Property(x => x.ManagerScore).HasPrecision(5, 2);
            foreach (var name in new[] { nameof(PerformanceReview.SelfComment), nameof(PerformanceReview.ManagerComment), nameof(PerformanceReview.Strengths),
                         nameof(PerformanceReview.Improvements), nameof(PerformanceReview.DevelopmentPlan), nameof(PerformanceReview.CalibrationNote),
                         nameof(PerformanceReview.EmployeeSignOffComment) })
                e.Property(name).HasMaxLength(4000);
            e.HasOne(x => x.Cycle).WithMany().HasForeignKey(x => x.CycleId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReviewerEmployeeId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ReviewItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Target).HasMaxLength(1000);
            e.Property(x => x.Weight).HasPrecision(6, 2);
            e.Property(x => x.SelfComment).HasMaxLength(2000);
            e.Property(x => x.ManagerComment).HasMaxLength(2000);
        });

        b.Entity<ApprovalDelegation>(e =>
        {
            e.HasIndex(x => new { x.ToEmployeeId, x.FromDate, x.ToDate });
            e.HasIndex(x => x.FromEmployeeId);
            e.Property(x => x.RequestTypes).HasMaxLength(200);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.CreatedByName).HasMaxLength(200);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.FromEmployeeId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.ToEmployeeId);
        });

        b.Entity<BudgetPlan>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.DecidedByName).HasMaxLength(200);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasMany(x => x.Positions).WithOne().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BudgetPosition>(e =>
        {
            e.HasIndex(x => new { x.PlanId, x.DepartmentId, x.DesignationId }).IsUnique();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId);
            e.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId);
        });

        b.Entity<PieceCategory>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<PieceItem>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.Rate).HasPrecision(18, 4);
            e.Property(x => x.DailyTarget).HasPrecision(18, 2);
            e.HasOne<PieceCategory>().WithMany().HasForeignKey(x => x.CategoryId);
        });

        b.Entity<PieceRate>(e =>
        {
            e.HasIndex(x => new { x.ItemId, x.EmployeeId }).IsUnique();
            e.Property(x => x.Rate).HasPrecision(18, 4);
            e.HasOne<PieceItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<PieceEntry>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => new { x.EmployeeId, x.Date });
            e.HasIndex(x => x.PayrollRunId);
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.Property(x => x.Rate).HasPrecision(18, 4);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Property(x => x.EnteredByName).HasMaxLength(200);
            e.Property(x => x.DecidedByName).HasMaxLength(200);
            e.Property(x => x.DecisionNote).HasMaxLength(500);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
            e.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId);
        });

        b.Entity<DeskSettings>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.MaxDaysAhead).HasDefaultValue(14);
            e.Property(x => x.ReleaseAfterMinutes).HasDefaultValue(60);
            e.Property(x => x.DayStartsAt).HasDefaultValue(9);
        });

        b.Entity<Floor>(e =>
        {
            e.HasIndex(x => new { x.OfficeLocationId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeLocationId);
        });

        b.Entity<Space>(e =>
        {
            e.HasIndex(x => new { x.FloorId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Features).HasMaxLength(300);
            e.HasOne(x => x.Floor).WithMany().HasForeignKey(x => x.FloorId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.AssignedEmployeeId);
        });

        b.Entity<SpaceBooking>(e =>
        {
            e.HasIndex(x => new { x.SpaceId, x.Date });
            e.HasIndex(x => new { x.EmployeeId, x.Date });
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasOne(x => x.Space).WithMany().HasForeignKey(x => x.SpaceId);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<ScheduledAlert>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.ExtraRecipientIds).HasMaxLength(2000);
            e.Property(x => x.DepartmentIds).HasMaxLength(2000);
            e.Property(x => x.TitleTemplate).HasMaxLength(200);
            e.Property(x => x.BodyTemplate).HasMaxLength(2000);
            e.Property(x => x.SendHour).HasDefaultValue(9);
            e.Property(x => x.ProbationMonths).HasDefaultValue(3);
        });

        b.Entity<ScheduledReport>(e =>
        {
            e.HasIndex(x => x.NextRunAtUtc);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.RecipientIds).HasMaxLength(2000);
            e.Property(x => x.ExtraEmails).HasMaxLength(2000);
            e.Property(x => x.DepartmentIds).HasMaxLength(2000);
            e.Property(x => x.SendHour).HasDefaultValue(9);
        });

        b.Entity<LeaveReason>(e =>
        {
            e.HasIndex(x => new { x.LeaveTypeId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.MaxDays).HasPrecision(6, 1);
            e.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AttendanceExemption>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.Date });
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<AttendanceSettings>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.ExemptionsPerMonth).HasDefaultValue(2);
            e.Property(x => x.ExemptionWithinDays).HasDefaultValue(7);
        });

        b.Entity<SalaryRevision>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.ByName).HasMaxLength(200);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<RecurringPayItem>(e =>
        {
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<SalaryHold>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.HeldByName).HasMaxLength(200);
            e.Property(x => x.ReleasedByName).HasMaxLength(200);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<ReportSettings>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.Signatures).HasMaxLength(400);
        });

        b.Entity<ScheduledRun>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Note).HasMaxLength(500);
        });

        b.Entity<TrainingCategory>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<TrainingProgram>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Title }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Link).HasMaxLength(500);
            e.Property(x => x.DepartmentIds).HasMaxLength(2000);
            e.Property(x => x.DurationHours).HasPrecision(6, 1);
            e.HasOne<TrainingCategory>().WithMany().HasForeignKey(x => x.CategoryId);
        });

        b.Entity<TrainingSession>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.StartsAtUtc });
            e.Property(x => x.Venue).HasMaxLength(500);
            e.Property(x => x.ExternalTrainer).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.TrainerEmployeeId);
        });

        b.Entity<TrainingEnrollment>(e =>
        {
            e.HasIndex(x => new { x.SessionId, x.EmployeeId }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.NominatedByName).HasMaxLength(200);
            e.Property(x => x.FeedbackComment).HasMaxLength(2000);
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<TrainingRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.CourseName).HasMaxLength(200);
            e.Property(x => x.Provider).HasMaxLength(200);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne<TrainingProgram>().WithMany().HasForeignKey(x => x.ProgramId);
        });

        b.Entity<TravelSettings>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.DeskIds).HasMaxLength(2000);
            e.Property(x => x.MinNoticeDays).HasDefaultValue(3);
            e.Property(x => x.SettleWithinDays).HasDefaultValue(15);
        });

        b.Entity<TravelReason>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<TravelRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.Purpose).HasMaxLength(2000);
            e.Property(x => x.UrgentReason).HasMaxLength(500);
            e.Property(x => x.HotelCity).HasMaxLength(100);
            e.Property(x => x.PickDropNotes).HasMaxLength(500);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.Property(x => x.HotelName).HasMaxLength(150);
            e.Property(x => x.HotelRef).HasMaxLength(100);
            e.Property(x => x.BookingNotes).HasMaxLength(2000);
            e.Property(x => x.BookedByName).HasMaxLength(200);
            e.Property(x => x.AdvancePaidVia).HasMaxLength(100);
            e.Property(x => x.SettlementNote).HasMaxLength(2000);
            e.Property(x => x.SettlementDecisionNote).HasMaxLength(1000);
            e.Property(x => x.Settlement).HasDefaultValue(SettlementStatus.None);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne<TravelReason>().WithMany().HasForeignKey(x => x.ReasonId);
            e.HasOne<PayRequest>().WithMany().HasForeignKey(x => x.PayRequestId);
            e.HasMany(x => x.Legs).WithOne().HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Expenses).WithOne().HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TravelLeg>(e =>
        {
            e.Property(x => x.From).HasMaxLength(100);
            e.Property(x => x.To).HasMaxLength(100);
            e.Property(x => x.Preference).HasMaxLength(150);
            e.Property(x => x.BookingRef).HasMaxLength(100);
        });

        b.Entity<TravelExpense>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(500);
        });

        b.Entity<JobRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Qualification).HasMaxLength(300);
            e.Property(x => x.Skills).HasMaxLength(1000);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.RequestedByEmployeeId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReplacesEmployeeId);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId);
            e.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId);
        });

        b.Entity<JobPost>(e =>
        {
            e.Property(x => x.ScreenCities).HasMaxLength(500);
            e.Property(x => x.ScreenKeywords).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Slug).HasMaxLength(80);
            e.Property(x => x.Location).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(8000);
            e.Property(x => x.Requirements).HasMaxLength(8000);
            e.HasOne<JobRequest>().WithMany().HasForeignKey(x => x.JobRequestId);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId);
            e.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.RecruiterEmployeeId);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.HiringManagerEmployeeId);
        });

        b.Entity<Candidate>(e =>
        {
            e.Property(x => x.PortalToken).HasMaxLength(64);
            e.HasIndex(x => x.PortalToken).IsUnique().HasFilter("[PortalToken] IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Cnic).HasMaxLength(15);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.CurrentTitle).HasMaxLength(150);
            e.Property(x => x.CurrentCompany).HasMaxLength(150);
            e.Property(x => x.ExperienceYears).HasPrecision(4, 1);
            e.Property(x => x.LinkedInUrl).HasMaxLength(300);
            e.Property(x => x.Skills).HasMaxLength(1000);
            e.Property(x => x.ReferredBy).HasMaxLength(150);
            e.Property(x => x.BlacklistReason).HasMaxLength(500);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<JobApplication>(e =>
        {
            e.HasIndex(x => new { x.JobPostId, x.CandidateId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Stage });
            e.Property(x => x.CoverLetter).HasMaxLength(4000);
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.Property(x => x.OfferNote).HasMaxLength(2000);
            e.HasOne(x => x.JobPost).WithMany().HasForeignKey(x => x.JobPostId);
            e.HasOne(x => x.Candidate).WithMany().HasForeignKey(x => x.CandidateId);
        });

        b.Entity<ApplicationEvent>(e =>
        {
            e.HasIndex(x => x.ApplicationId);
            e.Property(x => x.Text).HasMaxLength(2000);
            e.Property(x => x.ByName).HasMaxLength(200);
            e.HasOne<JobApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Interview>(e =>
        {
            e.HasIndex(x => x.ApplicationId);
            e.HasIndex(x => new { x.TenantId, x.StartsAtUtc });
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Where).HasMaxLength(500);
            e.Property(x => x.InterviewerIds).HasMaxLength(2000);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId);
        });

        b.Entity<InterviewFeedback>(e =>
        {
            e.HasIndex(x => new { x.InterviewId, x.InterviewerEmployeeId }).IsUnique();
            e.Property(x => x.Strengths).HasMaxLength(2000);
            e.Property(x => x.Concerns).HasMaxLength(2000);
            e.HasOne<Interview>().WithMany().HasForeignKey(x => x.InterviewId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.InterviewerEmployeeId);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.TimestampUtc });
            e.HasIndex(x => x.TimestampUtc);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.Path).HasMaxLength(500);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(512);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
        });

        // SQL Server rejects multiple cascade paths, so only the parent-child links above cascade.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            if (fk.DeleteBehavior == DeleteBehavior.Cascade && !IsOwnedChild(fk.DeclaringEntityType.ClrType))
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        foreach (var type in b.Model.GetEntityTypes().Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)))
            b.Entity(type.ClrType).HasQueryFilter(TenantFilter(type.ClrType));

        SeedTaxYears(b);
    }

    private static bool IsOwnedChild(Type t) =>
        t == typeof(TaxSlab) || t == typeof(SalaryComponent) || t == typeof(Payslip) || t == typeof(PayslipLine)
        || t == typeof(ClearanceItem) || t == typeof(ExpenseItem) || t == typeof(ApprovalFlowStep) || t == typeof(ApprovalStep)
        || t == typeof(AttachmentContent) || t == typeof(LoanRepayment)
        || t == typeof(AnnouncementRead) || t == typeof(PollOption) || t == typeof(PollVote) || t == typeof(CustomFieldValue)
        || t == typeof(TicketComment) || t == typeof(TicketType) || t == typeof(OnboardingTemplateTask) || t == typeof(OnboardingTask) || t == typeof(ReviewItem) || t == typeof(ApplicationEvent) || t == typeof(InterviewFeedback)
        || t == typeof(TravelLeg) || t == typeof(TravelExpense) || t == typeof(TrainingEnrollment) || t == typeof(PieceRate) || t == typeof(BudgetPosition) || t == typeof(LeaveReason)
        || t == typeof(Dependant) || t == typeof(Qualification) || t == typeof(WorkExperience)
        || t == typeof(ProjectMember) || t == typeof(ProjectTask) || t == typeof(TimesheetLine) || t == typeof(ExpenseTypeRate)
        || t == typeof(ClearanceTemplateItem) || t == typeof(ExitQuestion) || t == typeof(InterviewQuestion);

    private LambdaExpression TenantFilter(Type entityType)
    {
        var p = Expression.Parameter(entityType, "e");
        var tenantId = Expression.Property(p, nameof(ITenantEntity.TenantId));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        return Expression.Lambda(Expression.Equal(tenantId, current), p);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.Entity is User { Role: UserRole.SuperAdmin })
            {
                if (entry.Entity.TenantId != Guid.Empty)
                    throw new InvalidOperationException("A SuperAdmin cannot belong to a company.");
            }
            else if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                if (CurrentTenantId == Guid.Empty)
                    throw new InvalidOperationException($"Cannot save {entry.Entity.GetType().Name} without a tenant.");
                entry.Entity.TenantId = CurrentTenantId;
            }
            else if (entry.State == EntityState.Modified && entry.Property(nameof(ITenantEntity.TenantId)).IsModified)
            {
                throw new InvalidOperationException("TenantId cannot be changed.");
            }
        }

        AuditLogs.AddRange(BuildDataAuditEntries());
        return base.SaveChangesAsync(ct);
    }

    private static readonly HashSet<string> MaskedProperties = [nameof(User.PasswordHash)];

    /// <summary>One Data audit row per added, changed or deleted entity, written in the same transaction as the change.</summary>
    private List<AuditLog> BuildDataAuditEntries()
    {
        var logs = new List<AuditLog>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog or Notification or PushSubscription or HRMS.Domain.Entities.PushKeys or EmailLog || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var changes = new Dictionary<string, object?>();
            foreach (var p in entry.Properties)
            {
                var name = p.Metadata.Name;
                if (entry.State == EntityState.Modified && !p.IsModified)
                    continue;
                if (entry.State == EntityState.Modified && Equals(p.OriginalValue, p.CurrentValue))
                    continue;

                object? Mask(object? v) => v switch
                {
                    null => null,
                    byte[] bytes => $"[{bytes.Length:N0} bytes]",
                    _ when MaskedProperties.Contains(name) => "***",
                    _ => v,
                };
                changes[name] = entry.State switch
                {
                    EntityState.Added => new { @new = Mask(p.CurrentValue) },
                    EntityState.Deleted => new { old = Mask(p.OriginalValue) },
                    _ => new { old = Mask(p.OriginalValue), @new = Mask(p.CurrentValue) },
                };
            }
            if (entry.State == EntityState.Modified && changes.Count == 0)
                continue;

            var tenantId = entry.Entity is ITenantEntity t && t.TenantId != Guid.Empty ? t.TenantId
                : entry.Entity is Tenant tenant ? tenant.Id
                : CurrentTenantId == Guid.Empty ? (Guid?)null : CurrentTenantId;

            logs.Add(new AuditLog
            {
                Category = AuditCategory.Data,
                TenantId = tenantId,
                UserId = currentUser.UserId,
                UserEmail = currentUser.Email,
                Role = currentUser.Role,
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Deleted => "Deleted",
                    _ => "Updated",
                },
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                Changes = JsonSerializer.Serialize(changes),
                IpAddress = currentUser.IpAddress,
                UserAgent = Truncate(currentUser.UserAgent, 512),
                CorrelationId = currentUser.CorrelationId,
            });
        }
        return logs;
    }

    private static string? Truncate(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;

    /// <summary>
    /// Salaried slabs for tax year 2025-26 (Finance Act 2025), including the 9% surcharge above Rs 10 million.
    /// Add each new tax year's slabs after the federal budget; payroll uses the latest year that has started.
    /// </summary>
    private static void SeedTaxYears(ModelBuilder b)
    {
        var ty2526 = new Guid("6a1f1c2e-0001-4000-8000-000000002526");
        b.Entity<TaxYear>().HasData(new
        {
            Id = ty2526,
            CreatedAtUtc = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Code = "2025-26",
            StartDate = new DateOnly(2025, 7, 1),
            EndDate = new DateOnly(2026, 6, 30),
            SurchargeThreshold = 10_000_000m,
            SurchargeRate = 0.09m,
        });

        object Slab(int n, decimal from, decimal? to, decimal fixedTax, decimal rate) => new
        {
            Id = new Guid($"6a1f1c2e-0002-4000-8000-0000000025{n:D2}"),
            CreatedAtUtc = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            TaxYearId = ty2526,
            FromAmount = from,
            ToAmount = to,
            FixedTax = fixedTax,
            Rate = rate,
        };

        b.Entity<TaxSlab>().HasData(
            Slab(1, 0m, 600_000m, 0m, 0m),
            Slab(2, 600_000m, 1_200_000m, 0m, 0.01m),
            Slab(3, 1_200_000m, 2_200_000m, 6_000m, 0.11m),
            Slab(4, 2_200_000m, 3_200_000m, 116_000m, 0.23m),
            Slab(5, 3_200_000m, 4_100_000m, 346_000m, 0.30m),
            Slab(6, 4_100_000m, null, 616_000m, 0.35m));
    }
}
