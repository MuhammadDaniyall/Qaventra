using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Profile change requests, emergency contacts, job history, re-hire and logins handed out by HR.</summary>
public class EmployeeRecordsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static async Task<Guid> NewEmployee(HttpClient admin, string first, object? extra = null)
    {
        var body = new Dictionary<string, object?> { ["firstName"] = first, ["lastName"] = "R", ["joiningDate"] = "2024-01-01", ["status"] = "Active" };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties())
                body[p.Name] = p.GetValue(extra);
        return (await Ok(await admin.PostAsJsonAsync("/api/employees", body, ApiFactory.Json))).GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> LoginFor(HttpClient admin, Guid id, string company, string role = "Employee")
    {
        var email = $"u{Guid.NewGuid():N}@{company.ToLowerInvariant()}.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Rec-Pass-1234", role }));
        return await factory.SignInAsync(email, "Rec-Pass-1234");
    }

    [Fact]
    public async Task Profile_change_requests_are_applied_once_approved()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("ProfReq");
        var leadId = await NewEmployee(admin, "Lena");
        var lead = await LoginFor(admin, leadId, "ProfReq", "Manager");
        var workerId = await NewEmployee(admin, "Wali", new { managerId = leadId, phone = "0300-1111111" });
        var worker = await LoginFor(admin, workerId, "ProfReq");

        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/employees/profile-changes", new { changes = new { cnic = "12345" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/employees/profile-changes", new { changes = new { phone = "0300-1111111" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/employees/profile-changes", new { changes = new { status = "Active" } })).StatusCode);

        var req = await Ok(await worker.PostAsJsonAsync("/api/employees/profile-changes", new
        {
            changes = new Dictionary<string, string?> { ["lastName"] = "Rahman", ["phone"] = "0321-2222222", ["bankAccount"] = "PK36SCBL0000001123456702", ["bloodGroup"] = "b+", ["maritalStatus"] = "Married" },
            reason = "Got married, new bank",
        }));
        Assert.Equal("Pending", req.GetProperty("status").GetString());
        Assert.Equal(5, req.GetProperty("changes").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/employees/profile-changes", new { changes = new { phone = "0333-3333333" } })).StatusCode);

        // Not applied until approved; the line manager sees it waiting.
        Assert.Equal("0300-1111111", (await Ok(await worker.GetAsync("/api/employees/me"))).GetProperty("phone").GetString());
        Assert.Single((await Ok(await lead.GetAsync("/api/employees/profile-changes"))).EnumerateArray());
        await Ok(await lead.PostAsJsonAsync($"/api/employees/profile-changes/{req.GetProperty("id").GetGuid()}/approve", new { }));

        var me = await Ok(await worker.GetAsync("/api/employees/me"));
        Assert.Equal("Rahman", me.GetProperty("lastName").GetString());
        Assert.Equal("0321-2222222", me.GetProperty("phone").GetString());
        Assert.Equal("B+", me.GetProperty("bloodGroup").GetString());
        Assert.Equal("Married", me.GetProperty("maritalStatus").GetString());
        Assert.Equal("Wali Rahman", (await Ok(await worker.GetAsync("/api/auth/me"))).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Employees_keep_their_own_emergency_contacts()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Emerg");
        var id = await NewEmployee(admin, "Sara");
        var sara = await LoginFor(admin, id, "Emerg");
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PutAsJsonAsync("/api/employees/me/emergency-contacts", new { contacts = new[] { new { name = "Ali", relation = "", phone = "0300" } } })).StatusCode);
        await Ok(await sara.PutAsJsonAsync("/api/employees/me/emergency-contacts", new
        {
            contacts = new[] { new { name = "Ali Khan", relation = "Brother", phone = "0300-1234567", altPhone = (string?)null }, new { name = "Hina", relation = "Mother", phone = "0301-7654321", altPhone = (string?)"042-111" } },
        }));
        var seen = await Ok(await admin.GetAsync($"/api/employees/{id}/emergency-contacts"));
        Assert.Equal(["Ali Khan", "Hina"], seen.EnumerateArray().Select(c => c.GetProperty("name").GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.GetAsync($"/api/employees/{id}/emergency-contacts")).StatusCode);
    }

    [Fact]
    public async Task Promotions_transfers_and_rehire_build_the_job_history()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("JobHist");
        var eng = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Engineering" }))).GetProperty("id").GetGuid();
        var qa = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Quality" }))).GetProperty("id").GetGuid();
        var dev = (await Ok(await admin.PostAsJsonAsync("/api/designations", new { title = "Developer" }))).GetProperty("id").GetGuid();
        var senior = (await Ok(await admin.PostAsJsonAsync("/api/designations", new { title = "Senior Developer" }))).GetProperty("id").GetGuid();
        var id = await NewEmployee(admin, "Omar", new { departmentId = eng, designationId = dev, email = "omar@jobhist.test" });
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

        // Promotion; a future date and a change that changes nothing are refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/employees/{id}/job-changes", new { kind = "Promotion", effectiveDate = today.AddDays(5), departmentId = eng, designationId = senior })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/employees/{id}/job-changes", new { kind = "Transfer", effectiveDate = today, departmentId = eng, designationId = dev })).StatusCode);
        var after = await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/job-changes", new { kind = "Promotion", effectiveDate = today, departmentId = eng, designationId = senior, note = "Annual review" }));
        Assert.Equal("Senior Developer", after.GetProperty("designation").GetString());

        // Moving department on the profile form is kept too.
        var e = await Ok(await admin.GetAsync($"/api/employees/{id}"));
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}", new { firstName = "Omar", lastName = "R", joiningDate = "2024-01-01", status = "Active", email = "omar@jobhist.test", departmentId = qa, designationId = senior }));
        var history = (await Ok(await admin.GetAsync($"/api/employees/{id}/job-history"))).EnumerateArray().ToList();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.GetProperty("kind").GetString() == "Promotion" && h.GetProperty("fromDesignation").GetString() == "Developer" && h.GetProperty("toDesignation").GetString() == "Senior Developer");
        Assert.Contains(history, h => h.GetProperty("kind").GetString() == "ProfileEdit" && h.GetProperty("fromDepartment").GetString() == "Engineering" && h.GetProperty("toDepartment").GetString() == "Quality");

        // Leaves, then is re-hired: joining date must be after leaving; record kept, history grows, login stays off.
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email = "omar@jobhist.test", password = "Rec-Pass-1234", role = "Employee" }));
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}", new { firstName = "Omar", lastName = "R", joiningDate = "2024-01-01", status = "Resigned", leavingDate = "2025-03-31", email = "omar@jobhist.test", departmentId = qa, designationId = senior }));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/employees/{id}/job-changes", new { kind = "Transfer", effectiveDate = today, departmentId = eng })).StatusCode);
        var former = await Ok(await admin.GetAsync("/api/employees?former=true"));
        Assert.Single(former.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/employees/{id}/rehire", new { joiningDate = "2025-03-01", status = "Probation", departmentId = eng })).StatusCode);
        var back = await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/rehire", new { joiningDate = today, status = "Probation", departmentId = eng, designationId = dev }));
        Assert.Equal("Probation", back.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, back.GetProperty("leavingDate").ValueKind);
        Assert.False(back.GetProperty("loginActive").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/employees/{id}/rehire", new { joiningDate = today, status = "Active" })).StatusCode);
        var rehire = (await Ok(await admin.GetAsync($"/api/employees/{id}/job-history"))).EnumerateArray().First();
        Assert.Equal("Rehire", rehire.GetProperty("kind").GetString());
        Assert.Contains("1 Jan 2024", rehire.GetProperty("note").GetString());
    }

    [Fact]
    public async Task HR_issues_logins_that_must_change_their_password()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Creds");
        var a = await NewEmployee(admin, "Asad", new { email = $"asad{Guid.NewGuid():N}@creds.test" });
        var b = await NewEmployee(admin, "Bina");
        var c = await NewEmployee(admin, "Chand", new { email = $"chand{Guid.NewGuid():N}@creds.test" });
        await LoginFor(admin, c, "Creds");

        var without = await Ok(await admin.GetAsync("/api/employees?withoutLogin=true"));
        Assert.DoesNotContain(without.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == c);

        // Email is off in tests, so the temporary password comes back once.
        var result = await Ok(await admin.PostAsJsonAsync("/api/employees/logins", new { employeeIds = new[] { a, b, c } }));
        Assert.False(result.GetProperty("emailed").GetBoolean());
        var created = Assert.Single(result.GetProperty("created").EnumerateArray());
        Assert.Equal(2, result.GetProperty("skipped").GetArrayLength());
        var (email, password) = (created.GetProperty("email").GetString()!, created.GetProperty("password").GetString()!);
        Assert.Matches("^[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}$", password);

        var asad = await factory.SignInAsync(email, password);
        Assert.True((await Ok(await asad.GetAsync("/api/auth/me"))).GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await asad.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = password, newPassword = password })).StatusCode);
        await Ok(await asad.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = password, newPassword = "My-Own-Pass-99" }));
        Assert.False((await Ok(await asad.GetAsync("/api/auth/me"))).GetProperty("mustChangePassword").GetBoolean());

        // Reset hands out a new temporary password and asks again.
        var reset = await Ok(await admin.PostAsync($"/api/employees/{a}/login/reset-password", null));
        var again = await factory.SignInAsync(email, reset.GetProperty("password").GetString()!);
        Assert.True((await Ok(await again.GetAsync("/api/auth/me"))).GetProperty("mustChangePassword").GetBoolean());
    }
}
