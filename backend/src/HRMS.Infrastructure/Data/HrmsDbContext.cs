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
            e.HasIndex(x => new { x.TenantId, x.EmployeeCode }).IsUnique();
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId);
        });

        b.Entity<OfficeLocation>().Property(x => x.Name).HasMaxLength(150);
        b.Entity<Shift>().Property(x => x.Name).HasMaxLength(100);

        b.Entity<Holiday>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.HasIndex(x => new { x.TenantId, x.Date }).IsUnique();
        });

        b.Entity<AttendanceRecord>().HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();

        b.Entity<LeaveType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.AnnualQuota).HasPrecision(6, 2);
        });

        b.Entity<LeaveBalance>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
            e.Property(x => x.Entitled).HasPrecision(6, 2);
            e.Property(x => x.Used).HasPrecision(6, 2);
        });

        b.Entity<LeaveRequest>(e =>
        {
            e.Property(x => x.Days).HasPrecision(6, 2);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
        });

        b.Entity<PayrollSettings>().HasIndex(x => x.TenantId).IsUnique();

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
            e.HasIndex(x => new { x.TenantId, x.Year, x.Month }).IsUnique();
            e.Property(x => x.TaxYearCode).HasMaxLength(10);
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
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Subject).HasMaxLength(300);
        });

        b.Entity<IssuedLetter>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ReferenceNo }).IsUnique();
            e.HasIndex(x => x.EmployeeId);
            e.Property(x => x.ReferenceNo).HasMaxLength(40);
            e.Property(x => x.Subject).HasMaxLength(300);
            e.Property(x => x.IssuedByName).HasMaxLength(200);
        });

        b.Entity<Separation>(e =>
        {
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
        || t == typeof(AttachmentContent);

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
            if (entry.Entity is AuditLog || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
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
