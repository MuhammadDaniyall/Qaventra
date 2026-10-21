using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Roster, shift change and swap requests, late penalties and closing a month's attendance.</summary>
public class SchedulingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    /// <summary>The first Monday at least a week from today, so every day of that week is in the future.</summary>
    private static DateOnly NextMonday()
    {
        var d = Today.AddDays(7);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Guid Lead, HttpClient LeadClient, Func<string, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Make(string first, Guid? managerId, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "S", joiningDate = "2024-01-01", status = "Active", managerId }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Shift-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Shift-Pass-1234"));
        }
        var (leadId, lead) = await Make("Lena", null, "Manager");
        return (admin, leadId, lead, first => Make(first, leadId, "Employee"));
    }

    private static async Task<Guid> ShiftAsync(HttpClient admin, string name, string start, string end, params string[] off) =>
        (await Ok(await admin.PostAsJsonAsync("/api/setup/shifts", new { name, startTime = start, endTime = end, graceMinutes = 10, weeklyOffDays = off, isDefault = false }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();

    private static JsonElement Cell(JsonElement roster, Guid employeeId, DateOnly day) =>
        roster.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("employeeId").GetGuid() == employeeId)
            .GetProperty("days").EnumerateArray().First(c => c.GetProperty("date").GetString() == Iso(day));

    [Fact]
    public async Task Roster_overrides_the_usual_shift_and_off_days()
    {
        var (admin, _, _, person) = await CompanyAsync("Roster");
        var (worker, _) = await person("Wali");
        var evening = await ShiftAsync(admin, "Evening", "14:00:00", "22:00:00", "Sunday");
        var mon = NextMonday();

        // Evening shift on Mon/Wed of next week, and Friday off.
        Assert.Equal(2, (await Ok(await admin.PutAsJsonAsync("/api/attendance/roster", new { employeeIds = new[] { worker }, from = Iso(mon), to = Iso(mon.AddDays(6)), weekdays = new[] { "Monday", "Wednesday" }, shiftId = evening }, ApiFactory.Json))).GetProperty("days").GetInt32());
        await Ok(await admin.PutAsJsonAsync("/api/attendance/roster", new { employeeIds = new[] { worker }, from = Iso(mon.AddDays(4)), to = Iso(mon.AddDays(4)), off = true }, ApiFactory.Json));
        // Saturday is normally off; roster him to work it.
        await Ok(await admin.PutAsJsonAsync("/api/attendance/roster", new { employeeIds = new[] { worker }, from = Iso(mon.AddDays(5)), to = Iso(mon.AddDays(5)), shiftId = evening }, ApiFactory.Json));

        var roster = await Ok(await admin.GetAsync($"/api/attendance/roster?from={Iso(mon)}&to={Iso(mon.AddDays(6))}"));
        Assert.Equal(evening, Cell(roster, worker, mon).GetProperty("shiftId").GetGuid());
        Assert.True(Cell(roster, worker, mon).GetProperty("rostered").GetBoolean());
        Assert.False(Cell(roster, worker, mon.AddDays(1)).GetProperty("rostered").GetBoolean());
        Assert.True(Cell(roster, worker, mon.AddDays(4)).GetProperty("isOff").GetBoolean());
        Assert.False(Cell(roster, worker, mon.AddDays(5)).GetProperty("isOff").GetBoolean());
        Assert.True(Cell(roster, worker, mon.AddDays(6)).GetProperty("isOff").GetBoolean());

        // Reset Monday back to the usual shift.
        await Ok(await admin.PutAsJsonAsync("/api/attendance/roster", new { employeeIds = new[] { worker }, from = Iso(mon), to = Iso(mon), clear = true }, ApiFactory.Json));
        roster = await Ok(await admin.GetAsync($"/api/attendance/roster?from={Iso(mon)}&to={Iso(mon)}"));
        Assert.False(Cell(roster, worker, mon).GetProperty("rostered").GetBoolean());
        Assert.NotEqual(evening, Cell(roster, worker, mon).GetProperty("shiftId").GetGuid());
    }

    [Fact]
    public async Task Late_is_judged_by_the_rostered_shift()
    {
        var (admin, _, _, person) = await CompanyAsync("LateRoster");
        var (worker, _) = await person("Wali");
        var evening = await ShiftAsync(admin, "Evening", "14:00:00", "22:00:00");
        var day = Today.AddDays(-3);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || day.Month != Today.Month && Today.Day > 3) day = day.AddDays(-1);

        var asUsual = await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(day), checkIn = "13:50:00", checkOut = "22:00:00" }, ApiFactory.Json));
        Assert.Equal("Late", asUsual.GetProperty("status").GetString());
        await Ok(await admin.PutAsJsonAsync("/api/attendance/roster", new { employeeIds = new[] { worker }, from = Iso(day), to = Iso(day), shiftId = evening }, ApiFactory.Json));
        var rostered = await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(day), checkIn = "13:50:00", checkOut = "22:00:00" }, ApiFactory.Json));
        Assert.Equal("Present", rostered.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Shift_change_and_swap_go_through_approval_into_the_roster()
    {
        var (admin, _, lead, person) = await CompanyAsync("Swaps");
        var (a, aClient) = await person("Asad");
        var (b, bClient) = await person("Bina");
        var evening = await ShiftAsync(admin, "Evening", "14:00:00", "22:00:00", "Saturday", "Sunday");
        var mon = NextMonday();

        // Change: Asad wants evenings Mon–Fri; the weekend stays off.
        var change = await Ok(await aClient.PostAsJsonAsync("/api/attendance/shift-requests/change", new { from = Iso(mon), to = Iso(mon.AddDays(6)), shiftId = evening, reason = "Evening classes" }, ApiFactory.Json));
        Assert.Equal("Pending", change.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await aClient.PostAsJsonAsync("/api/attendance/shift-requests/change", new { from = Iso(mon), to = Iso(mon), shiftId = evening, reason = "again" }, ApiFactory.Json)).StatusCode);
        await Ok(await lead.PostAsJsonAsync($"/api/attendance/shift-requests/{change.GetProperty("id").GetGuid()}/approve", new { }));
        var roster = await Ok(await admin.GetAsync($"/api/attendance/roster?from={Iso(mon)}&to={Iso(mon.AddDays(6))}"));
        Assert.Equal(evening, Cell(roster, a, mon.AddDays(2)).GetProperty("shiftId").GetGuid());
        Assert.Equal("Request", Cell(roster, a, mon.AddDays(2)).GetProperty("source").GetString());
        Assert.True(Cell(roster, a, mon.AddDays(5)).GetProperty("isOff").GetBoolean());

        // Swap the following Monday: Bina must agree before the manager sees it.
        var next = mon.AddDays(7);
        await Ok(await admin.PutAsJsonAsync("/api/attendance/roster", new { employeeIds = new[] { a }, from = Iso(next), to = Iso(next), shiftId = evening }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await aClient.PostAsJsonAsync("/api/attendance/shift-requests/swap", new { colleagueId = b, from = Iso(next.AddDays(1)), to = Iso(next.AddDays(1)), reason = "Same day" }, ApiFactory.Json)).StatusCode);
        var swap = await Ok(await aClient.PostAsJsonAsync("/api/attendance/shift-requests/swap", new { colleagueId = b, from = Iso(next), to = Iso(next), reason = "Doctor in the evening" }, ApiFactory.Json));
        Assert.Equal("AwaitingColleague", swap.GetProperty("status").GetString());
        var swapId = swap.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await lead.PostAsJsonAsync($"/api/attendance/shift-requests/{swapId}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await aClient.PostAsync($"/api/attendance/shift-requests/{swapId}/accept", null)).StatusCode);
        Assert.Contains((await Ok(await bClient.GetAsync("/api/attendance/shift-requests/my"))).EnumerateArray(), r => r.GetProperty("id").GetGuid() == swapId);
        await Ok(await bClient.PostAsync($"/api/attendance/shift-requests/{swapId}/accept", null));
        await Ok(await lead.PostAsJsonAsync($"/api/attendance/shift-requests/{swapId}/approve", new { note = "Fine" }));

        roster = await Ok(await admin.GetAsync($"/api/attendance/roster?from={Iso(next)}&to={Iso(next)}"));
        Assert.NotEqual(evening, Cell(roster, a, next).GetProperty("shiftId").GetGuid());
        Assert.Equal(evening, Cell(roster, b, next).GetProperty("shiftId").GetGuid());
        Assert.Equal("Swap", Cell(roster, b, next).GetProperty("source").GetString());
    }

    [Fact]
    public async Task Late_penalties_reach_payroll_and_a_closed_month_cannot_change()
    {
        var (admin, _, _, person) = await CompanyAsync("Penalty");
        var (worker, _) = await person("Wali");
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{worker}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 90000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
        await Ok(await admin.PutAsJsonAsync("/api/payroll/settings", new { eobiEmployeeContribution = 0, eobiEmployerContribution = 0, deductAbsentDays = false, overtimeMultiplier = 1.5, latesPerPenalty = 3, latePenaltyDays = 1, deductHalfDays = true }, ApiFactory.Json));

        var last = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var weekdays = Enumerable.Range(0, 28).Select(i => last.AddDays(i)).Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
        foreach (var d in weekdays.Take(4))
            await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(d), checkIn = "09:40:00", checkOut = "18:00:00" }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(weekdays[4]), checkIn = "09:00:00", checkOut = "11:00:00" }, ApiFactory.Json));

        var monthly = await Ok(await admin.GetAsync($"/api/attendance/monthly?year={last.Year}&month={last.Month}"));
        var row = monthly.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("employeeId").GetGuid() == worker);
        Assert.Equal(4, row.GetProperty("late").GetInt32());
        Assert.Equal(1, row.GetProperty("halfDay").GetInt32());
        Assert.Equal(1.5m, row.GetProperty("penaltyDays").GetDecimal());
        Assert.True(monthly.GetProperty("canClose").GetBoolean());

        // 90,000 / days in month per day: one penalty day for 4 lates, half a day for the half day.
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = last.Year, month = last.Month }));
        var slipId = run.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == worker).GetProperty("id").GetGuid();
        var lines = (await Ok(await admin.GetAsync($"/api/payroll/payslips/{slipId}"))).GetProperty("lines").EnumerateArray().ToList();
        var dayRate = 90000m / DateTime.DaysInMonth(last.Year, last.Month);
        Assert.Single(lines, l => l.GetProperty("name").GetString() == "Late arrival penalty (4 late, 1 day)" && l.GetProperty("amount").GetDecimal() == Math.Round(dayRate, 0));
        Assert.Single(lines, l => l.GetProperty("name").GetString() == "Half days (1 × ½ day)" && l.GetProperty("amount").GetDecimal() == Math.Round(dayRate / 2, 0));

        // Closing: not the current month; once closed nothing in it changes; reopen until payroll is finalized.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/attendance/periods/{Today.Year}/{Today.Month}/close", null)).StatusCode);
        await Ok(await admin.PostAsync($"/api/attendance/periods/{last.Year}/{last.Month}/close", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(weekdays[6]), checkIn = "09:00:00", checkOut = "18:00:00" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/attendance/roster", new { employeeIds = new[] { worker }, from = Iso(weekdays[6]), to = Iso(weekdays[6]), off = true }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsync($"/api/attendance/periods/{last.Year}/{last.Month}/reopen", null));
        await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = worker, date = Iso(weekdays[6]), checkIn = "09:00:00", checkOut = "18:00:00" }, ApiFactory.Json));

        // Finalizing payroll for an ended month closes it for good.
        run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = last.Year, month = last.Month }));
        await Ok(await admin.PostAsync($"/api/payroll/runs/{run.GetProperty("run").GetProperty("id").GetGuid()}/finalize", null));
        Assert.True((await Ok(await admin.GetAsync($"/api/attendance/monthly?year={last.Year}&month={last.Month}"))).GetProperty("closed").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/attendance/periods/{last.Year}/{last.Month}/reopen", null)).StatusCode);
    }
}
