using System.Threading.Tasks;

namespace Tenant.Api.Services
{
    public interface IReceiptService
    {
        Task<(byte[] PdfBytes, string FileName)> GenerateReceiptAsync(Guid recordId, string? publicId = null);
    }
}
