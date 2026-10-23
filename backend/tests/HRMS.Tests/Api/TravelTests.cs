using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Travel requests through approval, booking by the desk, advances and the settlement that reaches payroll.</summary>
public class TravelTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Func<string, Guid?, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "A", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant().Replace(" ", "")}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Trip-Pass-1234", role = manager is null ? "Manager" : "Employee" }));
            return (id, await factory.SignInAsync(email, "Trip-Pass-1234"));
        }
        return (admin, Person);
    }

    private static object Trip(DateOnly depart, DateOnly back, string? urgent = null, decimal? advance = null) => new
    {
        purpose = "Client workshop",
        tripType = "Return",
        isInternational = false,
        legs = new[]
        {
            new { from = "Lahore", to = "Karachi", date = Iso(depart), mode = "Air", preference = "Morning" },
            new { from = "Karachi", to = "Lahore", date = Iso(back), mode = "Air", preference = (string?)null },
        },
        urgentReason = urgent,
        needsHotel = true,
        hotelCity = "Karachi",
        checkIn = Iso(depart),
        checkOut = Iso(back),
        needsPickDrop = true,
        pickDropNotes = "Airport to hotel",
        advanceRequested = advance,
    };

    [Fact]
    public async Task A_trip_is_approved_booked_advanced_and_settled_into_payroll()
    {
        var (admin, person) = await CompanyAsync("Travel Co");
        var (manager, managerClient) = await person("Maha", null);
        var (desk, deskClient) = await person("Danish", null);
        var (sana, sanaClient) = await person("Sana", manager);
        await Ok(await admin.PutAsJsonAsync("/api/travel/settings", new { minNoticeDays = 3, perDiemDomestic = 5000, perDiemInternational = 50, settleWithinDays = 15, deskIds = new[] { desk } }));

        // Short notice needs a reason; overlapping trips are refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync("/api/travel", Trip(Today.AddDays(1), Today.AddDays(3)), ApiFactory.Json)).StatusCode);
        var trip = await Ok(await sanaClient.PostAsJsonAsync("/api/travel", Trip(Today.AddDays(1), Today.AddDays(3), urgent: "Client moved the date", advance: 20000), ApiFactory.Json));
        var id = trip.GetProperty("id").GetGuid();
        Assert.Equal("Pending", trip.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync("/api/travel", Trip(Today.AddDays(2), Today.AddDays(5), urgent: "x"), ApiFactory.Json)).StatusCode);

        // The manager approves from the inbox; the desk is told to book.
        var inbox = await Ok(await managerClient.GetAsync("/api/approvals/inbox"));
        Assert.Contains("URGENT", Assert.Single(inbox.EnumerateArray(), i => i.GetProperty("requestType").GetString() == "Travel").GetProperty("summary").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await sanaClient.PostAsJsonAsync($"/api/travel/{id}/booking", new { }, ApiFactory.Json)).StatusCode);
        await Ok(await managerClient.PostAsJsonAsync($"/api/travel/{id}/approve", new { }));
        Assert.Contains("Book travel for Sana A", (await Ok(await deskClient.GetAsync("/api/notifications"))).ToString());
        Assert.Single((await Ok(await deskClient.GetAsync("/api/travel/all?filter=book"))).EnumerateArray());

        var legs = (await Ok(await deskClient.GetAsync($"/api/travel/{id}"))).GetProperty("legs");
        await Ok(await deskClient.PostAsJsonAsync($"/api/travel/{id}/booking", new
        {
            legs = legs.EnumerateArray().Select(l => new { legId = l.GetProperty("id").GetGuid(), bookingRef = "PK-30" + l.GetProperty("order").GetInt32(), bookingCost = 25000 }),
            hotelName = "Marriott", hotelRef = "H-1", hotelCost = 30000,
        }, ApiFactory.Json));
        await Ok(await deskClient.PostAsJsonAsync($"/api/travel/{id}/advance", new { amount = 20000, paidOn = Iso(Today), via = "Cash" }, ApiFactory.Json));
        var booked = await Ok(await sanaClient.GetAsync($"/api/travel/{id}"));
        Assert.Equal("Booked", booked.GetProperty("status").GetString());
        Assert.Equal(80000m, booked.GetProperty("bookedCost").GetDecimal());
        Assert.Contains("is booked", (await Ok(await sanaClient.GetAsync("/api/notifications"))).ToString());

        // Settling before the trip starts is refused; then fake the trip into the past.
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync($"/api/travel/{id}/settlement", new { expenses = Array.Empty<object>(), perDiemDays = 1 }, ApiFactory.Json)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var t = await db.TravelRequests.IgnoreQueryFilters().FirstAsync(x => x.Id == id);
            t.DepartOn = Today.AddDays(-4);
            t.ReturnOn = Today.AddDays(-2);
            await db.SaveChangesAsync();
        }

        // 3 days of allowance (15,000) + 8,000 taxi - 20,000 advance = 3,000 to reimburse.
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync($"/api/travel/{id}/settlement", new { expenses = Array.Empty<object>(), perDiemDays = 4 }, ApiFactory.Json)).StatusCode);
        await Ok(await sanaClient.PostAsJsonAsync($"/api/travel/{id}/settlement", new
        {
            expenses = new[] { new { date = Iso(Today.AddDays(-3)), category = "Travel", description = "Taxis", amount = 8000 } }, perDiemDays = 3,
        }, ApiFactory.Json));
        Assert.Equal(3000m, (await Ok(await sanaClient.GetAsync($"/api/travel/{id}"))).GetProperty("net").GetDecimal());

        // HR sends it back once, then approves; payroll gets a non-taxable reimbursement.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/travel/{id}/settlement/return", new { }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/travel/{id}/settlement/return", new { note = "Attach the taxi receipts" }, ApiFactory.Json));
        Assert.Equal("Returned", (await Ok(await sanaClient.GetAsync($"/api/travel/{id}"))).GetProperty("settlement").GetString());
        await Ok(await sanaClient.PostAsJsonAsync($"/api/travel/{id}/settlement", new
        {
            expenses = new[] { new { date = Iso(Today.AddDays(-3)), category = "Travel", description = "Taxis", amount = 8000 } }, perDiemDays = 3,
        }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/travel/{id}/settlement/approve", new { }, ApiFactory.Json));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var pay = await db.PayRequests.IgnoreQueryFilters().SingleAsync(p => p.EmployeeId == sana);
            Assert.Equal(3000m, pay.Amount);
            Assert.False(pay.IsTaxable);
            Assert.Equal(HRMS.Domain.PayRequestKind.Allowance, pay.Kind);
        }
        Assert.Equal("Settled", (await Ok(await sanaClient.GetAsync($"/api/travel/{id}"))).GetProperty("settlement").GetString());

        var report = await Ok(await deskClient.GetAsync("/api/travel/report"));
        Assert.Equal(1, report.GetProperty("trips").GetInt32());
        Assert.Equal(0m, report.GetProperty("advancesOutstanding").GetDecimal());
    }

    [Fact]
    public async Task Unused_advance_is_recovered_and_outsiders_cannot_see_trips()
    {
        var (admin, person) = await CompanyAsync("Travel Rules");
        var (manager, managerClient) = await person("Maha", null);
        var (sana, sanaClient) = await person("Sana", manager);
        var (omar, omarClient) = await person("Omar", manager);

        var id = (await Ok(await sanaClient.PostAsJsonAsync("/api/travel", Trip(Today.AddDays(10), Today.AddDays(11), advance: 50000), ApiFactory.Json))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await omarClient.GetAsync($"/api/travel/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await omarClient.GetAsync("/api/travel/all")).StatusCode);
        await Ok(await managerClient.PostAsJsonAsync($"/api/travel/{id}/approve", new { }));
        await Ok(await admin.PostAsJsonAsync($"/api/travel/{id}/advance", new { amount = 50000, paidOn = Iso(Today), via = "Bank" }, ApiFactory.Json));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var t = await db.TravelRequests.IgnoreQueryFilters().FirstAsync(x => x.Id == id);
            t.DepartOn = Today.AddDays(-30);
            t.ReturnOn = Today.AddDays(-29);
            await db.SaveChangesAsync();
        }
        Assert.True(Assert.Single((await Ok(await admin.GetAsync("/api/travel/all?filter=unsettled"))).EnumerateArray()).GetProperty("settlementOverdue").GetBoolean());

        await Ok(await sanaClient.PostAsJsonAsync($"/api/travel/{id}/settlement", new
        {
            expenses = new[] { new { date = Iso(Today.AddDays(-30)), category = "Meals", description = "Dinner", amount = 5000 } }, perDiemDays = 0,
        }, ApiFactory.Json));
        Assert.Equal(-45000m, (await Ok(await sanaClient.GetAsync($"/api/travel/{id}"))).GetProperty("net").GetDecimal());
        await Ok(await admin.PostAsJsonAsync($"/api/travel/{id}/settlement/approve", new { }, ApiFactory.Json));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var pay = await db.PayRequests.IgnoreQueryFilters().SingleAsync(p => p.EmployeeId == sana);
            Assert.Equal(HRMS.Domain.PayRequestKind.Deduction, pay.Kind);
            Assert.Equal(45000m, pay.Amount);
        }

        // A trip that is cancelled before approval leaves nothing in the inbox.
        var other = (await Ok(await omarClient.PostAsJsonAsync("/api/travel", Trip(Today.AddDays(20), Today.AddDays(21)), ApiFactory.Json))).GetProperty("id").GetGuid();
        await Ok(await omarClient.PostAsJsonAsync($"/api/travel/{other}/cancel", new { note = "Meeting moved online" }, ApiFactory.Json));
        Assert.DoesNotContain((await Ok(await managerClient.GetAsync("/api/approvals/inbox"))).EnumerateArray(), i => i.GetProperty("requestType").GetString() == "Travel");
    }
}
