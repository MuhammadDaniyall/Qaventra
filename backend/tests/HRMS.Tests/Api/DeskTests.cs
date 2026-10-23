using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Floors, desks and rooms; booking a desk for a day or a room by the hour; check-in and releasing no-shows.</summary>
public class DeskTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Guid Floor, Func<string, Task<(Guid Id, HttpClient Client)>> Person)> OfficeAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        var office = (await Ok(await admin.PostAsJsonAsync("/api/setup/office-locations", new { name = "Head Office", latitude = 31.52, longitude = 74.35, radiusMeters = 200 }))).GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/desks/floors", new { officeLocationId = office, name = "First floor", order = 1 }));
        var floor = Assert.Single((await Ok(await admin.GetAsync("/api/desks/floors"))).EnumerateArray()).GetProperty("id").GetGuid();
        async Task<(Guid, HttpClient)> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "A", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant().Replace(" ", "")}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Desk-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Desk-Pass-1234"));
        }
        return (admin, floor, Person);
    }

    [Fact]
    public async Task Desks_are_booked_once_a_day_and_rooms_by_the_hour()
    {
        var (admin, floor, person) = await OfficeAsync("Desk Co");
        var (sana, sanaClient) = await person("Sana");
        var (omar, omarClient) = await person("Omar");

        Assert.Equal(3, (await Ok(await admin.PostAsJsonAsync("/api/desks/spaces/bulk", new { floorId = floor, prefix = "A-", from = 1, to = 3, features = "Monitor" }, ApiFactory.Json))).GetProperty("added").GetInt32());
        Assert.Equal(0, (await Ok(await admin.PostAsJsonAsync("/api/desks/spaces/bulk", new { floorId = floor, prefix = "A-", from = 1, to = 3 }, ApiFactory.Json))).GetProperty("added").GetInt32());
        await Ok(await admin.PostAsJsonAsync("/api/desks/spaces", new { floorId = floor, name = "Boardroom", kind = "MeetingRoom", seats = 8, features = "Projector" }, ApiFactory.Json));
        var spaces = (await Ok(await admin.GetAsync($"/api/desks/floors/{floor}/spaces"))).EnumerateArray().ToList();
        Guid Id(string n) => spaces.First(s => s.GetProperty("name").GetString() == n).GetProperty("id").GetGuid();
        // A-03 is Omar's permanent desk.
        await Ok(await admin.PutAsJsonAsync($"/api/desks/spaces/{Id("A-03")}", new { floorId = floor, name = "A-03", kind = "Desk", seats = 1, assignedEmployeeId = omar, isActive = true }, ApiFactory.Json));

        var day = Today.AddDays(1);
        await Ok(await sanaClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("A-01"), date = Iso(day) }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("A-02"), date = Iso(day) }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await omarClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("A-01"), date = Iso(day) }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("A-03"), date = Iso(day.AddDays(1)) }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("A-02"), date = Iso(Today.AddDays(30)) }, ApiFactory.Json)).StatusCode);

        // Rooms: overlapping times are refused, back-to-back is fine, seats are checked.
        await Ok(await sanaClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("Boardroom"), date = Iso(day), from = "10:00", to = "11:00", title = "Sprint review", attendees = 6 }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await omarClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("Boardroom"), date = Iso(day), from = "10:30", to = "11:30", attendees = 2 }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await omarClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("Boardroom"), date = Iso(day), from = "11:00", to = "12:00", attendees = 9 }, ApiFactory.Json)).StatusCode);
        await Ok(await omarClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = Id("Boardroom"), date = Iso(day), from = "11:00", to = "12:00", attendees = 4 }, ApiFactory.Json));

        var plan = await Ok(await omarClient.GetAsync($"/api/desks/floors/{floor}/day?date={Iso(day)}"));
        Assert.Equal(1, plan.GetProperty("desksFree").GetInt32()); // A-02; A-01 is booked and A-03 is Omar's
        var room = plan.GetProperty("spaces").EnumerateArray().First(s => s.GetProperty("name").GetString() == "Boardroom");
        Assert.Equal(2, room.GetProperty("bookings").GetArrayLength());

        var inOffice = await Ok(await omarClient.GetAsync($"/api/desks/in-office?date={Iso(day)}"));
        Assert.Equal(2, inOffice.GetArrayLength()); // Sana's booking and Omar's permanent desk

        // Cancel frees the desk for others.
        var mine = (await Ok(await sanaClient.GetAsync("/api/desks/bookings/my"))).EnumerateArray().First(b => b.GetProperty("kind").GetString() == "Desk");
        await Ok(await sanaClient.PostAsync($"/api/desks/bookings/{mine.GetProperty("id").GetGuid()}/cancel", null));
        Assert.Equal(HttpStatusCode.Forbidden, (await omarClient.PostAsync($"/api/desks/bookings/{mine.GetProperty("id").GetGuid()}/cancel", null)).StatusCode);
        Assert.Equal(2, (await Ok(await omarClient.GetAsync($"/api/desks/floors/{floor}/day?date={Iso(day)}"))).GetProperty("desksFree").GetInt32());
        _ = sana;
    }

    [Fact]
    public async Task Bookings_nobody_checks_into_are_released()
    {
        var (admin, floor, person) = await OfficeAsync("Release Co");
        var (_, sanaClient) = await person("Sana");
        await Ok(await admin.PutAsJsonAsync("/api/desks/settings", new { maxDaysAhead = 14, requireCheckIn = true, releaseAfterMinutes = 60, dayStartsAt = 9 }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/desks/spaces/bulk", new { floorId = floor, prefix = "B", from = 1, to = 1 }, ApiFactory.Json));
        var desk = (await Ok(await admin.GetAsync($"/api/desks/floors/{floor}/spaces")))[0].GetProperty("id").GetGuid();
        await Ok(await sanaClient.PostAsJsonAsync("/api/desks/bookings", new { spaceId = desk, date = Iso(Today.AddDays(1)) }, ApiFactory.Json));
        var booking = (await Ok(await sanaClient.GetAsync("/api/desks/bookings/my")))[0];
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsync($"/api/desks/bookings/{booking.GetProperty("id").GetGuid()}/check-in", null)).StatusCode);

        // Move the booking into the past: nobody checked in, so it is released.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var b = await db.SpaceBookings.IgnoreQueryFilters().FirstAsync(x => x.Id == booking.GetProperty("id").GetGuid());
            b.Date = Today.AddDays(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal("Released", (await Ok(await sanaClient.GetAsync("/api/desks/bookings/my")))[0].GetProperty("status").GetString());
        var use = await Ok(await admin.GetAsync("/api/desks/utilisation"));
        Assert.Equal(1, use.GetProperty("noShows").GetInt32());
    }
}
