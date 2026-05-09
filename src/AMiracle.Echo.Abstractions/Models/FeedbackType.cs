namespace AMiracle.Echo.Abstractions.Models;

public static class FeedbackType
{
    public const string Text = "text";
    public const string Voice = "voice";

    public static bool IsValid(string? value) => value is Text or Voice;
}

public static class FeedbackStatus
{
    public const string New = "new";
    public const string Triaged = "triaged";
    public const string Resolved = "resolved";

    public static bool IsValid(string? value) => value is New or Triaged or Resolved;
}

public static class FeedbackCategory
{
    public const string Bug = "bug";
    public const string Idea = "idea";
    public const string Praise = "praise";
    public const string Question = "question";

    public static bool IsValid(string? value) =>
        value is null or Bug or Idea or Praise or Question;
}
