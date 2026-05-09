using AMiracle.Echo.Abstractions.Stores;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Storage.LocalFS;

public sealed class LocalFileBlobStoreOptions
{
    public string RootPath { get; set; } = "./echo-blobs";
}

public sealed class LocalFileBlobStore : IBlobStore
{
    private readonly string _root;

    public LocalFileBlobStore(IOptions<LocalFileBlobStoreOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> WriteAsync(string keyPrefix, string suggestedFileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var safePrefix = SanitizePrefix(keyPrefix);
        var safeName = SanitizeFileName(suggestedFileName);
        var key = $"{safePrefix}/{safeName}";
        var fullDir = Path.Combine(_root, safePrefix.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.Combine(fullDir, safeName);

        if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal))
            throw new InvalidOperationException("Path traversal detected.");

        Directory.CreateDirectory(fullDir);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);
        await File.WriteAllTextAsync(fullPath + ".content-type", contentType, ct);
        return key;
    }

    public Task<BlobReadHandle?> ReadAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (!File.Exists(fullPath)) return Task.FromResult<BlobReadHandle?>(null);

        var contentType = File.Exists(fullPath + ".content-type")
            ? File.ReadAllText(fullPath + ".content-type")
            : "application/octet-stream";
        var fs = File.OpenRead(fullPath);
        return Task.FromResult<BlobReadHandle?>(new BlobReadHandle
        {
            Content = fs,
            ContentType = contentType,
            Length = fs.Length,
        });
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        if (File.Exists(fullPath + ".content-type")) File.Delete(fullPath + ".content-type");
        return Task.CompletedTask;
    }

    public Task DeleteByPrefixAsync(string keyPrefix, CancellationToken ct = default)
    {
        var safePrefix = SanitizePrefix(keyPrefix);
        var dir = Path.Combine(_root, safePrefix.Replace('/', Path.DirectorySeparatorChar));
        var fullDir = Path.GetFullPath(dir);
        if (!fullDir.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal))
            throw new InvalidOperationException("Path traversal detected.");
        if (Directory.Exists(fullDir)) Directory.Delete(fullDir, recursive: true);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var path = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal))
            throw new InvalidOperationException("Path traversal detected.");
        return full;
    }

    private static readonly HashSet<char> _badForFileName =
        new(Path.GetInvalidFileNameChars()
            .Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }));

    private static string SanitizeFileName(string s)
    {
        var safe = new string(s.Select(c => _badForFileName.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "_" : safe;
    }

    private static string SanitizePrefix(string s)
    {
        // Allow forward-slash separators inside the prefix, but sanitize each segment.
        var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("Empty key prefix.", nameof(s));
        return string.Join('/', parts.Select(SanitizeFileName));
    }
}
