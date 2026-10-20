using HRMS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRMS.Infrastructure.Data;

/// <summary>Used only by "dotnet ef" to build migrations.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HrmsDbContext>
{
    public HrmsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=HRMS;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new HrmsDbContext(options, new NoUser());
    }

    private sealed class NoUser : ICurrentUser
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
        public Guid? EmployeeId => null;
        public UserRole? Role => null;
        public string? Email => null;
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? CorrelationId => null;
    }
}
