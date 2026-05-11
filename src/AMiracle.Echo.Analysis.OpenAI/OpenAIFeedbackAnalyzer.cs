using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Analysis.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Analysis.OpenAI;

public sealed class OpenAIFeedbackAnalyzer : IFeedbackAnalyzer
{
    private readonly HttpClient _http;
    private readonly OpenAIAnalysisOptions _opts;
    private readonly ILogger<OpenAIFeedbackAnalyzer> _log;

    public OpenAIFeedbackAnalyzer(
        HttpClient http,
        IOptions<OpenAIAnalysisOptions> options,
        ILogger<OpenAIFeedbackAnalyzer> log)
    {
        _http = http;
        _opts = options.Value;
        _log = log;
        if (!string.IsNullOrEmpty(_opts.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        _http.BaseAddress = new Uri(_opts.BaseUrl.TrimEnd('/') + "/");
    }

    public int Version => _opts.Version;

    public async Task<AnalysisResult> AnalyzeAsync(Feedback feedback, AnalysisContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_opts.ApiKey))
            return new AnalysisResult { Error = "OpenAI ApiKey not configured." };

        var result = new AnalysisResult();

        // 1. Transcription (voice only, when blob is uploaded and not already transcribed).
        if (feedback.Type == FeedbackType.Voice
            && !string.IsNullOrEmpty(feedback.AudioBlobKey)
            && !feedback.AudioBlobKey.StartsWith("pending:", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(feedback.Text)
            && context.OpenAudioBlob is not null)
        {
            try
            {
                await using var audio = await context.OpenAudioBlob(feedback.AudioBlobKey, ct);
                if (audio is not null)
                {
                    result.Transcript = await TranscribeAsync(audio, ct);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Transcription failed for feedback {Id}", feedback.Id);
                result.Error = "Transcription failed: " + ex.Message;
                return result;
            }
        }

        // 2. Text analysis (summary + category + priority). Always run for non-empty content.
        var textForAnalysis = !string.IsNullOrWhiteSpace(result.Transcript) ? result.Transcript : feedback.Text;
        if (!string.IsNullOrWhiteSpace(textForAnalysis))
        {
            try
            {
                var (summary, category, priority) = await ChatAnalyzeAsync(textForAnalysis, ct);
                result.Summary = summary;
                // Don't overwrite a category the submitter explicitly chose.
                if (string.IsNullOrEmpty(feedback.Category)) result.Category = category;
                // Don't overwrite a manually-set priority either.
                if (feedback.Priority is null) result.Priority = priority;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Chat analysis failed for feedback {Id}", feedback.Id);
                result.Error = "Chat analysis failed: " + ex.Message;
            }
        }

        return result;
    }

    private async Task<string?> TranscribeAsync(Stream audio, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var audioContent = new StreamContent(audio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm"); // OpenAI accepts most container types
        form.Add(audioContent, "file", "audio.webm");
        form.Add(new StringContent(_opts.TranscriptionModel), "model");

        using var resp = await _http.PostAsync("audio/transcriptions", form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Whisper {(int)resp.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private async Task<(string? summary, string? category, short? priority)> ChatAnalyzeAsync(string text, CancellationToken ct)
    {
        const string system = """
            You analyze a single user-submitted product feedback string.
            Respond with strict JSON only, no prose, matching this schema:
            {
              "summary": "one-sentence summary of the user's feedback (max 200 chars, plain text, no emoji)",
              "category": "bug" | "idea" | "praise" | "question" | null,
              "priority": 1 | 2 | 3 | 4 | 5 | null
            }
            Rules:
            - category: pick the single best fit, or null if unclear.
            - priority: 1 = trivial, 3 = normal, 5 = critical (e.g. data loss, crash blocking core flow). Default to 3 unless evidence suggests otherwise. Use null if the input is too short to judge.
            - summary: rewrite in neutral third-person ("User reports..." style allowed, but keep it short).
            - Never include text outside the JSON object.
            """;

        var body = new
        {
            model = _opts.ChatModel,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = text.Length > 4000 ? text[..4000] : text },
            },
            temperature = 0.2,
        };
        using var resp = await _http.PostAsJsonAsync("chat/completions", body, _jsonOpts, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"chat/completions {(int)resp.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        try
        {
            using var inner = JsonDocument.Parse(content);
            string? summary = inner.RootElement.TryGetProperty("summary", out var s) ? s.GetString() : null;
            string? category = inner.RootElement.TryGetProperty("category", out var c) ? c.GetString() : null;
            short? priority = null;
            if (inner.RootElement.TryGetProperty("priority", out var pr) && pr.ValueKind == JsonValueKind.Number)
            {
                var n = pr.GetInt32();
                if (n >= 1 && n <= 5) priority = (short)n;
            }
            if (category is not null && category is not ("bug" or "idea" or "praise" or "question")) category = null;
            return (summary, category, priority);
        }
        catch (JsonException)
        {
            // Model returned something that wasn't strict JSON; degrade gracefully.
            return (null, null, null);
        }
    }
}
