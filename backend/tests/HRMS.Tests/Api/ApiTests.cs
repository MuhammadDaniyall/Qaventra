using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Read(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static async Task<JsonElement> CreateEmployee(HttpClient client, string first, Guid? managerId = null)
    {
        return await Read(await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = first,
            lastName = "Test",
            joiningDate = "2026-01-01",
            status = "Active",
            managerId,
        }, ApiFactory.Json));
    }

    [Fact]
    public async Task Companies_cannot_see_each_others_employees()
    {
        var (a, _, _) = await factory.CreateCompanyAsync("Alpha");
        var (b, _, _) = await factory.CreateCompanyAsync("Beta");
        var alphaEmployee = await CreateEmployee(a, "Ayesha");

        var bList = await Read(await b.GetAsync("/api/employees?search=Ayesha"));
        Assert.Equal(0, bList.GetProperty("total").GetInt32());

        var direct = await b.GetAsync($"/api/employees/{alphaEmployee.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
    }

    [Fact]
    public async Task Every_event_is_audited_and_company_admin_sees_only_its_own_logs()
    {
        var (a, adminEmail, _) = await factory.CreateCompanyAsync("Gamma");
        var (b, _, _) = await factory.CreateCompanyAsync("Delta");
        await CreateEmployee(a, "Bilal");
        await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = adminEmail, password = "wrong-password" });
        await factory.SignInAsync(adminEmail, "Company-Pass-123");

        var logs = await Read(await a.GetAsync("/api/audit-logs?pageSize=200"));
        var items = logs.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, l => l.GetProperty("category").GetString() == "Auth" && l.GetProperty("action").GetString() == "LoginFailed");
        Assert.Contains(items, l => l.GetProperty("category").GetString() == "Auth" && l.GetProperty("action").GetString() == "LoginSucceeded");
        Assert.Contains(items, l => l.GetProperty("category").GetString() == "Request" && l.GetProperty("path").GetString() == "/api/employees");
        var created = items.First(l => l.GetProperty("category").GetString() == "Data" && l.GetProperty("entityType").GetString() == "Employee");
        Assert.Contains("Bilal", created.GetProperty("changes").GetString());

        var tenantIds = items.Select(l => l.GetProperty("tenantId").GetString()).Distinct().ToList();
        Assert.Single(tenantIds);

        // Password hashes are never written to the log.
        var userRows = items.Where(l => l.GetProperty("entityType").ValueKind == JsonValueKind.String && l.GetProperty("entityType").GetString() == "User");
        Assert.All(userRows, l => Assert.DoesNotContain("AQAAAA", l.GetProperty("changes").GetString() ?? ""));

        var deltaLogs = await Read(await b.GetAsync("/api/audit-logs?pageSize=200"));
        Assert.DoesNotContain(deltaLogs.GetProperty("items").EnumerateArray(), l => l.GetProperty("userEmail").GetString() == adminEmail);
    }

    [Fact]
    public async Task SuperAdmin_manages_companies_and_company_users_cannot_reach_platform()
    {
        var (a, adminEmail, companyId) = await factory.CreateCompanyAsync("Epsilon");
        var root = await factory.SignInAsOwnerAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/platform/companies")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsJsonAsync("/api/platform/companies", new
        {
            name = "Sneaky Co", adminFirstName = "X", adminLastName = "Y", adminEmail = "x@sneaky.test",
        })).StatusCode);
        // Without opening a company, the owner's platform token cannot reach company endpoints.
        Assert.Equal(HttpStatusCode.Forbidden, (await root.GetAsync("/api/employees")).StatusCode);

        var companies = await Read(await root.GetAsync("/api/platform/companies"));
        var epsilon = companies.EnumerateArray().First(c => c.GetProperty("id").GetGuid() == companyId);
        Assert.Equal(adminEmail, epsilon.GetProperty("adminEmail").GetString());

        var suspend = await root.PutAsJsonAsync($"/api/platform/companies/{epsilon.GetProperty("id").GetGuid()}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);
        var blocked = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = adminEmail, password = "Company-Pass-123" });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var allLogs = await Read(await root.GetAsync("/api/audit-logs?category=Auth&pageSize=200"));
        Assert.Contains(allLogs.GetProperty("items").EnumerateArray(), l => l.GetProperty("action").GetString() == "LoginBlocked:CompanyInactive");
    }

    [Fact]
    public async Task Manager_approves_team_leave_and_balance_is_used()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Zeta");
        var manager = await CreateEmployee(admin, "Mehwish");
        var worker = await CreateEmployee(admin, "Usman", manager.GetProperty("id").GetGuid());
        var managerEmail = $"mehwish+{Guid.NewGuid():N}@zeta.test";
        var workerEmail = $"usman+{Guid.NewGuid():N}@zeta.test";

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/employees/{manager.GetProperty("id").GetGuid()}/login",
            new { email = managerEmail, password = "Manager-Pass-1", role = "Manager" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/employees/{worker.GetProperty("id").GetGuid()}/login",
            new { email = workerEmail, password = "Worker-Pass-1", role = "Employee" }, ApiFactory.Json)).StatusCode);

        var workerClient = await factory.SignInAsync(workerEmail, "Worker-Pass-1");
        var managerClient = await factory.SignInAsync(managerEmail, "Manager-Pass-1");
        var types = await Read(await workerClient.GetAsync("/api/setup/leave-types"));
        var annual = types.EnumerateArray().First(t => t.GetProperty("code").GetString() == "AL");

        // Mon 2026-11-02 .. Wed 2026-11-04 = 3 working days.
        var applied = await Read(await workerClient.PostAsJsonAsync("/api/leave", new
        {
            leaveTypeId = annual.GetProperty("id").GetGuid(), fromDate = "2026-11-02", toDate = "2026-11-04", reason = "Family event",
        }, ApiFactory.Json));
        Assert.Equal(3m, applied.GetProperty("days").GetDecimal());

        // The employee cannot approve their own leave.
        var selfApprove = await workerClient.PostAsJsonAsync($"/api/leave/{applied.GetProperty("id").GetGuid()}/approve", new { note = "" });
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);

        var queue = await Read(await managerClient.GetAsync("/api/leave/approvals"));
        Assert.Contains(queue.EnumerateArray(), r => r.GetProperty("id").GetGuid() == applied.GetProperty("id").GetGuid());

        var approve = await managerClient.PostAsJsonAsync($"/api/leave/{applied.GetProperty("id").GetGuid()}/approve", new { note = "Enjoy" });
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var balances = await Read(await workerClient.GetAsync("/api/leave/balances?year=2026"));
        var al = balances.EnumerateArray().First(b => b.GetProperty("code").GetString() == "AL");
        Assert.Equal(3m, al.GetProperty("used").GetDecimal());
    }

    [Fact]
    public async Task Payroll_calculates_tax_and_eobi_and_locks_when_finalized()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Eta");
        var emp = await CreateEmployee(admin, "Sana");
        var id = emp.GetProperty("id").GetGuid();

        var save = await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new
        {
            effectiveFrom = "2026-01-01",
            basicSalary = 60_000,
            eobiApplicable = true,
            components = new object[]
            {
                new { name = "House Rent", kind = "Earning", amount = 27_000, isTaxable = true },
                new { name = "Medical", kind = "Earning", amount = 6_000, isTaxable = false },
            },
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

        var run = await Read(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = 2026, month = 9 }));
        var slip = run.GetProperty("payslips").EnumerateArray().Single(p => p.GetProperty("employeeId").GetGuid() == id);
        Assert.Equal(93_000m, slip.GetProperty("grossEarnings").GetDecimal());
        Assert.Equal(370m, slip.GetProperty("incomeTax").GetDecimal());
        Assert.Equal(93_000m - 370 - 400, slip.GetProperty("netPay").GetDecimal());

        var runId = run.GetProperty("run").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/payroll/runs/{runId}/finalize", null)).StatusCode);
        var again = await admin.PostAsJsonAsync("/api/payroll/runs", new { year = 2026, month = 9 });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Public_company_registration_is_not_available()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register-company", new
        {
            companyName = "Walk-in Co", firstName = "A", lastName = "B", email = "a@walkin.test", password = "Walk-in-Pass-1",
        });
        Assert.True(res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.Unauthorized,
            $"Expected the endpoint to be gone, got {(int)res.StatusCode}");
    }

    [Fact]
    public async Task Owner_creates_company_with_generated_admin_password_that_works()
    {
        var owner = await factory.SignInAsOwnerAsync();
        var email = $"ceo+{Guid.NewGuid():N}@theta.test";
        var created = await Read(await owner.PostAsJsonAsync("/api/platform/companies", new
        {
            name = $"Theta {Guid.NewGuid():N}", adminFirstName = "Hina", adminLastName = "Khan", adminEmail = email, city = "Lahore",
        }));

        var password = created.GetProperty("generatedPassword").GetString();
        Assert.False(string.IsNullOrEmpty(password));
        var admin = await factory.SignInAsync(email, password!);
        var me = await Read(await admin.GetAsync("/api/auth/me"));
        Assert.Equal("CompanyAdmin", me.GetProperty("role").GetString());
        Assert.Equal("Lahore", created.GetProperty("company").GetProperty("city").GetString());

        // The company starts with a designation the admin can use for new employees.
        var designations = await Read(await admin.GetAsync("/api/designations"));
        Assert.NotEmpty(designations.EnumerateArray());

        var reset = await Read(await owner.PostAsJsonAsync($"/api/platform/companies/{created.GetProperty("company").GetProperty("id").GetGuid()}/reset-admin-password", new { }));
        await factory.SignInAsync(email, reset.GetProperty("generatedPassword").GetString()!);
    }

    [Fact]
    public async Task Company_super_admin_is_named_after_the_company_by_default()
    {
        var owner = await factory.SignInAsOwnerAsync();
        var name = $"Systems Test {Guid.NewGuid():N}"[..20];
        var created = await Read(await owner.PostAsJsonAsync("/api/platform/companies", new
        {
            name, adminEmail = $"superadmin+{Guid.NewGuid():N}@systems.test",
        }));
        Assert.Equal($"{name} Super Admin", created.GetProperty("company").GetProperty("adminName").GetString());
    }

    [Fact]
    public async Task Owner_opens_a_company_and_sees_and_does_everything_inside_it()
    {
        var (admin, _, companyId) = await factory.CreateCompanyAsync("Iota");
        var hired = await CreateEmployee(admin, "Kamran");
        var (other, _, _) = await factory.CreateCompanyAsync("Kappa");
        await CreateEmployee(other, "Nida");

        var owner = await factory.SignInAsOwnerAsync();
        var session = await Read(await owner.PostAsync($"/api/platform/companies/{companyId}/enter", null));
        Assert.True(session.GetProperty("user").GetProperty("isActingAsOwner").GetBoolean());
        var inside = factory.CreateClient();
        inside.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("token").GetString());

        var list = await Read(await inside.GetAsync("/api/employees?pageSize=200"));
        var names = list.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("fullName").GetString()).ToList();
        Assert.Contains("Kamran Test", names);
        Assert.DoesNotContain("Nida Test", names);

        // Full HR rights inside the company, logged under the owner's account.
        await CreateEmployee(inside, "Owner-Hired");
        var logs = await Read(await admin.GetAsync("/api/audit-logs?category=Data&entityType=Employee&pageSize=200"));
        Assert.Contains(logs.GetProperty("items").EnumerateArray(),
            l => l.GetProperty("role").GetString() == "SuperAdmin" && (l.GetProperty("changes").GetString() ?? "").Contains("Owner-Hired"));

        var detail = await Read(await inside.GetAsync($"/api/employees/{hired.GetProperty("id").GetGuid()}"));
        Assert.Equal("Kamran", detail.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Employee_limit_set_by_owner_is_enforced()
    {
        // The Company Super Admin is employee #1, so a limit of 2 allows one more.
        var (admin, _, _) = await factory.CreateCompanyAsync("Lambda", maxEmployees: 2);
        await CreateEmployee(admin, "First");

        var res = await admin.PostAsJsonAsync("/api/employees", new { firstName = "Second", lastName = "Test", joiningDate = "2026-01-01", status = "Active" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("allows 2", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var res = await factory.CreateClient().GetAsync("/api/employees");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
