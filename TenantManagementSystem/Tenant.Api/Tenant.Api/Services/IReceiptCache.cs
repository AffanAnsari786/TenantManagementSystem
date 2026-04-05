namespace Tenant.Api.Services;

/// <summary>
/// Content-addressed cache for generated receipt PDFs. Keys are derived from
/// a hash of the record's mutable fields so that any update automatically
/// produces a new key and invalidates previous entries (which are then
/// reaped by a periodic cleanup job — out of scope for this tier).
///
/// The interface is blob-storage-agnostic — the concrete implementation can
/// be swapped for S3 / Azure Blob without touching callers.
/// </summary>
public interface IReceiptCache
{
    Task<byte[]?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default);

    Task SetAsync(string cacheKey, byte[] pdfBytes, CancellationToken cancellationToken = default);
}
