using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AMiracle.Echo.Abstractions.Email;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Server.Endpoints;
using AMiracle.Echo.Server.Services;

namespace AMiracle.Echo.Tests;

public class ReplyTests
{
    private static Task<HttpResponseMessage> ReplyAsync(EchoTestHost host, Guid id, object body)
        => host.Client.SendAsync(host.Admin(HttpMethod.Post, $"/feedbacks/{id}/reply", body));

    [Fact]
    public async Task Sends_the_reply_records_it_and_can_resolve()
    {
        await using var host = await EchoTestHost.StartAsync(email: new FakeEmailSender());
        var project = await host.CreateProjectAsync("CoFounder");
        var id = await host.SubmitOkAsync(project, new
        {
            type = "text", text = "The export button does nothing.",
            submitter = new { id = "u1", email = "ann@example.com", name = "Ann" },
        });

        using var res = await ReplyAsync(host, id, new { message = "Fixed — thanks for telling us!", resolve = true });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var mail = Assert.Single(host.Email!.Sent);
        Assert.Equal("ann@example.com", mail.To);
        Assert.Equal("Re: your feedback on CoFounder", mail.Subject);
        Assert.Contains("Hi Ann,", mail.TextBody);
        Assert.Contains("Fixed — thanks for telling us!", mail.TextBody);
        Assert.Contains("> The export button does nothing.", mail.TextBody);

        Assert.Equal(FeedbackStatus.Resolved, (await host.GetFeedbackAsync(id)).Status);

        var comments = await host.AdminAsync<List<FeedbackComment>>(HttpMethod.Get, $"/feedbacks/{id}/comments");
        var record = Assert.Single(comments);
        Assert.Equal(AdminEndpoints.ReplyCommentAuthor, record.Author);
        Assert.Contains("ann@example.com", record.Body);
        Assert.Contains("Fixed — thanks for telling us!", record.Body);
    }

    [Fact]
    public async Task Leaves_status_alone_unless_asked()
    {
        await using var host = await EchoTestHost.StartAsync(email: new FakeEmailSender());
        var project = await host.CreateProjectAsync();
        var id = await host.SubmitOkAsync(project, new { type = "text", text = "q", submitter = new { email = "a@example.com" } });
        using var res = await ReplyAsync(host, id, new { message = "Looking into it." });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(FeedbackStatus.New, (await host.GetFeedbackAsync(id)).Status);
    }

    [Fact]
    public async Task Tells_the_admin_whether_the_address_was_signed()
    {
        await using var host = await EchoTestHost.StartAsync(identitySigningKey: "k-0123456789abcdef", email: new FakeEmailSender());
        var project = await host.CreateProjectAsync();
        var secret = host.Service<IdentityVerifier>().ProjectSecret(project.Id)!;
        var signed = await host.SubmitOkAsync(project, new { type = "text", text = "a", submitter = new { id = "u1", email = "a@example.com", hash = IdentityVerifier.Sign(secret, "u1") } });
        var unsigned = await host.SubmitOkAsync(project, new { type = "text", text = "b", submitter = new { email = "b@example.com" } });

        using var r1 = await ReplyAsync(host, signed, new { message = "hi" });
        using var r2 = await ReplyAsync(host, unsigned, new { message = "hi" });
        Assert.True((await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("verified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("verified").ValueKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-email")]
    [InlineData("a@example.com, b@example.com")]
    [InlineData("a@example.com\r\nBcc: everyone@example.com")]
    [InlineData("Mallory <m@example.com>")]
    public async Task Refuses_feedback_without_one_plain_address(string? email)
    {
        await using var host = await EchoTestHost.StartAsync(email: new FakeEmailSender());
        var project = await host.CreateProjectAsync();
        var id = await host.SubmitOkAsync(project, new { type = "text", text = "x", submitter = new { id = "u", email } });
        using var res = await ReplyAsync(host, id, new { message = "hi" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Empty(host.Email!.Sent);
    }

    [Fact]
    public async Task Says_501_when_email_is_not_configured()
    {
        await using var host = await EchoTestHost.StartAsync();
        var project = await host.CreateProjectAsync();
        var id = await host.SubmitOkAsync(project, new { type = "text", text = "x", submitter = new { email = "a@example.com" } });
        using var res = await ReplyAsync(host, id, new { message = "hi" });
        Assert.Equal(HttpStatusCode.NotImplemented, res.StatusCode);

        var caps = await host.AdminAsync<JsonElement>(HttpMethod.Get, "/capabilities");
        Assert.False(caps.GetProperty("emailReplies").GetBoolean());
    }

    [Fact]
    public async Task A_provider_failure_is_a_502_and_records_nothing()
    {
        var email = new FakeEmailSender { FailWith = new EchoEmailException("Resend refused the message (422)") };
        await using var host = await EchoTestHost.StartAsync(email: email);
        var project = await host.CreateProjectAsync();
        var id = await host.SubmitOkAsync(project, new { type = "text", text = "x", submitter = new { email = "a@example.com" } });

        using var res = await ReplyAsync(host, id, new { message = "hi", resolve = true });
        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
        Assert.Empty(await host.AdminAsync<List<FeedbackComment>>(HttpMethod.Get, $"/feedbacks/{id}/comments"));
        Assert.Equal(FeedbackStatus.New, (await host.GetFeedbackAsync(id)).Status);
    }

    [Fact]
    public async Task Rejects_an_empty_message_and_unknown_feedback()
    {
        await using var host = await EchoTestHost.StartAsync(email: new FakeEmailSender());
        var project = await host.CreateProjectAsync();
        var id = await host.SubmitOkAsync(project, new { type = "text", text = "x", submitter = new { email = "a@example.com" } });
        using var empty = await ReplyAsync(host, id, new { message = "   " });
        using var missing = await ReplyAsync(host, Guid.NewGuid(), new { message = "hi" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Requires_the_admin_token()
    {
        await using var host = await EchoTestHost.StartAsync(email: new FakeEmailSender());
        using var res = await host.Client.PostAsJsonAsync($"/api/v1/admin/feedbacks/{Guid.NewGuid()}/reply", new { message = "hi" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Configured_providers_are_picked_from_options()
    {
        await using var host = await EchoTestHost.StartAsync(configure: o =>
        {
            o.Email.Provider = "resend";
            o.Email.From = "Echo <echo@example.com>";
            o.Email.Resend.ApiKey = "re_test";
        });
        var caps = await host.AdminAsync<JsonElement>(HttpMethod.Get, "/capabilities");
        Assert.True(caps.GetProperty("emailReplies").GetBoolean());
    }
}
