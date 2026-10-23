using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Shift flag rules, penalty rules, region holidays, off-day requests, projects and timesheets.</summary>
public class AttendanceDepthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Guid Worker, HttpClient WorkerClient, HttpClient Lead, Guid Shift)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        // 09:00–17:00 with a 60-minute break: 7 hours expected; under 6 h short day, under 3 h half day.
        var shift = (await Ok(await admin.PostAsJsonAsync("/api/setup/shifts", new
        {
            name = "Day", startTime = "09:00:00", endTime = "17:00:00", graceMinutes = 10, weeklyOffDays = new[] { "Saturday", "Sunday" }, isDefault = false,
            earlyLeaveGraceMinutes = 10, halfDayMinutes = 180, shortDayMinutes = 360, breakMinutes = 60,
        }, ApiFactory.Json))).GetProperty("id").GetGuid();
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "D", joiningDate = "2024-01-01", status = "Active", managerId = manager, shiftId = shift }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@depth.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Depth-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Depth-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lena", null, "Manager");
        var (worker, workerClient) = await Person("Wali", lead, "Employee");
        return (admin, worker, workerClient, leadClient, shift);
    }

    private static DateOnly WeekdayInLastMonth(int nth)
    {
        var start = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var d = start;
        var seen = 0;
        while (true)
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && ++seen == nth) return d;
            d = d.AddDays(1);
        }
    }

    [Fact]
    public async Task Flags_and_penalty_rules()
    {
        var (admin, worker, _, _, _) = await CompanyAsync("Flags");
        var day1 = WeekdayInLastMonth(1);
        var day2 = WeekdayInLastMonth(2);
        var a = await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(day1), checkIn = "09:30:00", checkOut = "15:20:00" }, ApiFactory.Json));
        Assert.Equal("ShortDay", a.GetProperty("status").GetString());
        Assert.Equal(20, a.GetProperty("lateMinutes").GetInt32());
        Assert.Equal(90, a.GetProperty("earlyLeaveMinutes").GetInt32());
        var b = await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(day2), checkIn = "09:45:00", checkOut = "17:00:00" }, ApiFactory.Json));
        Assert.Equal("Late", b.GetProperty("status").GetString());

        // Every 2 early-or-late flags: late counts once (day 2; day 1 became a short day), early leave once.
        await Ok(await admin.PostAsJsonAsync("/api/attendance-rules/penalties", new { name = "Short days", flag = "ShortDay", everyCount = 1, freeCount = 0, minMinutes = 0, repeat = true, action = "DeductPay", days = 0.5 }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/attendance-rules/penalties", new { name = "Lates", flag = "Late", everyCount = 2, freeCount = 0, minMinutes = 0, repeat = true, action = "DeductPay", days = 1 }, ApiFactory.Json));
        var leaveType = (await Ok(await admin.GetAsync("/api/setup/leave-types"))).EnumerateArray().First().GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/attendance-rules/penalties", new { name = "Early leave", flag = "EarlyLeave", everyCount = 1, freeCount = 0, minMinutes = 30, repeat = true, action = "DeductLeave", days = 1, leaveTypeId = leaveType }, ApiFactory.Json));
        var preview = await Ok(await admin.GetAsync($"/api/attendance-rules/penalties/preview?year={day1.Year}&month={day1.Month}"));
        var row = preview.EnumerateArray().Single(r => r.GetProperty("employeeId").GetGuid() == worker);
        Assert.Equal(0.5m, row.GetProperty("payDays").GetDecimal()); // short day only; one late is under every 2
        Assert.Equal(1m, row.GetProperty("leaveDays").GetDecimal());

        // Closing the month takes the leave penalty off the balance; reopening gives it back.
        var balance = async () => (await Ok(await admin.GetAsync($"/api/leave-rules/balances?year={day1.Year}"))).EnumerateArray()
            .First(x => x.GetProperty("employeeId").GetGuid() == worker && x.GetProperty("leaveTypeId").GetGuid() == leaveType).GetProperty("adjustment").GetDecimal();
        var before = await balance();
        await Ok(await admin.PostAsync($"/api/attendance/periods/{day1.Year}/{day1.Month}/close", null));
        Assert.Equal(before - 1, await balance());
        var report = await Ok(await admin.PostAsJsonAsync("/api/reports/attendance.penalties", new { year = day1.Year, month = day1.Month }, ApiFactory.Json));
        Assert.Equal(2, report.GetProperty("rows").GetArrayLength());
        await Ok(await admin.PostAsync($"/api/attendance/periods/{day1.Year}/{day1.Month}/reopen", null));
        Assert.Equal(before, await balance());
    }

    [Fact]
    public async Task Region_holidays_and_off_day_requests()
    {
        var (admin, worker, workerClient, lead, _) = await CompanyAsync("OffDays");
        var region = (await Ok(await admin.PostAsJsonAsync("/api/lookups", new { kind = "Region", name = "Lahore" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        var mon = Today.AddDays(14 - ((int)Today.DayOfWeek + 6) % 7);
        await Ok(await admin.PostAsJsonAsync("/api/setup/holidays", new { date = Iso(mon), name = "City day", kind = "Special", regionIds = new[] { region } }, ApiFactory.Json));
        var roster = async () => (await Ok(await admin.GetAsync($"/api/attendance/roster?from={Iso(mon)}&to={Iso(mon.AddDays(6))}"))).GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("employeeId").GetGuid() == worker).GetProperty("days").EnumerateArray().ToList();
        Assert.False((await roster())[0].GetProperty("isHoliday").GetBoolean());
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{worker}/file", new { regionId = region }, ApiFactory.Json));
        Assert.True((await roster())[0].GetProperty("isHoliday").GetBoolean());

        // Friday and Saturday off instead of Saturday and Sunday, for that week.
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync("/api/attendance-rules/off-days", new { fromDate = Iso(mon), toDate = Iso(mon.AddDays(6)), offDays = new[] { "Saturday", "Sunday" }, reason = "x" }, ApiFactory.Json)).StatusCode);
        await Ok(await workerClient.PostAsJsonAsync("/api/attendance-rules/off-days", new { fromDate = Iso(mon), toDate = Iso(mon.AddDays(6)), offDays = new[] { "Friday", "Saturday" }, reason = "Family" }, ApiFactory.Json));
        var waiting = await Ok(await lead.GetAsync("/api/attendance-rules/off-days/approvals"));
        var id = waiting.EnumerateArray().Single().GetProperty("id").GetGuid();
        await Ok(await lead.PostAsJsonAsync($"/api/approvals/OffDay/{id}/decide", new { approve = true }, ApiFactory.Json));
        var days = await roster();
        Assert.True(days[4].GetProperty("isOff").GetBoolean());  // Friday
        Assert.True(days[5].GetProperty("isOff").GetBoolean());  // Saturday
        Assert.False(days[6].GetProperty("isOff").GetBoolean()); // Sunday is a working day now
    }

    [Fact]
    public async Task Projects_and_timesheets()
    {
        var (admin, worker, workerClient, lead, _) = await CompanyAsync("Timesheets");
        var project = await Ok(await admin.PostAsJsonAsync("/api/projects", new
        {
            name = "Website", client = "ACME", budgetHours = 100, billable = true, openToAll = false, status = "Active",
            members = new[] { new { employeeId = worker, hourlyRate = 1000 } },
        }, ApiFactory.Json));
        var projectId = project.GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "Design", assigneeEmployeeId = worker, estimatedHours = 10, isDone = false }, ApiFactory.Json));
        var task = (await Ok(await admin.GetAsync($"/api/projects/{projectId}"))).GetProperty("tasks").EnumerateArray().Single().GetProperty("id").GetGuid();

        // The lead is not on the team.
        var monday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
        Assert.Equal(HttpStatusCode.BadRequest, (await lead.PutAsJsonAsync("/api/timesheets/week", new { weekStart = Iso(monday), lines = new[] { new { date = Iso(monday), projectId, minutes = 60 } } }, ApiFactory.Json)).StatusCode);
        await Ok(await workerClient.PutAsJsonAsync("/api/timesheets/week", new
        {
            weekStart = Iso(monday),
            lines = new object[] { new { date = Iso(monday), projectId, taskId = task, minutes = 240 }, new { date = Iso(monday.AddDays(1)), projectId, minutes = 120, note = "Review" } },
        }, ApiFactory.Json));
        var week = await Ok(await workerClient.PostAsync($"/api/timesheets/week/submit?start={Iso(monday)}", null));
        Assert.Equal("Submitted", week.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PutAsJsonAsync("/api/timesheets/week", new { weekStart = Iso(monday), lines = Array.Empty<object>() }, ApiFactory.Json)).StatusCode);

        var sheet = (await Ok(await lead.GetAsync("/api/timesheets/approvals"))).EnumerateArray().Single();
        await Ok(await lead.PostAsJsonAsync($"/api/timesheets/{sheet.GetProperty("id").GetGuid()}/approve", new { }, ApiFactory.Json));
        var after = await Ok(await admin.GetAsync($"/api/projects/{projectId}"));
        Assert.Equal(6m, after.GetProperty("loggedHours").GetDecimal());
        Assert.Equal(6000m, after.GetProperty("cost").GetDecimal());
        Assert.Equal(4m, after.GetProperty("tasks")[0].GetProperty("loggedHours").GetDecimal());
        var report = await Ok(await admin.PostAsJsonAsync("/api/reports/projects.hours", new { from = Iso(monday), to = Iso(monday.AddDays(6)) }, ApiFactory.Json));
        Assert.Equal("6", report.GetProperty("rows")[0][3].GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/projects/{projectId}")).StatusCode);
    }
}
