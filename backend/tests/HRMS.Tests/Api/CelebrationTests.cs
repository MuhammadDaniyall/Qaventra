using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class CelebrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        var text = await res.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    [Fact]
    public async Task Todays_birthday_is_listed_and_a_wish_reaches_the_person_once()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Cake Co");
        var dob = new DateOnly(1990, Today.Month, Math.Min(Today.Day, 28)).ToString("yyyy-MM-dd");
        var emp = (await Ok(await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Birthday", lastName = "Person", joiningDate = "2024-01-01", status = "Active", dateOfBirth = dob,
        }, ApiFactory.Json))).GetProperty("id").GetGuid();
        var email = $"bday+{Guid.NewGuid():N}@cake.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{emp}/login", new { email, password = "Cake-Pass-123", role = "Employee" }));
        var person = await factory.SignInAsync(email, "Cake-Pass-123");

        var list = await Ok(await admin.GetAsync("/api/dashboard/celebrations"));
        var mine = list.EnumerateArray().Where(c => c.GetProperty("employeeId").GetGuid() == emp && c.GetProperty("kind").GetString() == "Birthday").ToList();
        if (Today.Day > 28) return; // the test birthday was moved to the 28th; nothing to wish today
        Assert.True(Assert.Single(mine).GetProperty("isToday").GetBoolean());

        await Ok(await admin.PostAsJsonAsync($"/api/dashboard/celebrations/{emp}/wish", new { kind = "Birthday" }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/dashboard/celebrations/{emp}/wish", new { kind = "Birthday" }, ApiFactory.Json));

        var feed = await Ok(await person.GetAsync("/api/notifications"));
        Assert.Single(feed.GetProperty("items").EnumerateArray(), n => n.GetProperty("kind").GetString() == "Celebration");
        var again = await Ok(await admin.GetAsync("/api/dashboard/celebrations"));
        Assert.True(again.EnumerateArray().First(c => c.GetProperty("employeeId").GetGuid() == emp).GetProperty("wishedByMe").GetBoolean());

        // Nothing to wish on a day that isn't the occasion.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync($"/api/dashboard/celebrations/{emp}/wish", new { kind = "Anniversary" }, ApiFactory.Json)).StatusCode);
    }
}
