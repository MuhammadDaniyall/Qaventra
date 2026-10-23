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
}
