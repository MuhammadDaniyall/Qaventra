using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Piece-rate items, production sheets, approvals and payment with the month's salary.</summary>
public class PieceWorkTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    [Fact]
    public async Task Production_is_entered_approved_and_paid_with_salary()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Stitch Co");
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "T", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 40000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@stitch.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Piece-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Piece-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lena", null, "Manager");
        var (amna, amnaClient) = await Person("Amna", lead, "Employee");
        var (bilal, _) = await Person("Bilal", lead, "Employee");
        var (other, _) = await Person("Omar", null, "Employee");

        await Ok(await admin.PostAsJsonAsync("/api/piece-work/items", new { name = "Collar stitching", unit = "piece", rate = 12, dailyTarget = 100 }, ApiFactory.Json));
        var item = Assert.Single((await Ok(await admin.GetAsync("/api/piece-work/items"))).EnumerateArray()).GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/piece-work/items/{item}/rates", new { employeeId = bilal, rate = 15 }, ApiFactory.Json)); // senior rate

        var day = Today.AddDays(-1);
        // The lead enters a sheet for the team: approved at once, each at their own rate.
        Assert.Equal(HttpStatusCode.Forbidden, (await leadClient.PostAsJsonAsync("/api/piece-work/entries", new { itemId = item, date = Iso(day), lines = new[] { new { employeeId = other, quantity = 10 } } }, ApiFactory.Json)).StatusCode);
        var sheet = await Ok(await leadClient.PostAsJsonAsync("/api/piece-work/entries", new
        {
            itemId = item, date = Iso(day), lines = new[] { new { employeeId = amna, quantity = 120m }, new { employeeId = bilal, quantity = 80m } },
        }, ApiFactory.Json));
        Assert.True(sheet.GetProperty("approved").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await leadClient.PostAsJsonAsync("/api/piece-work/entries", new { itemId = item, date = Iso(day), lines = new[] { new { employeeId = amna, quantity = 5m } } }, ApiFactory.Json)).StatusCode);

        // Amna enters her own work for today: it waits for the lead.
        var own = await Ok(await amnaClient.PostAsJsonAsync("/api/piece-work/entries", new { itemId = item, date = Iso(Today), lines = new[] { new { employeeId = amna, quantity = 50m } } }, ApiFactory.Json));
        Assert.False(own.GetProperty("approved").GetBoolean());
        var pending = (await Ok(await leadClient.GetAsync("/api/piece-work/entries?team=true&status=Pending"))).EnumerateArray().ToList();
        var pid = Assert.Single(pending).GetProperty("id").GetGuid();
        await Ok(await leadClient.PostAsJsonAsync("/api/piece-work/entries/decide", new { ids = new[] { pid }, approve = true }, ApiFactory.Json));

        var mine = (await Ok(await amnaClient.GetAsync($"/api/piece-work/entries?year={day.Year}&month={day.Month}"))).EnumerateArray().ToList();
        Assert.Contains(mine, e => e.GetProperty("efficiency").GetDecimal() == 120m);

        // Payroll: 120 × 12 + 50 × 12 for Amna (if both days fall in this month), 80 × 15 for Bilal.
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));
        run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));
        async Task<List<JsonElement>> Lines(Guid id)
        {
            var slip = run.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == id).GetProperty("id").GetGuid();
            return (await Ok(await admin.GetAsync($"/api/payroll/payslips/{slip}"))).GetProperty("lines").EnumerateArray().ToList();
        }
        var amnaPiece = Assert.Single(await Lines(amna), l => l.GetProperty("name").GetString()!.StartsWith("Piece work"));
        Assert.Equal(day.Month == Today.Month ? 2040m : 600m, amnaPiece.GetProperty("amount").GetDecimal());
        Assert.Equal(1200m, Assert.Single(await Lines(bilal), l => l.GetProperty("name").GetString()!.StartsWith("Piece work")).GetProperty("amount").GetDecimal());

        await Ok(await admin.PostAsync($"/api/payroll/runs/{run.GetProperty("run").GetProperty("id").GetGuid()}/finalize", null));
        Assert.All((await Ok(await amnaClient.GetAsync($"/api/piece-work/entries?year={Today.Year}&month={Today.Month}"))).EnumerateArray(), e => Assert.True(e.GetProperty("paid").GetBoolean()));
        Assert.False((await Ok(await amnaClient.GetAsync($"/api/piece-work/entries?year={Today.Year}&month={Today.Month}")))[0].GetProperty("canDelete").GetBoolean());

        var report = await Ok(await admin.GetAsync($"/api/piece-work/report?year={Today.Year}&month={Today.Month}"));
        Assert.Equal(0, report.GetProperty("pending").GetInt32());
    }
}
