using System.Text.Json;
using AMiracle.Echo.Abstractions.Configuration;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Abstractions.Stores;
using AMiracle.Echo.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Endpoints;

internal static class IngestionEndpoints
{
    public static void MapIngestion(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/feedbacks");

        group.MapPost("/", CreateFeedback);
        group.MapPost("/{id:guid}/audio", UploadAudio);
        group.MapPost("/{id:guid}/screenshot", UploadScreenshot);
    }

    private static async Task<IResult> CreateFeedback(
        HttpContext ctx,
        IFeedbackStore store,
        FeedbackIngestionService ingestion,
        CancellationToken ct)
    {
        var (project, errorResult) = await AuthenticateProjectAsync(ctx, store, ct);
        if (errorResult is not null) return errorResult;

        CreateFeedbackInput? input;
        try
        {
            input = await ctx.Request.ReadFromJsonAsync<CreateFeedbackInput>(ct);
        }
        catch (JsonException ex)
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid-json", "Invalid JSON.", ex.Message);
        }
        if (input is null) return Problem(StatusCodes.Status400BadRequest, "empty-body", "Empty body.", null);

        // Pull pageUrl/userAgent from input but respect DNT.
        if (string.Equals(ctx.Request.Headers["DNT"].ToString(), "1", StringComparison.Ordinal))
        {
            input.PageUrl = null;
            input.UserAgent = null;
        }

        var result = await ingestion.IngestAsync(project!.Id, input, ct);
        if (!result.Success)
            return Problem(StatusCodes.Status400BadRequest, result.ErrorCode!, result.ErrorMessage!, null);

        var fb = result.Feedback!;
        return Results.Created($"/api/v1/feedbacks/{fb.Id}", new
        {
            id = fb.Id,
            uploadAudioUrl = fb.AudioBlobKey is not null ? $"/api/v1/feedbacks/{fb.Id}/audio" : null,
            uploadScreenshotUrl = fb.ScreenshotKey is not null ? $"/api/v1/feedbacks/{fb.Id}/screenshot" : null,
        });
    }

    private static async Task<IResult> UploadAudio(
        Guid id,
        HttpContext ctx,
        IFeedbackStore store,
        IBlobStore blobs,
        IOptions<EchoOptions> options,
        CancellationToken ct)
    {
        return await UploadBlob(id, ctx, store, blobs, options.Value.MaxAudioBytes, isAudio: true, ct);
    }

    private static async Task<IResult> UploadScreenshot(
        Guid id,
        HttpContext ctx,
        IFeedbackStore store,
        IBlobStore blobs,
        IOptions<EchoOptions> options,
        CancellationToken ct)
    {
        return await UploadBlob(id, ctx, store, blobs, options.Value.MaxScreenshotBytes, isAudio: false, ct);
    }

    private static async Task<IResult> UploadBlob(
        Guid id, HttpContext ctx,
        IFeedbackStore store, IBlobStore blobs,
        long maxBytes, bool isAudio, CancellationToken ct)
    {
        var (project, errorResult) = await AuthenticateProjectAsync(ctx, store, ct);
        if (errorResult is not null) return errorResult;

        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb is null || fb.ProjectId != project!.Id)
            return Problem(StatusCodes.Status404NotFound, "not-found", "Feedback not found.", null);

        var keyField = isAudio ? fb.AudioBlobKey : fb.ScreenshotKey;
        if (keyField is null || !keyField.StartsWith("pending:", StringComparison.Ordinal))
            return Problem(StatusCodes.Status409Conflict, "blob-not-expected",
                isAudio ? "This feedback did not declare an audio upload, or it was already uploaded." : "Screenshot already uploaded or not expected.", null);

        if (ctx.Request.ContentLength is { } cl && cl > maxBytes)
            return Problem(StatusCodes.Status413PayloadTooLarge, "too-large", $"Blob exceeds maxBytes={maxBytes}.", null);

        var contentType = ctx.Request.ContentType ?? "application/octet-stream";
        var fileName = isAudio ? "audio" + GuessExt(contentType) : "screenshot" + GuessExt(contentType);
        var keyPrefix = $"{project!.Id}/{fb.Id}";

        // Cap by reading through a length-limited stream.
        var limited = new LengthLimitedStream(ctx.Request.Body, maxBytes);
        string newKey;
        try
        {
            newKey = await blobs.WriteAsync(keyPrefix, fileName, limited, contentType, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("limit"))
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "too-large", "Blob exceeded size limit.", null);
        }

        if (isAudio) fb.AudioBlobKey = newKey; else fb.ScreenshotKey = newKey;
        await store.UpdateFeedbackAsync(fb, ct);
        return Results.NoContent();
    }

    internal static async Task<(Project? project, IResult? error)> AuthenticateProjectAsync(
        HttpContext ctx, IFeedbackStore store, CancellationToken ct)
    {
        var publicKey = ctx.Request.Headers["X-Echo-Project-Key"].ToString();
        if (string.IsNullOrWhiteSpace(publicKey))
            return (null, Problem(StatusCodes.Status401Unauthorized, "missing-key", "X-Echo-Project-Key header required.", null));

        var project = await store.GetProjectByPublicKeyAsync(publicKey, ct);
        if (project is null || project.ArchivedAt is not null)
            return (null, Problem(StatusCodes.Status401Unauthorized, "invalid-key", "Unknown or archived project key.", null));

        var origin = ctx.Request.Headers["Origin"].ToString();
        if (!OriginValidator.IsAllowed(origin, project.AllowedOrigins))
            return (null, Problem(StatusCodes.Status403Forbidden, "origin-not-allowed", $"Origin '{origin}' not in project's allowed origins.", null));

        // CORS reflection for ingestion (only when origin is allowed).
        if (!string.IsNullOrEmpty(origin))
        {
            ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
            ctx.Response.Headers["Vary"] = "Origin";
        }

        return (project, null);
    }

    private static IResult Problem(int status, string code, string title, string? detail)
        => Results.Problem(
            type: $"/errors/{code}",
            title: title,
            detail: detail,
            statusCode: status);

    private static string GuessExt(string contentType) => contentType.ToLowerInvariant() switch
    {
        "audio/webm" or "audio/webm;codecs=opus" => ".webm",
        "audio/ogg" or "audio/ogg;codecs=opus" => ".ogg",
        "audio/mp4" or "audio/m4a" => ".m4a",
        "audio/mpeg" => ".mp3",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => ".bin",
    };
}

internal sealed class LengthLimitedStream : Stream
{
    private readonly Stream _inner;
    private readonly long _max;
    private long _read;

    public LengthLimitedStream(Stream inner, long max) { _inner = inner; _max = max; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = _inner.Read(buffer, offset, count);
        _read += n;
        if (_read > _max) throw new InvalidOperationException("Stream exceeded length limit.");
        return n;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await _inner.ReadAsync(buffer, ct);
        _read += n;
        if (_read > _max) throw new InvalidOperationException("Stream exceeded length limit.");
        return n;
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
