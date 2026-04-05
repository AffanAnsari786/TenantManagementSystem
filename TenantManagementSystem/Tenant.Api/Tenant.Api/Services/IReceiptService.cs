namespace Tenant.Api.Services
{
    public interface IReceiptService
    {
        /// <summary>
        /// Authorised / share-token-scoped receipt fetch. Returns the PDF
        /// bytes + the download filename.
        /// </summary>
        Task<(byte[] PdfBytes, string FileName)> GenerateReceiptAsync(Guid recordId, string? publicId = null);

        /// <summary>
        /// Background pre-generation path: asks the service to materialise
        /// and cache the PDF for a record without running any authorization
        /// checks. Invoked only by <see cref="ReceiptWarmingWorker"/>.
        /// </summary>
        Task WarmReceiptAsync(Guid recordId, CancellationToken cancellationToken);
    }
}
