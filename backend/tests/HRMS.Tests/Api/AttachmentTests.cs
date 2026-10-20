using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Files on leave requests and employee documents: who may see, add and remove them.</summary>
public class AttachmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");

    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, string ownerType, Guid ownerId, string fileName, byte[] bytes, string? category = null, string? expiresOn = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (category is not null) form.Add(new StringContent(category), "category");
        if (expiresOn is not null) form.Add(new StringContent(expiresOn), "expiresOn");
        return client.PostAsync($"/api/attachments/{ownerType}/{ownerId}", form);
    }

    private async Task<(HttpClient Admin, HttpClient Lead, HttpClient Worker, HttpClient Peer, Guid WorkerId)> TeamAsync(string company)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(company);
        async Task<Guid> Employee(string first, Guid? managerId) =>
            (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = company, joiningDate = "2025-01-01", status = "Active", managerId }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        async Task<HttpClient> Login(Guid id, string role)
        {
            var email = $"u+{Guid.NewGuid():N}@{company.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Files-Pass-123", role }));
            return await factory.SignInAsync(email, "Files-Pass-123");
        }
        var lead = await Employee("Lead", null);
        var worker = await Employee("Worker", lead);
        var peer = await Employee("Peer", lead);
        return (admin, await Login(lead, "Manager"), await Login(worker, "Employee"), await Login(peer, "Employee"), worker);
    }

    [Fact]
    public async Task Leave_files_are_seen_by_the_employee_and_approvers_only()
    {
        var (admin, lead, worker, peer, _) = await TeamAsync("Files");
        var types = await Ok(await worker.GetAsync("/api/setup/leave-types"));
        var sick = types.EnumerateArray().First(t => t.GetProperty("isPaid").GetBoolean());
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(10);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        var leave = await Ok(await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = sick.GetProperty("id").GetGuid(), fromDate = day.ToString("yyyy-MM-dd"), toDate = day.ToString("yyyy-MM-dd") }));
        var leaveId = leave.GetProperty("id").GetGuid();

        var file = await Ok(await Upload(worker, "Leave", leaveId, "medical-certificate.pdf", Pdf));
        Assert.Equal("application/pdf", file.GetProperty("contentType").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(worker, "Leave", leaveId, "evil.html", Encoding.UTF8.GetBytes("<script>alert(1)</script>"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(worker, "Leave", leaveId, "big.pdf", new byte[5 * 1024 * 1024 + 1])).StatusCode);

        var list = await Ok(await lead.GetAsync($"/api/attachments/Leave/{leaveId}"));
        Assert.Single(list.GetProperty("items").EnumerateArray());
        var download = await lead.GetAsync($"/api/attachments/{file.GetProperty("id").GetGuid()}/file");
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());

        Assert.Equal(HttpStatusCode.NotFound, (await peer.GetAsync($"/api/attachments/Leave/{leaveId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await peer.GetAsync($"/api/attachments/{file.GetProperty("id").GetGuid()}/file")).StatusCode);

        var mine = (await Ok(await worker.GetAsync("/api/leave/my"))).EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == leaveId);
        Assert.Equal(1, mine.GetProperty("attachmentCount").GetInt32());

        // Once decided, the employee can no longer add or remove files; HR still can.
        await Ok(await lead.PostAsJsonAsync($"/api/leave/{leaveId}/approve", new { }));
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(worker, "Leave", leaveId, "late.pdf", Pdf)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.DeleteAsync($"/api/attachments/{file.GetProperty("id").GetGuid()}")).StatusCode);
        await Ok(await admin.DeleteAsync($"/api/attachments/{file.GetProperty("id").GetGuid()}"));
    }

    [Fact]
    public async Task Employee_documents_with_expiry_stay_inside_the_company()
    {
        var (admin, lead, worker, _, workerId) = await TeamAsync("Docs");
        var (other, _, _) = await factory.CreateCompanyAsync("OtherDocs");

        var cnic = await Ok(await Upload(admin, "EmployeeDocument", workerId, "cnic.jpg", [0xFF, 0xD8, 0xFF, 0xE0], "Cnic", "2030-05-31"));
        Assert.Equal("Cnic", cnic.GetProperty("category").GetString());
        Assert.Equal("2030-05-31", cnic.GetProperty("expiresOn").GetString());
        await Ok(await Upload(worker, "EmployeeDocument", workerId, "degree.pdf", Pdf, "Degree"));

        var docs = await Ok(await worker.GetAsync($"/api/attachments/EmployeeDocument/{workerId}"));
        Assert.Equal(2, docs.GetProperty("items").GetArrayLength());
        Assert.True(docs.GetProperty("canAdd").GetBoolean());

        Assert.Equal(HttpStatusCode.NotFound, (await lead.GetAsync($"/api/attachments/EmployeeDocument/{workerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/attachments/EmployeeDocument/{workerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/attachments/{cnic.GetProperty("id").GetGuid()}/file")).StatusCode);

        // The employee cannot remove what HR uploaded.
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.DeleteAsync($"/api/attachments/{cnic.GetProperty("id").GetGuid()}")).StatusCode);
    }
}
