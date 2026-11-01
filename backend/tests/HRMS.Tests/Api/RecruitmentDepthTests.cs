using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Shortlisting scores and the candidate status page.</summary>
public class RecruitmentDepthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Scores_and_candidate_portal()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Hiring2");
        var post = await Ok(await admin.PostAsJsonAsync("/api/recruitment/jobs", new
        {
            title = "Developer", employmentType = "FullTime", description = "Build things", positions = 1, showSalary = false,
        }, ApiFactory.Json));
        var postId = post.GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/recruitment-extras/posts/{postId}/criteria", new { minExperienceYears = 3, keywords = "react, dotnet" }, ApiFactory.Json));
        var form = new MultipartFormDataContent
        {
            { new StringContent("Sara"), "FirstName" }, { new StringContent("K"), "LastName" },
            { new StringContent($"sara+{Guid.NewGuid():N}@cand.test"), "Email" }, { new StringContent("5"), "ExperienceYears" },
            { new StringContent("React, Node"), "Skills" },
        };
        await Ok(await admin.PostAsync($"/api/recruitment/jobs/{postId}/candidates", form));
        var scores = (await Ok(await admin.GetAsync($"/api/recruitment-extras/posts/{postId}/scores"))).EnumerateArray().Single();
        Assert.Equal(30 + 20 + 10 + 20, scores.GetProperty("score").GetInt32());

        // The status page works with the secret link and lets the candidate withdraw.
        var appId = scores.GetProperty("applicationId").GetGuid();
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var c = await db.Candidates.IgnoreQueryFilters().FirstAsync(x => x.Id == scores.GetProperty("candidateId").GetGuid());
            c.PortalToken = "testtoken" + Guid.NewGuid().ToString("N");
            token = c.PortalToken;
            await db.SaveChangesAsync();
        }
        var anon = factory.CreateClient();
        var page = await Ok(await anon.GetAsync($"/api/careers/portal/{token}"));
        Assert.Equal("Received", page.GetProperty("applications")[0].GetProperty("stage").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/careers/portal/not-a-real-token-123456")).StatusCode);
        await Ok(await anon.PostAsync($"/api/careers/portal/{token}/withdraw/{appId}", null));
        Assert.Equal("Withdrawn", (await Ok(await anon.GetAsync($"/api/careers/portal/{token}"))).GetProperty("applications")[0].GetProperty("stage").GetString());
    }

    [Fact]
    public async Task A_candidate_who_withdraws_after_accepting_cannot_be_hired()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Hiring3");
        var post = await Ok(await admin.PostAsJsonAsync("/api/recruitment/jobs", new
        {
            title = "Analyst", employmentType = "FullTime", description = "Analyse things", positions = 1, showSalary = false,
        }, ApiFactory.Json));
        var postId = post.GetProperty("id").GetGuid();
        var form = new MultipartFormDataContent
        {
            { new StringContent("Hina"), "FirstName" }, { new StringContent("A"), "LastName" }, { new StringContent($"hina+{Guid.NewGuid():N}@cand.test"), "Email" },
        };
        await Ok(await admin.PostAsync($"/api/recruitment/jobs/{postId}/candidates", form));
        var card = (await Ok(await admin.GetAsync($"/api/recruitment/jobs/{postId}/pipeline"))).EnumerateArray().Single();
        var appId = card.GetProperty("id").GetGuid();
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var c = await db.Candidates.IgnoreQueryFilters().FirstAsync(x => x.Id == card.GetProperty("candidateId").GetGuid());
            c.PortalToken = "testtoken" + Guid.NewGuid().ToString("N");
            token = c.PortalToken;
            await db.SaveChangesAsync();
        }
        var joining = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14).ToString("yyyy-MM-dd");
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/offer", new { salary = 150000, joiningDate = joining, emailCandidate = false }, ApiFactory.Json));
        var anon = factory.CreateClient();
        await Ok(await anon.PostAsJsonAsync($"/api/careers/portal/{token}/offer/{appId}", new { accepted = true }, ApiFactory.Json));
        await Ok(await anon.PostAsync($"/api/careers/portal/{token}/withdraw/{appId}", null));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/hire", new { }, ApiFactory.Json)).StatusCode);
        var app = (await Ok(await anon.GetAsync($"/api/careers/portal/{token}"))).GetProperty("applications")[0];
        Assert.Equal("Withdrawn", app.GetProperty("stage").GetString());
        Assert.Equal("Declined", app.GetProperty("offerStatus").GetString());
    }

    [Fact]
    public async Task A_candidate_moved_back_from_offer_cannot_be_hired()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Hiring4");
        var post = await Ok(await admin.PostAsJsonAsync("/api/recruitment/jobs", new
        {
            title = "Analyst", employmentType = "FullTime", description = "Analyse things", positions = 1, showSalary = false,
        }, ApiFactory.Json));
        var postId = post.GetProperty("id").GetGuid();
        var form = new MultipartFormDataContent
        {
            { new StringContent("Omar"), "FirstName" }, { new StringContent("B"), "LastName" }, { new StringContent($"omar+{Guid.NewGuid():N}@cand.test"), "Email" },
        };
        await Ok(await admin.PostAsync($"/api/recruitment/jobs/{postId}/candidates", form));
        var appId = (await Ok(await admin.GetAsync($"/api/recruitment/jobs/{postId}/pipeline"))).EnumerateArray().Single().GetProperty("id").GetGuid();
        var joining = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14).ToString("yyyy-MM-dd");
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/offer", new { salary = 150000, joiningDate = joining, emailCandidate = false }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/offer-response", new { accepted = true }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/move", new { stage = "Interview", emailCandidate = false }, ApiFactory.Json));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/hire", new { }, ApiFactory.Json)).StatusCode);
    }
}
