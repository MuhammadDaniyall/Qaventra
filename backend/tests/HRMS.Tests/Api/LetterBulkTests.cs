using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>One letter issued to many people, with placeholders filled for each.</summary>
public class LetterBulkTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Same_letter_goes_to_many_people()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("BulkLetters");
        var ids = new List<Guid>();
        foreach (var name in new[] { "Huma", "Imad", "Jalal" })
            ids.Add((await (await admin.PostAsJsonAsync("/api/employees", new { firstName = name, lastName = "B", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json))
                .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid());

        var res = await admin.PostAsJsonAsync("/api/letters/bulk", new
        {
            employeeIds = ids, category = "Other", subject = "Office move", body = "Dear {{EmployeeName}}, we move on Monday.", shareWithEmployee = true,
        }, ApiFactory.Json);
        res.EnsureSuccessStatusCode();
        Assert.Equal(3, (await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("issued").GetInt32());

        var list = await (await admin.GetAsync("/api/letters?search=&category=")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        var letters = list.EnumerateArray().Where(l => l.GetProperty("subject").GetString() == "Office move").ToList();
        Assert.Equal(3, letters.Count);
        Assert.Equal(3, letters.Select(l => l.GetProperty("referenceNo").GetString()).Distinct().Count());
        var one = await (await admin.GetAsync($"/api/letters/{letters[0].GetProperty("id").GetGuid()}")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        Assert.Matches("Dear (Huma|Imad|Jalal) B,", one.GetProperty("body").GetString());
    }
}
