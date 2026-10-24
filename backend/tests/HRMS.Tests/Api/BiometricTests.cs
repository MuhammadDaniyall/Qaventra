using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class BiometricTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private async Task<(HttpClient Admin, Guid EmployeeId, string Token)> SetupAsync(string company)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(company);
        var emp = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Punch", lastName = "Er", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{emp}/file", new { punchCode = "1001" }, ApiFactory.Json));
        var device = await Ok(await admin.PostAsJsonAsync("/api/biometric/devices", new { name = "Gate 1", serialNumber = "ZK-01" }, ApiFactory.Json));
        return (admin, emp, device.GetProperty("token").GetString()!);
    }

    private HttpClient Machine(string token)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Device-Token", token);
        return c;
    }

    [Fact]
    public async Task Machine_punches_become_check_in_and_check_out()
    {
        var (admin, emp, token) = await SetupAsync("Bio Punch Co");
        var day = Today.AddDays(-1).ToString("yyyy-MM-dd");

        var result = await Ok(await Machine(token).PostAsJsonAsync("/api/biometric/punches", new
        {
            punches = new object[]
            {
                new { punchCode = "1001", time = $"{day}T09:02:00" },
                new { punchCode = "1001", time = $"{day}T09:03:00" },
                new { punchCode = "1001", time = $"{day}T18:05:00" },
                new { punchCode = "9999", time = $"{day}T09:00:00" },
            },
        }, ApiFactory.Json));
        Assert.Equal(3, result.GetProperty("accepted").GetInt32());
        Assert.Single(result.GetProperty("skipped").EnumerateArray());

        var daily = await Ok(await admin.GetAsync($"/api/attendance/daily?date={day}"));
        var row = daily.EnumerateArray().Single(r => r.GetProperty("employeeId").GetGuid() == emp);
        var record = row.GetProperty("record");
        Assert.Equal("Biometric", record.GetProperty("source").GetString());
        Assert.NotEqual(JsonValueKind.Null, record.GetProperty("checkOut").ValueKind);

        var devices = await Ok(await admin.GetAsync("/api/biometric/devices"));
        Assert.Equal(3, devices.EnumerateArray().Single().GetProperty("punchesReceived").GetInt32());
    }

    [Fact]
    public async Task A_wrong_or_replaced_token_is_refused()
    {
        var (admin, _, token) = await SetupAsync("Bio Token Co");
        var body = new { punches = new[] { new { punchCode = "1001", time = DateTime.UtcNow } } };

        Assert.Equal(HttpStatusCode.Unauthorized, (await Machine("qvd_wrong").PostAsJsonAsync("/api/biometric/punches", body, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/biometric/punches", body, ApiFactory.Json)).StatusCode);

        var id = (await Ok(await admin.GetAsync("/api/biometric/devices"))).EnumerateArray().Single().GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"/api/biometric/devices/{id}/token", null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Machine(token).PostAsJsonAsync("/api/biometric/punches", body, ApiFactory.Json)).StatusCode);
    }
}
