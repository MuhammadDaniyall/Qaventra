using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HRMS.Tests.Api;

/// <summary>Many CVs at once become candidates, with the email and phone read from each file.</summary>
public class BulkCvTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static byte[] Docx(string text)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task Cvs_become_candidates()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("BulkCv");
        var post = (await (await admin.PostAsJsonAsync("/api/recruitment/jobs", new { title = "Accountant", employmentType = "FullTime", description = "Books", positions = 1, showSalary = false }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var email = $"ali.raza+{Guid.NewGuid():N}@cvmail.test";
        var form = new MultipartFormDataContent();
        void Add(string name, byte[] data)
        {
            var file = new ByteArrayContent(data);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            form.Add(file, "files", name);
        }
        Add("Ali_Raza_CV.docx", Docx($"Ali Raza. Email: {email}. Phone 0300-1234567. 6 years of accounting."));
        Add("No_Email_Resume.docx", Docx("Experienced clerk, call me."));

        var res = await admin.PostAsync($"/api/recruitment/jobs/{post}/candidates/bulk", form);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var rows = (await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).EnumerateArray().ToList();
        var ali = rows.Single(r => r.GetProperty("file").GetString() == "Ali_Raza_CV.docx");
        Assert.True(ali.GetProperty("added").GetBoolean());
        Assert.Equal("Ali Raza", ali.GetProperty("name").GetString());
        Assert.False(rows.Single(r => r.GetProperty("file").GetString() == "No_Email_Resume.docx").GetProperty("added").GetBoolean());

        var scores = (await (await admin.GetAsync($"/api/recruitment-extras/posts/{post}/scores")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).EnumerateArray().Single();
        Assert.Equal("Ali Raza", scores.GetProperty("candidate").GetString());
    }
}
