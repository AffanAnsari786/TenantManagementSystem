using Microsoft.Extensions.Options;

namespace Tenant.Api.Services;

public sealed class ReceiptCacheOptions
{
    public string Directory { get; set; } = Path.Combine(Path.GetTempPath(), "tms-receipt-cache");
}

/// <summary>
/// Local-filesystem implementation of <see cref="IReceiptCache"/>. Suitable
/// for single-node deployments. For multi-instance deployments, swap for an
/// S3 / Azure Blob implementation — the interface is identical.
/// </summary>
public sealed class LocalFileReceiptCache : IReceiptCache
{
    private readonly string _directory;
    private readonly ILogger<LocalFileReceiptCache> _logger;

    public LocalFileReceiptCache(IOptions<ReceiptCacheOptions> options, ILogger<LocalFileReceiptCache> logger)
    {
        var configured = options.Value.Directory;
        _directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "tms-receipt-cache")
            : configured;
        _logger = logger;
        Directory.CreateDirectory(_directory);
        _logger.LogInformation("Receipt cache directory: {Directory}", _directory);
    }

    public async Task<byte[]?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var path = GetSafePath(cacheKey);
        if (!File.Exists(path)) return null;

        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached receipt {Path}, treating as miss", path);
            return null;
        }
    }

    public async Task SetAsync(string cacheKey, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var path = GetSafePath(cacheKey);
        var tmp = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tmp, pdfBytes, cancellationToken);
            // Atomic rename so readers never see a half-written file.
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write cached receipt {Path}", path);
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); } catch { /* best-effort */ }
            }
        }
    }

    private string GetSafePath(string cacheKey)
    {
        // Keys are produced by ReceiptService as base64url of a SHA-256 hash,
        // so they're already filesystem-safe, but we guard against traversal
        // anyway.
        var safe = cacheKey.Replace('/', '_').Replace('\\', '_');
        return Path.Combine(_directory, safe + ".pdf");
    }
}
