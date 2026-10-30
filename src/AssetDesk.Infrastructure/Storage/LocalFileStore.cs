using AssetDesk.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AssetDesk.Infrastructure.Storage;

public class StorageOptions
{
    public const string Section = "Storage";
    /// <summary>"Local" or "S3".</summary>
    public string Provider { get; set; } = "Local";
    public string LocalPath { get; set; } = "uploads";
    public string? Bucket { get; set; }
    public string? Region { get; set; }
    /// <summary>Optional custom endpoint (MinIO / LocalStack).</summary>
    public string? ServiceUrl { get; set; }
}

public class LocalFileStore(IOptions<StorageOptions> options) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalPath);

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> OpenAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (!File.Exists(path)) throw new FileNotFoundException("Stored file not found.", key);
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Resolves a key under the root and rejects path traversal.</summary>
    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}
