using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Asset inventory, hand-over and take-over, requests through approval, starter kits and overdue loans.</summary>
public class AssetTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Func<string, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "A", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Asset-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Asset-Pass-1234"));
        }
        return (admin, Person);
    }

    private static async Task<Guid> TypeAsync(HttpClient admin, string name, Guid[]? handlers = null, bool multiple = false)
    {
        await Ok(await admin.PostAsJsonAsync("/api/assets/types", new { name, allowMultiple = multiple, returnOnExit = true, handlerIds = handlers ?? [] }));
        return (await Ok(await admin.GetAsync("/api/assets/types"))).EnumerateArray().First(t => t.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> AddAsync(HttpClient c, Guid type, string name, string? serial = null) =>
        await Ok(await c.PostAsJsonAsync("/api/assets", new { typeId = type, name, serialNumber = serial, condition = "New" }, ApiFactory.Json));

    [Fact]
    public async Task Handlers_hand_over_and_take_back_assets()
    {
        var (admin, person) = await CompanyAsync("Inventory");
        var (ian, ianClient) = await person("Ian");
        var (wali, waliClient) = await person("Wali");
        var laptop = await TypeAsync(admin, "Laptop", [ian]);

        var l1 = await AddAsync(ianClient, laptop, "Dell Latitude 5420", "SN-1");
        Assert.Equal("AST-0001", l1.GetProperty("tag").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await ianClient.PostAsJsonAsync("/api/assets", new { typeId = laptop, name = "Dup", serialNumber = "SN-1", condition = "New" }, ApiFactory.Json)).StatusCode);
        var l2 = await AddAsync(ianClient, laptop, "HP ProBook 450");
        var (id1, id2) = (l1.GetProperty("id").GetGuid(), l2.GetProperty("id").GetGuid());

        // Only handlers and HR see and manage the inventory.
        Assert.Empty((await Ok(await waliClient.GetAsync("/api/assets"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await waliClient.PostAsJsonAsync($"/api/assets/{id1}/assign", new { employeeId = wali, assignedOn = Iso(Today), condition = "Good" }, ApiFactory.Json)).StatusCode);

        await Ok(await ianClient.PostAsJsonAsync($"/api/assets/{id1}/assign", new { employeeId = wali, assignedOn = Iso(Today), condition = "New", notes = "With charger" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await ianClient.PostAsJsonAsync($"/api/assets/{id2}/assign", new { employeeId = wali, assignedOn = Iso(Today), condition = "Good" }, ApiFactory.Json)).StatusCode);
        var mine = await Ok(await waliClient.GetAsync("/api/assets/my"));
        var holding = Assert.Single(mine.GetProperty("holding").EnumerateArray());
        Assert.Equal("AST-0001", holding.GetProperty("tag").GetString());
        Assert.Contains("handed over to you", (await Ok(await waliClient.GetAsync("/api/notifications"))).ToString());

        // Back damaged: goes to repair, with the history kept.
        await Ok(await ianClient.PostAsJsonAsync($"/api/assets/assignments/{holding.GetProperty("id").GetGuid()}/return", new { returnedOn = Iso(Today), condition = "Damaged", notes = "Cracked screen" }, ApiFactory.Json));
        var detail = await Ok(await ianClient.GetAsync($"/api/assets/{id1}"));
        Assert.Equal("InRepair", detail.GetProperty("asset").GetProperty("status").GetString());
        Assert.Equal("Damaged", Assert.Single(detail.GetProperty("history").EnumerateArray()).GetProperty("conditionIn").GetString());

        // Lost while with someone: their assignment ends.
        await Ok(await ianClient.PostAsJsonAsync($"/api/assets/{id2}/assign", new { employeeId = wali, assignedOn = Iso(Today), condition = "Good" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await ianClient.PostAsJsonAsync($"/api/assets/{id2}/status", new { status = "Retired" }, ApiFactory.Json)).StatusCode);
        await Ok(await ianClient.PostAsJsonAsync($"/api/assets/{id2}/status", new { status = "Lost", note = "Left in a taxi" }, ApiFactory.Json));
        Assert.Empty((await Ok(await waliClient.GetAsync("/api/assets/my"))).GetProperty("holding").EnumerateArray());
        var summary = await Ok(await admin.GetAsync("/api/assets/summary"));
        Assert.Equal(1, summary.GetProperty("lost").GetInt32());
        Assert.Equal(1, summary.GetProperty("inRepair").GetInt32());
    }

    [Fact]
    public async Task Requests_are_approved_then_fulfilled_and_returns_go_straight_to_the_handler()
    {
        var (admin, person) = await CompanyAsync("AssetReq");
        var (wali, waliClient) = await person("Wali");
        var sim = await TypeAsync(admin, "SIM", multiple: true);
        var phone = await TypeAsync(admin, "Phone");
        var s1 = (await AddAsync(admin, sim, "Jazz 0300-1111111")).GetProperty("id").GetGuid();
        var p1 = (await AddAsync(admin, phone, "Samsung A15")).GetProperty("id").GetGuid();

        var req = await Ok(await waliClient.PostAsJsonAsync("/api/assets/requests/new", new { typeId = sim, reason = "Client calls", returnBy = Iso(Today.AddDays(30)) }, ApiFactory.Json));
        Assert.Equal("Pending", req.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await waliClient.PostAsJsonAsync("/api/assets/requests/new", new { typeId = sim, reason = "again" }, ApiFactory.Json)).StatusCode);
        var id = req.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/assets/requests/{id}/fulfill", new { assetId = s1, on = Iso(Today), condition = "New" }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/assets/requests/{id}/approve", new { }));
        Assert.Single((await Ok(await admin.GetAsync("/api/assets/requests"))).EnumerateArray());

        // Fulfil with the right kind of asset; a temporary loan keeps its return date.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/assets/requests/{id}/fulfill", new { assetId = p1, on = Iso(Today), condition = "New" }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/assets/requests/{id}/fulfill", new { assetId = s1, on = Iso(Today), condition = "New" }, ApiFactory.Json));
        var mine = await Ok(await waliClient.GetAsync("/api/assets/my"));
        var held = Assert.Single(mine.GetProperty("holding").EnumerateArray());
        Assert.Equal(Iso(Today.AddDays(30)), held.GetProperty("dueBack").GetString());
        Assert.Equal("Fulfilled", mine.GetProperty("requests")[0].GetProperty("status").GetString());

        // Return: no approval, the handler takes it back.
        var ret = await Ok(await waliClient.PostAsJsonAsync("/api/assets/requests/return", new { assignmentId = held.GetProperty("id").GetGuid(), reason = "Project over" }));
        Assert.Equal("Approved", ret.GetProperty("status").GetString());
        Assert.True((await Ok(await waliClient.GetAsync("/api/assets/my"))).GetProperty("holding")[0].GetProperty("returnRequested").GetBoolean());
        await Ok(await admin.PostAsJsonAsync($"/api/assets/requests/{ret.GetProperty("id").GetGuid()}/fulfill", new { on = Iso(Today), condition = "Good" }, ApiFactory.Json));
        Assert.Empty((await Ok(await waliClient.GetAsync("/api/assets/my"))).GetProperty("holding").EnumerateArray());
        Assert.Equal("Available", (await Ok(await admin.GetAsync($"/api/assets/{s1}"))).GetProperty("asset").GetProperty("status").GetString());
        _ = wali;
    }

    [Fact]
    public async Task Kits_queue_hand_overs_and_late_loans_show_as_overdue()
    {
        var (admin, person) = await CompanyAsync("Kits");
        var (ian, ianClient) = await person("Ian");
        var (newbie, newbieClient) = await person("Nida");
        var laptop = await TypeAsync(admin, "Laptop", [ian]);
        var card = await TypeAsync(admin, "Access card");
        await Ok(await admin.PostAsJsonAsync("/api/assets/kits", new { name = "Developer", typeIds = new[] { laptop, card } }));
        var kit = (await Ok(await admin.GetAsync("/api/assets/kits")))[0].GetProperty("id").GetGuid();

        Assert.Equal(2, (await Ok(await admin.PostAsync($"/api/assets/kits/{kit}/apply/{newbie}", null))).GetProperty("requests").GetInt32());
        Assert.Equal(0, (await Ok(await admin.PostAsync($"/api/assets/kits/{kit}/apply/{newbie}", null))).GetProperty("requests").GetInt32());
        Assert.Equal(2, (await Ok(await admin.GetAsync("/api/assets/requests"))).GetArrayLength());
        Assert.Equal("Laptop", Assert.Single((await Ok(await ianClient.GetAsync("/api/assets/requests"))).EnumerateArray()).GetProperty("type").GetString());
        Assert.Contains("Hand over a laptop to Nida A", (await Ok(await ianClient.GetAsync("/api/notifications"))).ToString());

        // A temporary loan past its date is overdue.
        var l = (await AddAsync(ianClient, laptop, "Loaner")).GetProperty("id").GetGuid();
        await Ok(await ianClient.PostAsJsonAsync($"/api/assets/{l}/assign", new { employeeId = newbie, assignedOn = Iso(Today.AddDays(-10)), dueBack = Iso(Today.AddDays(-2)), condition = "Good" }, ApiFactory.Json));
        Assert.True((await Ok(await newbieClient.GetAsync("/api/assets/my"))).GetProperty("holding")[0].GetProperty("isOverdue").GetBoolean());
        Assert.Equal(1, (await Ok(await ianClient.GetAsync("/api/assets/summary"))).GetProperty("overdueReturns").GetInt32());
        Assert.Single((await Ok(await ianClient.GetAsync("/api/assets?overdue=true"))).EnumerateArray());
        var forHr = await Ok(await admin.GetAsync($"/api/assets/employees/{newbie}"));
        Assert.Single(forHr.GetProperty("holding").EnumerateArray());
        Assert.Equal(2, forHr.GetProperty("open").GetArrayLength());
        var xlsx = await admin.GetAsync("/api/assets/export");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
    }
}
