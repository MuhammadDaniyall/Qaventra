using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Half day and short leave, leave type policies, HR adjustments and comp-off.</summary>
public class LeavePolicyApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private static DateOnly Weekday(int daysAhead)
    {
        var d = Today.AddDays(daysAhead);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return d;
    }

    private async Task<(HttpClient Admin, HttpClient Worker, Guid WorkerId, Dictionary<string, JsonElement> Types)> SetupAsync(string company, string gender = "Male")
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(company);
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Wali", lastName = "L", joiningDate = "2024-01-01", status = "Active", gender }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var email = $"wali+{Guid.NewGuid():N}@{company.ToLowerInvariant()}.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Leave-Pass-123", role = "Employee" }));
        var types = (await Ok(await admin.GetAsync("/api/setup/leave-types"))).EnumerateArray().ToDictionary(t => t.GetProperty("code").GetString()!);
        return (admin, await factory.SignInAsync(email, "Leave-Pass-123"), id, types);
    }

    private static object Policy(JsonElement t, Action<Dictionary<string, object?>> change)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = t.GetProperty("name").GetString(), ["code"] = t.GetProperty("code").GetString(), ["annualQuota"] = t.GetProperty("annualQuota").GetDecimal(),
            ["isPaid"] = t.GetProperty("isPaid").GetBoolean(), ["isActive"] = true, ["accrual"] = "Upfront", ["carryForwardMax"] = 0,
            ["maxConsecutiveDays"] = null, ["minNoticeDays"] = 0, ["applicableGender"] = null, ["allowedOnProbation"] = true,
            ["allowHalfDay"] = true, ["allowShortLeave"] = false, ["isCompensatory"] = false,
        };
        change(body);
        return body;
    }

    [Fact]
    public async Task Half_day_and_short_leave_use_fractions_of_a_day()
    {
        var (admin, worker, _, types) = await SetupAsync("Halves");
        var cl = types["CL"].GetProperty("id").GetGuid();
        var day = Weekday(10);

        var first = await Ok(await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = cl, fromDate = day.ToString("yyyy-MM-dd"), toDate = day.ToString("yyyy-MM-dd"), duration = "HalfDay", halfDayPart = "FirstHalf" }, ApiFactory.Json));
        Assert.Equal(0.5m, first.GetProperty("days").GetDecimal());
        Assert.Equal("HalfDay", first.GetProperty("duration").GetString());
        Assert.Equal("FirstHalf", first.GetProperty("halfDayPart").GetString());
        // The other half of the same day is allowed; the same half again is not.
        await Ok(await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = cl, fromDate = day.ToString("yyyy-MM-dd"), toDate = day.ToString("yyyy-MM-dd"), duration = "HalfDay", halfDayPart = "SecondHalf" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = cl, fromDate = day.ToString("yyyy-MM-dd"), toDate = day.ToString("yyyy-MM-dd"), duration = "HalfDay", halfDayPart = "FirstHalf" }, ApiFactory.Json)).StatusCode);

        // Short leave only where the type allows it (Unpaid Leave does for new companies; Casual does not).
        var shortDay = Weekday(17).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = cl, fromDate = shortDay, toDate = shortDay, duration = "ShortLeave", startTime = "10:00:00", endTime = "12:00:00" }, ApiFactory.Json)).StatusCode);
        var ul = types["UL"].GetProperty("id").GetGuid();
        var shortLeave = await Ok(await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = ul, fromDate = shortDay, toDate = shortDay, duration = "ShortLeave", startTime = "10:00:00", endTime = "12:00:00" }, ApiFactory.Json));
        Assert.Equal(0.25m, shortLeave.GetProperty("days").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = ul, fromDate = Weekday(24).ToString("yyyy-MM-dd"), toDate = Weekday(24).ToString("yyyy-MM-dd"), duration = "ShortLeave", startTime = "09:00:00", endTime = "15:00:00" }, ApiFactory.Json)).StatusCode);

        var balances = await Ok(await worker.GetAsync($"/api/leave/balances?year={day.Year}"));
        var casual = balances.EnumerateArray().Single(b => b.GetProperty("code").GetString() == "CL");
        Assert.Equal(1m, casual.GetProperty("pending").GetDecimal());
    }

    [Fact]
    public async Task Policies_limit_who_when_and_how_much()
    {
        var (admin, worker, workerId, types) = await SetupAsync("Rules", gender: "Male");
        var al = types["AL"];
        await Ok(await admin.PutAsJsonAsync($"/api/setup/leave-types/{al.GetProperty("id").GetGuid()}",
            Policy(al, p => { p["accrual"] = "Monthly"; p["maxConsecutiveDays"] = 3; p["minNoticeDays"] = 7; p["allowHalfDay"] = false; }), ApiFactory.Json));

        var tooSoon = Weekday(2).ToString("yyyy-MM-dd");
        var notice = await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = al.GetProperty("id").GetGuid(), fromDate = tooSoon, toDate = tooSoon }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, notice.StatusCode);
        Assert.Contains("in advance", await notice.Content.ReadAsStringAsync());

        var start = Weekday(14);
        var tooLong = await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = al.GetProperty("id").GetGuid(), fromDate = start.ToString("yyyy-MM-dd"), toDate = start.AddDays(6).ToString("yyyy-MM-dd") }, ApiFactory.Json);
        Assert.Contains("at most 3", await tooLong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = al.GetProperty("id").GetGuid(), fromDate = start.ToString("yyyy-MM-dd"), toDate = start.ToString("yyyy-MM-dd"), duration = "HalfDay", halfDayPart = "FirstHalf" }, ApiFactory.Json)).StatusCode);

        // Monthly accrual: earned = quota x months so far / 12.
        var bal = (await Ok(await worker.GetAsync($"/api/leave/balances?year={Today.Year}"))).EnumerateArray().Single(b => b.GetProperty("code").GetString() == "AL");
        var expected = Math.Round(14m * Today.Month / 12m * 2, MidpointRounding.AwayFromZero) / 2;
        Assert.Equal(expected, bal.GetProperty("earned").GetDecimal());
        Assert.Equal(14m, bal.GetProperty("entitled").GetDecimal());

        // A female-only type is refused for a male employee.
        var maternity = await Ok(await admin.PostAsJsonAsync("/api/setup/leave-types", new
        {
            name = "Maternity Leave", code = "ML", annualQuota = 90, isPaid = true, isActive = true, applicableGender = "Female",
        }, ApiFactory.Json));
        var ml = await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = maternity.GetProperty("id").GetGuid(), fromDate = start.ToString("yyyy-MM-dd"), toDate = start.ToString("yyyy-MM-dd") }, ApiFactory.Json);
        Assert.Contains("only for female", await ml.Content.ReadAsStringAsync());

        // HR adjusts a balance with a reason; the employee sees it.
        var sl = types["SL"].GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/leave/balances/{workerId}/{sl}?year={Today.Year}", new { adjustment = 2, note = "Hospitalised, extra sick days approved by CEO" }));
        var sick = (await Ok(await admin.GetAsync($"/api/leave/balances/{workerId}?year={Today.Year}"))).EnumerateArray().Single(b => b.GetProperty("code").GetString() == "SL");
        Assert.Equal(2m, sick.GetProperty("adjustment").GetDecimal());
        Assert.Equal(sick.GetProperty("entitled").GetDecimal() + 2, sick.GetProperty("available").GetDecimal());
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync($"/api/leave/balances/{workerId}?year={Today.Year}")).StatusCode);
    }

    [Fact]
    public async Task Comp_off_for_weekend_work_credits_compensatory_leave()
    {
        var (admin, worker, workerId, types) = await SetupAsync("CompOff");
        var sunday = Today.AddDays(-1);
        while (sunday.DayOfWeek != DayOfWeek.Sunday) sunday = sunday.AddDays(-1);
        var monday = sunday.AddDays(-6);

        // A working day does not qualify; a weekend day with no attendance does not either.
        Assert.Contains("normal working day", await (await worker.PostAsJsonAsync("/api/leave/comp-off", new { workedOn = monday.ToString("yyyy-MM-dd"), days = 1, reason = "Release" })).Content.ReadAsStringAsync());
        Assert.Contains("no attendance", await (await worker.PostAsJsonAsync("/api/leave/comp-off", new { workedOn = sunday.ToString("yyyy-MM-dd"), days = 1, reason = "Release" })).Content.ReadAsStringAsync());

        await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = workerId, date = sunday.ToString("yyyy-MM-dd"), checkIn = "10:00:00", checkOut = "16:00:00" }));
        var claim = await Ok(await worker.PostAsJsonAsync("/api/leave/comp-off", new { workedOn = sunday.ToString("yyyy-MM-dd"), days = 1, reason = "Production release over the weekend" }));
        Assert.Equal(360, claim.GetProperty("workedMinutes").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/leave/comp-off", new { workedOn = sunday.ToString("yyyy-MM-dd"), days = 0.5, reason = "Again" })).StatusCode);

        // No manager: the default chain sends it to HR.
        await Ok(await admin.PostAsJsonAsync($"/api/leave/comp-off/{claim.GetProperty("id").GetGuid()}/approve", new { }));
        var cpl = (await Ok(await worker.GetAsync($"/api/leave/balances?year={sunday.Year}"))).EnumerateArray().Single(b => b.GetProperty("isCompensatory").GetBoolean());
        Assert.Equal(1m, cpl.GetProperty("credited").GetDecimal());
        Assert.Equal(1m, cpl.GetProperty("available").GetDecimal());

        // And it can be spent like any paid leave.
        var day = Weekday(5).ToString("yyyy-MM-dd");
        if (Weekday(5).Year == sunday.Year)
            await Ok(await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = cpl.GetProperty("leaveTypeId").GetGuid(), fromDate = day, toDate = day }, ApiFactory.Json));
    }

    [Fact]
    public async Task Leave_in_a_closed_month_cannot_be_applied_approved_or_cancelled()
    {
        var (admin, worker, _, types) = await SetupAsync("Closed Month Leave");
        var cl = types["CL"].GetProperty("id").GetGuid();
        var lastMonth = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var day = lastMonth.AddDays(9);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        var date = day.ToString("yyyy-MM-dd");

        var applied = await Ok(await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = cl, fromDate = date, toDate = date }, ApiFactory.Json));
        var id = applied.GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"/api/attendance/periods/{lastMonth.Year}/{lastMonth.Month}/close", null));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/leave/{id}/approve", new { note = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await worker.PostAsJsonAsync("/api/leave", new { leaveTypeId = cl, fromDate = date, toDate = date }, ApiFactory.Json)).StatusCode);

        await Ok(await admin.PostAsync($"/api/attendance/periods/{lastMonth.Year}/{lastMonth.Month}/reopen", null));
        Assert.True((await admin.PostAsJsonAsync($"/api/leave/{id}/approve", new { note = "" })).IsSuccessStatusCode);

        await Ok(await admin.PostAsync($"/api/attendance/periods/{lastMonth.Year}/{lastMonth.Month}/close", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/leave/{id}/cancel", null)).StatusCode);
    }
}
