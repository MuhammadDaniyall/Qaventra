using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Loan types set limits, markup and a processing fee; markup is part of what is repaid.</summary>
public class LoanTypeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Markup_is_repaid_and_limits_hold()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("LoanTypes");
        var id = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = "Lal", lastName = "L", joiningDate = "2020-01-01", status = "Active" }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync("/api/pay/loan-types", new
        {
            name = "Car loan", maxAmount = 100000, maxInstallments = 12, markupPercent = 10, processingFee = 500, minServiceMonths = 12, isActive = true,
        }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var type = (await (await admin.GetAsync("/api/pay/loan-types")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json))[0].GetProperty("id").GetGuid();

        var next = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);
        object Loan(decimal amount, int n, Guid? t) => new { employeeId = id, title = "", amount, installments = n, startYear = next.Year, startMonth = next.Month, loanTypeId = t };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/pay/loans", Loan(60000, 6, null), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/pay/loans", Loan(150000, 6, type), ApiFactory.Json)).StatusCode);
        var res = await admin.PostAsJsonAsync("/api/pay/loans", Loan(60000, 6, type), ApiFactory.Json);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var loan = await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        Assert.Equal("Car loan", loan.GetProperty("title").GetString());
        Assert.Equal(6000m, loan.GetProperty("markup").GetDecimal());
        Assert.Equal(11000m, loan.GetProperty("installmentAmount").GetDecimal());
        Assert.Equal(66000m, loan.GetProperty("remaining").GetDecimal());
    }
}
