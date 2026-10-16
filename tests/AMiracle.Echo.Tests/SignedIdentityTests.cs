using System.Net;
using System.Text.Json;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Server.Services;

namespace AMiracle.Echo.Tests;

public class SignedIdentityTests
{
    private const string MasterKey = "test-master-key-0123456789";

    private static async Task<(EchoTestHost host, Project project, string secret)> SetupAsync()
    {
        var host = await EchoTestHost.StartAsync(identitySigningKey: MasterKey);
        var project = await host.CreateProjectAsync();
        var reveal = await host.AdminAsync<JsonElement>(HttpMethod.Get, $"/projects/{project.Id}/identity-secret");
        Assert.True(reveal.GetProperty("enabled").GetBoolean());
        return (host, project, reveal.GetProperty("secret").GetString()!);
    }

    [Fact]
    public async Task A_correct_signature_marks_the_submitter_verified_and_is_not_stored()
    {
        var (host, project, secret) = await SetupAsync();
        await using var _ = host;

        var id = await host.SubmitOkAsync(project, new
        {
            type = "text", text = "hello",
            submitter = new { id = "user-42", email = "u@example.com", hash = IdentityVerifier.Sign(secret, "user-42") },
        });

        var fb = await host.GetFeedbackAsync(id);
        Assert.True(fb.Submitter!.Verified);
        Assert.Null(fb.Submitter.Hash);
    }

    [Fact]
    public async Task Upper_case_hex_is_accepted()
    {
        var (host, project, secret) = await SetupAsync();
        await using var _ = host;
        var id = await host.SubmitOkAsync(project, new
        {
            type = "text", text = "hi",
            submitter = new { id = "user-42", hash = IdentityVerifier.Sign(secret, "user-42").ToUpperInvariant() },
        });
        Assert.True((await host.GetFeedbackAsync(id)).Submitter!.Verified);
    }

    [Fact]
    public async Task A_signature_for_another_user_is_flagged_as_a_mismatch()
    {
        var (host, project, secret) = await SetupAsync();
        await using var _ = host;
        var id = await host.SubmitOkAsync(project, new
        {
            type = "text", text = "spoof",
            submitter = new { id = "victim", hash = IdentityVerifier.Sign(secret, "attacker") },
        });
        Assert.False((await host.GetFeedbackAsync(id)).Submitter!.Verified);
    }

    [Fact]
    public async Task A_client_cannot_claim_verified_itself()
    {
        var (host, project, _) = await SetupAsync();
        await using var h = host;
        var id = await host.SubmitOkAsync(project, new
        {
            type = "text", text = "trust me",
            submitter = new { id = "user-42", verified = true },
        });
        Assert.Null((await host.GetFeedbackAsync(id)).Submitter!.Verified);
    }

    [Fact]
    public async Task Another_projects_secret_does_not_verify()
    {
        var (host, project, _) = await SetupAsync();
        await using var h = host;
        var other = await host.CreateProjectAsync("Other");
        var otherSecret = host.Service<IdentityVerifier>().ProjectSecret(other.Id)!;

        var id = await host.SubmitOkAsync(project, new
        {
            type = "text", text = "cross-project",
            submitter = new { id = "user-42", hash = IdentityVerifier.Sign(otherSecret, "user-42") },
        });
        Assert.False((await host.GetFeedbackAsync(id)).Submitter!.Verified);
    }

    [Fact]
    public async Task Without_a_master_key_signing_is_off_and_hashes_are_ignored()
    {
        await using var host = await EchoTestHost.StartAsync();
        var project = await host.CreateProjectAsync();
        var reveal = await host.AdminAsync<JsonElement>(HttpMethod.Get, $"/projects/{project.Id}/identity-secret");
        Assert.False(reveal.GetProperty("enabled").GetBoolean());

        var id = await host.SubmitOkAsync(project, new
        {
            type = "text", text = "x",
            submitter = new { id = "user-42", hash = "deadbeef" },
        });
        var fb = await host.GetFeedbackAsync(id);
        Assert.Null(fb.Submitter!.Verified);
        Assert.Null(fb.Submitter.Hash);
    }

    [Fact]
    public async Task The_identity_secret_is_admin_only()
    {
        var (host, project, _) = await SetupAsync();
        await using var h = host;
        using var res = await host.Client.GetAsync($"/api/v1/admin/projects/{project.Id}/identity-secret");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
