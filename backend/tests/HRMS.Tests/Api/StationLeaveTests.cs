using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Station leave needs the address where the person will be, and shows it on the request.</summary>
public class StationLeaveTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Station_leave_keeps_the_address()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Station");
        var emp = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = "Saad", lastName = "S", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var type = (await (await admin.GetAsync("/api/setup/leave-types")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json))
            .EnumerateArray().First(t => t.GetProperty("code").GetString() == "CL").GetProperty("id").GetGuid();
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(10);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        var iso = day.ToString("yyyy-MM-dd");

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/leave",
            new { leaveTypeId = type, fromDate = iso, toDate = iso, reason = "Wedding", employeeId = emp, isStationLeave = true }, ApiFactory.Json)).StatusCode);
        var res = await admin.PostAsJsonAsync("/api/leave",
            new { leaveTypeId = type, fromDate = iso, toDate = iso, reason = "Wedding", employeeId = emp, isStationLeave = true, stationAddress = "House 5, Multan", stationPhone = "0300-1234567" },
            ApiFactory.Json);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var leave = await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        Assert.True(leave.GetProperty("isStationLeave").GetBoolean());
        Assert.Equal("House 5, Multan", leave.GetProperty("stationAddress").GetString());
    }
}
