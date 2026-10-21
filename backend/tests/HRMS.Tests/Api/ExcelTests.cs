using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;

namespace HRMS.Tests.Api;

/// <summary>Excel templates, checked imports (all-or-nothing) and exports.</summary>
public class ExcelTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static byte[] Sheet(string[] headers, params object?[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Data");
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, string kind, byte[] xlsx, bool commit, bool createMissing = false)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "upload.xlsx");
        form.Add(new StringContent(createMissing ? "true" : "false"), "createMissing");
        return client.PostAsync($"/api/imports/{kind}?commit={commit}", form);
    }

    private static readonly string[] EmployeeHeaders = ["First name*", "Last name*", "Joining date*", "Employee code", "Email", "CNIC", "Gender", "Department", "Designation", "Manager code", "Basic salary"];

    [Fact]
    public async Task Employees_import_is_checked_row_by_row_and_all_or_nothing()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Bulk");

        var template = await admin.GetAsync("/api/imports/employees/template");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", template.Content.Headers.ContentType?.MediaType);
        using (var wb = new XLWorkbook(await template.Content.ReadAsStreamAsync()))
        {
            Assert.Equal("First name*", wb.Worksheet(1).Cell(1, 1).GetString());
            Assert.True(wb.Worksheet(1).Cell(2, 1).IsEmpty(), "The data sheet must not contain an example row.");
        }

        var bad = Sheet(EmployeeHeaders,
            ["Sara", "Lead", "01-Sep-2026", "T-100", "sara@bulk.test", "35202-1234567-1", "Female", "Platform", "Engineering Manager", null, 400000],
            ["Omar", "Dev", "2026-09-15", "T-101", "not-an-email", "12345", "Robot", "Platform", "Developer", "T-100", "abc"],
            ["Nida", "Dev", "15/09/2026", null, null, null, "Female", "Platform", "Developer", "T-999", 150000]);
        var check = await Ok(await Upload(admin, "employees", bad, commit: false));
        Assert.Equal(3, check.GetProperty("total").GetInt32());
        Assert.Equal(0, check.GetProperty("valid").GetInt32());
        var omar = check.GetProperty("rows")[1];
        Assert.Equal(3, omar.GetProperty("row").GetInt32());
        var errors = omar.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(errors, e => e!.Contains("Email"));
        Assert.Contains(errors, e => e!.Contains("CNIC"));
        Assert.Contains(errors, e => e!.Contains("Gender"));
        Assert.Contains(errors, e => e!.Contains("Basic salary"));
        Assert.Contains(errors, e => e!.Contains("Platform") || e!.Contains("Department"));

        // Committing a file with problems saves nothing.
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(admin, "employees", bad, commit: true)).StatusCode);
        Assert.Equal(0, (await Ok(await admin.GetAsync("/api/employees?search=T-10"))).GetProperty("total").GetInt32());

        var good = Sheet(EmployeeHeaders,
            ["Sara", "Lead", "01-Sep-2026", "T-100", "sara@bulk.test", "35202-1234567-1", "Female", "Platform", "Engineering Manager", null, 400000],
            ["Omar", "Dev", "2026-09-15", "T-101", "omar@bulk.test", "35202-7654321-2", "Male", "Platform", "Developer", "T-100", 180000],
            ["Nida", "Dev", "15/09/2026", null, null, null, "Female", "Platform", "Developer", "T-100", 150000]);
        var preview = await Ok(await Upload(admin, "employees", good, commit: false, createMissing: true));
        Assert.Equal(3, preview.GetProperty("valid").GetInt32());
        Assert.Contains(preview.GetProperty("rows")[0].GetProperty("notes").EnumerateArray(), n => n.GetString()!.Contains("department"));

        var done = await Ok(await Upload(admin, "employees", good, commit: true, createMissing: true));
        Assert.True(done.GetProperty("committed").GetBoolean());
        Assert.Equal(3, done.GetProperty("created").GetInt32());

        var list = await Ok(await admin.GetAsync("/api/employees?search=Dev&pageSize=50"));
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        var omarRow = list.GetProperty("items").EnumerateArray().First(e => e.GetProperty("employeeCode").GetString() == "T-101");
        var omarDetail = await Ok(await admin.GetAsync($"/api/employees/{omarRow.GetProperty("id").GetGuid()}"));
        Assert.Equal("Sara Lead", omarDetail.GetProperty("manager").GetString());
        Assert.Equal("Platform", omarDetail.GetProperty("department").GetString());
        var salary = await Ok(await admin.GetAsync($"/api/employees/{omarRow.GetProperty("id").GetGuid()}/salary"));
        Assert.Equal(180000m, salary.GetProperty("basicSalary").GetDecimal());

        // The same codes again are now duplicates.
        var again = await Ok(await Upload(admin, "employees", good, commit: false, createMissing: true));
        Assert.Contains(again.GetProperty("rows")[0].GetProperty("errors").EnumerateArray(), e => e.GetString()!.Contains("already exists"));

        // Export round-trip: the employees appear in the download.
        var export = await admin.GetAsync("/api/exports/employees");
        using var exported = new XLWorkbook(await export.Content.ReadAsStreamAsync());
        var codes = exported.Worksheet(1).Column(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("T-100", codes);
        Assert.Contains("T-101", codes);
    }

    [Fact]
    public async Task Attendance_import_creates_then_replaces_and_rejects_bad_rows()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Punch");
        var emp = await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Punch", lastName = "Card", joiningDate = "2025-01-01", status = "Active", employeeCode = "P-1" }, ApiFactory.Json));
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-3).ToString("yyyy-MM-dd");
        var day2 = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-2).ToString("yyyy-MM-dd");
        string[] headers = ["Employee code*", "Date*", "Check in*", "Check out"];

        var bad = Sheet(headers, ["P-1", day, "9:05", "8:00"], ["NOPE", day, "09:00", "18:00"], ["P-1", "2999-01-01", "09:00", null]);
        var check = await Ok(await Upload(admin, "attendance", bad, commit: false));
        Assert.Equal(0, check.GetProperty("valid").GetInt32());

        var good = Sheet(headers, ["P-1", day, "09:05", "18:10"], ["P-1", day2, "9:40 AM", null]);
        var done = await Ok(await Upload(admin, "attendance", good, commit: true));
        Assert.Equal(2, done.GetProperty("created").GetInt32());

        var replace = await Ok(await Upload(admin, "attendance", Sheet(headers, ["P-1", day, "10:00", "19:00"]), commit: true));
        Assert.Equal(1, replace.GetProperty("updated").GetInt32());

        var from = day; var to = day2;
        var export = await admin.GetAsync($"/api/exports/attendance?from={from}&to={to}");
        using var wb = new XLWorkbook(await export.Content.ReadAsStreamAsync());
        var ws = wb.Worksheet(1);
        Assert.Equal(3, ws.RowsUsed().Count());
        Assert.Equal("10:00", ws.Cell(2, 5).GetFormattedString());

        var worker = emp.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, worker);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/exports/attendance?from={to}&to={from}")).StatusCode);
    }
}
