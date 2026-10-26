using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Replies to ticket emails land on the ticket as the sender, and only from someone who may reply.</summary>
public class HelpdeskEmailReplyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private async Task<(HttpClient Admin, Func<string, Task<(Guid Id, string Email, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, string, HttpClient)> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "M", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant().Replace(' ', '-')}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Mail-Pass-1234", role = "Employee" }));
            return (id, email, await factory.SignInAsync(email, "Mail-Pass-1234"));
        }
        return (admin, Person);
    }

    private Task<InboundResult> Receive(string from, string subject, string text, string? authentication = null, bool automatic = false) =>
        factory.Services.GetRequiredService<HelpdeskInbox>().ProcessAsync(new InboundEmail(from, subject, text, null, authentication, automatic));

    [Fact]
    public async Task An_email_reply_is_added_to_the_ticket_as_the_sender()
    {
        var (admin, person) = await CompanyAsync("Mail Desk");
        var (agent, agentEmail, agentClient) = await person("Ian");
        var (_, workerEmail, workerClient) = await person("Wali");
        var (_, outsiderEmail, _) = await person("Sara");
        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "IT support", targetHours = 8, agentIds = new[] { agent } }));
        var category = Assert.Single((await Ok(await workerClient.GetAsync("/api/helpdesk/categories"))).EnumerateArray()).GetProperty("id").GetGuid();
        var ticket = await Ok(await workerClient.PostAsJsonAsync("/api/helpdesk/tickets", new { categoryId = category, subject = "VPN drops", description = "Every 10 minutes." }, ApiFactory.Json));
        var id = ticket.GetProperty("id").GetGuid();
        var code = ticket.GetProperty("code").GetString();

        // The agent answers by email; the quoted part of the email is left out.
        Assert.Equal(InboundResult.Added, await Receive(agentEmail.ToUpperInvariant(), $"Re: New IT support ticket {code}: VPN drops",
            "Please update FortiClient to 7.4 and try again.\n\nOn Mon, 26 Oct 2026 at 10:00, Qaventra <hr@qaventra.test> wrote:\n> New ticket"));
        var detail = await Ok(await workerClient.GetAsync($"/api/helpdesk/tickets/{id}"));
        var reply = detail.GetProperty("comments").EnumerateArray().Last(c => !c.GetProperty("isSystem").GetBoolean());
        Assert.Equal("Please update FortiClient to 7.4 and try again.", reply.GetProperty("body").GetString());
        Assert.Equal(agent, reply.GetProperty("authorEmployeeId").GetGuid());
        Assert.Equal("InProgress", detail.GetProperty("ticket").GetProperty("status").GetString());
        Assert.Contains("FortiClient", (await Ok(await workerClient.GetAsync("/api/notifications"))).ToString());

        // The employee answers back the same way.
        Assert.Equal(InboundResult.Added, await Receive(workerEmail, $"RE: Reply on {code}: VPN drops", "Updated, it works now. Thanks!"));
        Assert.Contains((await Ok(await agentClient.GetAsync($"/api/helpdesk/tickets/{id}"))).GetProperty("comments").EnumerateArray(),
            c => c.GetProperty("body").GetString() == "Updated, it works now. Thanks!");

        // Nobody else gets in: not a colleague who can't see the ticket, a stranger, a forged sender or an auto-reply.
        Assert.Equal(InboundResult.Refused, await Receive(outsiderEmail, $"Re: {code}", "Me too"));
        Assert.Equal(InboundResult.UnknownSender, await Receive("someone@elsewhere.test", $"Re: {code}", "Hello"));
        Assert.Equal(InboundResult.FailedAuthentication, await Receive(workerEmail, $"Re: {code}", "Hi", "mx.google.com; spf=fail smtp.mailfrom=x; dmarc=fail"));
        Assert.Equal(InboundResult.Automatic, await Receive(workerEmail, $"Out of office: {code}", "I'm away"));
        Assert.Equal(InboundResult.NoTicketCode, await Receive(workerEmail, "Hello HR", "Question"));
        Assert.Equal(InboundResult.Empty, await Receive(workerEmail, $"Re: {code}", "> only the quoted text"));

        // Ticket numbers are per company: someone from another company can't reach this one.
        var (_, otherPerson) = await CompanyAsync("Other Desk");
        var (_, otherEmail, _) = await otherPerson("Omar");
        Assert.Equal(InboundResult.TicketNotFound, await Receive(otherEmail, $"Re: {code}", "Hi"));

        var comments = (await Ok(await agentClient.GetAsync($"/api/helpdesk/tickets/{id}"))).GetProperty("comments").EnumerateArray().Count(c => !c.GetProperty("isSystem").GetBoolean());
        Assert.Equal(2, comments);
    }

    [Theory]
    [InlineData("Thanks!\n\n-- \nAli Raza\nSystems", "Thanks!")]
    [InlineData("Done\r\n\r\n-----Original Message-----\r\nFrom: HR", "Done")]
    [InlineData("Ok\n________________________________\nFrom: Qaventra", "Ok")]
    [InlineData("Line one\nLine two\n> quoted", "Line one\nLine two")]
    public void Reply_text_drops_quotes_and_signatures(string text, string expected) =>
        Assert.Equal(expected, HelpdeskInbox.ReplyText(text));

    [Fact]
    public void Ticket_codes_are_found_in_subjects()
    {
        Assert.Equal("HD-0042", HelpdeskInbox.TicketCode("Re: Reply on hd-0042: Laptop"));
        Assert.Null(HelpdeskInbox.TicketCode("Re: Laptop"));
    }
}
