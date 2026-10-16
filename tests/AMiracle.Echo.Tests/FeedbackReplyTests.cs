using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Server.Services.Email;

namespace AMiracle.Echo.Tests;

public class FeedbackReplyTests
{
    [Fact]
    public void Escapes_html_from_both_the_admin_and_the_submitter()
    {
        var fb = new Feedback
        {
            Text = "<img src=x onerror=alert(1)>",
            Submitter = new Submitter { Name = "<b>Eve</b>" },
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
        var mail = FeedbackReply.Build("e@example.com", "App", fb, "Use <script> carefully & well");
        Assert.DoesNotContain("<img", mail.HtmlBody);
        Assert.DoesNotContain("<script>", mail.HtmlBody);
        Assert.DoesNotContain("<b>Eve", mail.HtmlBody);
        Assert.Contains("&lt;script&gt; carefully &amp; well", mail.HtmlBody);
    }

    [Fact]
    public void Keeps_the_subject_on_one_line()
    {
        var mail = FeedbackReply.Build("e@example.com", "App\r\nBcc: x@example.com", new Feedback(), "hi");
        Assert.DoesNotContain("\n", mail.Subject);
        Assert.DoesNotContain("\r", mail.Subject);
    }

    [Fact]
    public void Quotes_a_long_original_only_in_part()
    {
        var fb = new Feedback { Text = new string('a', 2000) };
        var mail = FeedbackReply.Build("e@example.com", "App", fb, "thanks");
        Assert.True(mail.TextBody.Length < 1000);
        Assert.Contains("…", mail.TextBody);
    }

    [Theory]
    [InlineData("a@example.com", "a@example.com")]
    [InlineData("  a@example.com ", "a@example.com")]
    [InlineData("a@example.com\nBcc: b@example.com", null)]
    [InlineData("\"A\" <a@example.com>", null)]
    [InlineData("a@example.com;b@example.com", null)]
    [InlineData("", null)]
    public void Accepts_only_a_single_plain_address(string input, string? expected)
        => Assert.Equal(expected, FeedbackReply.SafeRecipient(input));
}
