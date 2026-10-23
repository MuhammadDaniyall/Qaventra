using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Company lookups, the employee file (documents, grouping, do-not-hire) and family/education records.</summary>
public class EmployeeFileTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static object File(Guid? division = null, Guid? grade = null, string? passportExpires = null, bool blacklisted = false) => new
    {
        fatherName = "Aslam", nationality = "Pakistani", passportNo = "AB1234567", passportIssuedOn = "2020-01-01", passportExpiresOn = passportExpires,
        divisionId = division, gradeId = grade, isBlacklisted = blacklisted, blacklistReason = blacklisted ? "Fraud" : null,
    };

    [Fact]
    public async Task Lookups_file_and_records_work_end_to_end()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("FileCo");
        var dept = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Finance" }))).GetProperty("id").GetGuid();
        var other = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Sales" }))).GetProperty("id").GetGuid();
        var division = (await Ok(await admin.PostAsJsonAsync("/api/lookups", new { kind = "Division", name = "Corporate", code = "CORP" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        var grade = (await Ok(await admin.PostAsJsonAsync("/api/lookups", new { kind = "Grade", name = "G5" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        // A sub-department needs its department; names are unique per list.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/lookups", new { kind = "SubDepartment", name = "Payroll" }, ApiFactory.Json)).StatusCode);
        var sub = (await Ok(await admin.PostAsJsonAsync("/api/lookups", new { kind = "SubDepartment", name = "Payroll", parentId = other }, ApiFactory.Json))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/lookups", new { kind = "Grade", name = "G5" }, ApiFactory.Json)).StatusCode);

        var emp = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Sana", lastName = "F", joiningDate = "2024-01-01", status = "Active", departmentId = dept }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();

        // Wrong kind in a slot, or a sub-department of another department, is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/employees/{emp}/file", File(division: grade), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/employees/{emp}/file", new { subDepartmentId = sub }, ApiFactory.Json)).StatusCode);

        var soon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10).ToString("yyyy-MM-dd");
        var file = await Ok(await admin.PutAsJsonAsync($"/api/employees/{emp}/file", File(division, grade, soon), ApiFactory.Json));
        Assert.Equal("AB1234567", file.GetProperty("passportNo").GetString());
        Assert.Equal(division, file.GetProperty("divisionId").GetGuid());

        // Filters: employee list and reports by group.
        var list = await Ok(await admin.GetAsync($"/api/employees?groupId={grade}"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        var report = await Ok(await admin.PostAsJsonAsync("/api/reports/employees.file", new { groupIds = new[] { division }, includeLeft = false }, ApiFactory.Json));
        var row = Assert.Single(report.GetProperty("rows").EnumerateArray());
        Assert.Contains("Corporate", row.EnumerateArray().Select(c => c.GetString()));
        var docs = await Ok(await admin.PostAsJsonAsync("/api/reports/employees.documents", new { date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30).ToString("yyyy-MM-dd") }, ApiFactory.Json));
        Assert.Contains(docs.GetProperty("rows").EnumerateArray(), r => r.EnumerateArray().Any(c => c.GetString() == "Passport"));

        // Linked lookups cannot be deleted.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/lookups/{grade}")).StatusCode);

        // Records: nominee shares cannot pass 100%.
        var bad = new
        {
            dependants = new[] { new { name = "A", relation = "Son", isNominee = true, nomineeShare = 70 }, new { name = "B", relation = "Wife", isNominee = true, nomineeShare = 40 } },
            qualifications = Array.Empty<object>(), experience = Array.Empty<object>(),
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/employees/{emp}/records", bad, ApiFactory.Json)).StatusCode);
        var good = new
        {
            dependants = new[] { new { name = "Ali", relation = "Son", isNominee = true, nomineeShare = 100 } },
            qualifications = new[] { new { degree = "BS", institute = "NUST", field = "Accounting", toYear = 2018, isHighest = true } },
            experience = new[] { new { organization = "ABC", designation = "Clerk", fromDate = "2018-06-01", toDate = "2023-12-31", lastSalary = 90000 } },
        };
        var saved = await Ok(await admin.PutAsJsonAsync($"/api/employees/{emp}/records", good, ApiFactory.Json));
        Assert.Single(saved.GetProperty("dependants").EnumerateArray());
        var exp = await Ok(await admin.PostAsJsonAsync("/api/reports/employees.experience", new { }, ApiFactory.Json));
        Assert.Single(exp.GetProperty("rows").EnumerateArray());

        // Do-not-hire blocks re-hiring after they leave.
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{emp}/file", File(division, grade, soon, blacklisted: true), ApiFactory.Json));
        var detail = await Ok(await admin.GetAsync($"/api/employees/{emp}"));
        var update = JsonSerializer.Deserialize<Dictionary<string, object?>>(detail.GetRawText())!;
        update["status"] = "Terminated";
        update["leavingDate"] = "2025-01-31";
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{emp}", update, ApiFactory.Json));
        var rehire = await admin.PostAsJsonAsync($"/api/employees/{emp}/rehire", new { joiningDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"), status = "Active" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, rehire.StatusCode);
        Assert.Contains("do-not-hire", await rehire.Content.ReadAsStringAsync());
    }
}
