namespace AMiracle.Echo.Abstractions.Stores;

public interface IBlobStore
{
    Task<string> WriteAsync(string keyPrefix, string suggestedFileName, Stream content, string contentType, CancellationToken ct = default);
    Task<BlobReadHandle?> ReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task DeleteByPrefixAsync(string keyPrefix, CancellationToken ct = default);
}

public sealed class BlobReadHandle : IAsyncDisposable
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required long Length { get; init; }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
    }
}
