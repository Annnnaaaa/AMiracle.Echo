using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Server.Services;

namespace AMiracle.Echo.Tests;

public class HistoryTests
{
    private const string MasterKey = "test-master-key-0123456789";

    private static async Task<(Guid id, string receipt)> SubmitWithReceiptAsync(EchoTestHost host, Project project, object body)
    {
        using var res = await host.SubmitAsync(project, body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (created.GetProperty("id").GetGuid(), created.GetProperty("receiptToken").GetString()!);
    }

    private static async Task<JsonElement> HistoryAsync(EchoTestHost host, Project project, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/feedbacks/history") { Content = JsonContent.Create(body) };
        req.Headers.Add("X-Echo-Project-Key", project.PublicKey);
        req.Headers.Add("Origin", EchoTestHost.Origin);
        using var res = await host.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Guid[] Ids(JsonElement history)
        => history.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task A_receipt_returns_the_feedback_with_its_current_status()
    {
        await using var host = await EchoTestHost.StartAsync();
        var project = await host.CreateProjectAsync();
        var (id, receipt) = await SubmitWithReceiptAsync(host, project, new { type = "text", text = "the save button is broken" });

        await host.AdminAsync<JsonElement>(HttpMethod.Patch, "/feedbacks/" + id, new { status = "triaged" });

        var history = await HistoryAsync(host, project, new { receipts = new[] { new { id, token = receipt } } });
        var item = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("triaged", item.GetProperty("status").GetString());
        Assert.Equal("the save button is broken", item.GetProperty("text").GetString());
        Assert.Equal("none", history.GetProperty("identity").GetString());
    }

    [Fact]
    public async Task A_wrong_receipt_or_one_for_another_project_returns_nothing()
    {
        await using var host = await EchoTestHost.StartAsync();
        var mine = await host.CreateProjectAsync("Mine");
        var other = await host.CreateProjectAsync("Other");
        var (id, receipt) = await SubmitWithReceiptAsync(host, mine, new { type = "text", text = "hello" });

        var forged = await HistoryAsync(host, mine, new { receipts = new[] { new { id, token = receipt[..^2] + "xx" } } });
        Assert.Empty(Ids(forged));

        // A valid receipt doesn't leak across projects even though the token checks out.
        var crossProject = await HistoryAsync(host, other, new { receipts = new[] { new { id, token = receipt } } });
        Assert.Empty(Ids(crossProject));
    }

    [Fact]
    public async Task A_signed_identity_returns_all_of_that_users_feedback_in_the_project_only()
    {
        await using var host = await EchoTestHost.StartAsync(identitySigningKey: MasterKey);
        var project = await host.CreateProjectAsync("Mine");
        var other = await host.CreateProjectAsync("Other");
        var secret = (await host.AdminAsync<JsonElement>(HttpMethod.Get, $"/projects/{project.Id}/identity-secret"))
            .GetProperty("secret").GetString()!;
        var otherSecret = (await host.AdminAsync<JsonElement>(HttpMethod.Get, $"/projects/{other.Id}/identity-secret"))
            .GetProperty("secret").GetString()!;
        var me = new { id = "user-42", hash = IdentityVerifier.Sign(secret, "user-42") };

        var a = await host.SubmitOkAsync(project, new { type = "text", text = "one", submitter = me });
        var b = await host.SubmitOkAsync(project, new { type = "text", text = "two", submitter = me });
        await host.SubmitOkAsync(project, new { type = "text", text = "someone else", submitter = new { id = "user-7" } });
        await host.SubmitOkAsync(other, new { type = "text", text = "other project",
            submitter = new { id = "user-42", hash = IdentityVerifier.Sign(otherSecret, "user-42") } });

        var history = await HistoryAsync(host, project, new { submitter = me });
        Assert.Equal("verified", history.GetProperty("identity").GetString());
        Assert.Equal(new[] { b, a }, Ids(history)); // newest first
    }

    [Fact]
    public async Task An_unsigned_or_badly_signed_identity_lists_nothing_by_id()
    {
        await using var host = await EchoTestHost.StartAsync(identitySigningKey: MasterKey);
        var project = await host.CreateProjectAsync();
        await host.SubmitOkAsync(project, new { type = "text", text = "private", submitter = new { id = "user-42" } });

        var unsigned = await HistoryAsync(host, project, new { submitter = new { id = "user-42" } });
        Assert.Equal("none", unsigned.GetProperty("identity").GetString());
        Assert.Empty(Ids(unsigned));

        var forged = await HistoryAsync(host, project, new { submitter = new { id = "user-42", hash = new string('0', 64) } });
        Assert.Equal("invalid", forged.GetProperty("identity").GetString());
        Assert.Empty(Ids(forged));
    }

    [Fact]
    public async Task Email_replies_are_shown_but_internal_comments_are_not()
    {
        var email = new FakeEmailSender();
        await using var host = await EchoTestHost.StartAsync(email: email);
        var project = await host.CreateProjectAsync();
        var (id, receipt) = await SubmitWithReceiptAsync(host, project,
            new { type = "text", text = "question", submitter = new { email = "u@example.com" } });

        await host.AdminAsync<JsonElement>(HttpMethod.Post, $"/feedbacks/{id}/comments", new { author = "Aram", body = "internal note" });
        await host.AdminAsync<JsonElement>(HttpMethod.Post, $"/feedbacks/{id}/reply", new { message = "Fixed in 2.1, thanks!" });

        var history = await HistoryAsync(host, project, new { receipts = new[] { new { id, token = receipt } } });
        var item = Assert.Single(history.GetProperty("items").EnumerateArray());
        var reply = Assert.Single(item.GetProperty("replies").EnumerateArray());
        Assert.Equal("Fixed in 2.1, thanks!", reply.GetProperty("message").GetString());
        Assert.DoesNotContain("internal note", item.GetRawText());
        Assert.DoesNotContain("u@example.com", item.GetRawText());
    }
}
