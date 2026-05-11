namespace AMiracle.Echo.Analysis.OpenAI;

public sealed class OpenAIAnalysisOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string TranscriptionModel { get; set; } = "whisper-1";
    public string ChatModel { get; set; } = "gpt-4o-mini";
    /// <summary>Bumped when prompts/models change. Old feedbacks below this version get re-analyzed.</summary>
    public int Version { get; set; } = 1;
}
