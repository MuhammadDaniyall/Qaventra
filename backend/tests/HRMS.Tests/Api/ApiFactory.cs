using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Runs the real API against a throwaway LocalDB database that is dropped after the test class.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SuperAdminEmail = "root@hrms.test";
    public const string SuperAdminPassword = "Test-Only-Root-Pass-1";

    private readonly string _database = $"HRMS_Tests_{Guid.NewGuid():N}";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Hrms", $"Server=(localdb)\\MSSQLLocalDB;Database={_database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True");
        builder.UseSetting("Jwt:Key", "test-signing-key-test-signing-key-0123456789");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("SuperAdmin:Email", SuperAdminEmail);
        builder.UseSetting("SuperAdmin:Password", SuperAdminPassword);
        builder.UseSetting("RateLimit:AuthPerMinute", "1000");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<HrmsDbContext>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var session = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());
        return client;
    }

    public Task<HttpClient> SignInAsOwnerAsync() => SignInAsync(SuperAdminEmail, SuperAdminPassword);

    /// <summary>The owner creates a company (with its Company Super Admin); returns a client signed in as that admin.</summary>
    public async Task<(HttpClient Client, string Email, Guid CompanyId)> CreateCompanyAsync(string company, int? maxEmployees = null)
    {
        var email = $"admin+{Guid.NewGuid():N}@{company.ToLowerInvariant().Replace(' ', '-')}.test";
        var owner = await SignInAsOwnerAsync();
        var res = await owner.PostAsJsonAsync("/api/platform/companies", new
        {
            name = $"{company} {Guid.NewGuid():N}"[..Math.Min(company.Length + 9, 200)],
            adminFirstName = "Admin",
            adminLastName = company,
            adminEmail = email,
            adminPassword = "Company-Pass-123",
            maxEmployees,
        });
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (await SignInAsync(email, "Company-Pass-123"), email, created.GetProperty("company").GetProperty("id").GetGuid());
    }
}
