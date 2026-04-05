using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Tenant.Api.Services;

namespace Tenant.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptsController : ControllerBase
    {
        private readonly IReceiptService _receiptService;

        public ReceiptsController(IReceiptService receiptService)
        {
            _receiptService = receiptService;
        }

        [HttpGet("{recordId}")]
        [Authorize]
        public async Task<IActionResult> DownloadReceipt(Guid recordId)
        {
            try
            {
                var (pdfBytes, fileName) = await _receiptService.GenerateReceiptAsync(recordId);
                return File(pdfBytes, "application/pdf", fileName);
                //return Forbid();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("shared/{recordId}")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadSharedReceipt(Guid recordId, [FromQuery] string publicId)
        {
            try
            {
                if (string.IsNullOrEmpty(publicId))
                {
                    return BadRequest(new { Message = "Public access ID is required" });
                }

                var (pdfBytes, fileName) = await _receiptService.GenerateReceiptAsync(recordId, publicId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
