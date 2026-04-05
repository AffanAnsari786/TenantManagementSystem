using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Tenant.Api.Services;

namespace Tenant.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptsController : ControllerBase
    {
        private readonly IReceiptService _receiptService;
        private readonly ILogger<ReceiptsController> _logger;

        public ReceiptsController(IReceiptService receiptService, ILogger<ReceiptsController> logger)
        {
            _receiptService = receiptService;
            _logger = logger;
        }

        [HttpGet("{recordId}")]
        [Authorize]
        public async Task<IActionResult> DownloadReceipt(Guid recordId)
        {
            try
            {
                var (pdfBytes, fileName) = await _receiptService.GenerateReceiptAsync(recordId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate receipt for record {RecordId}", recordId);
                return Problem(
                    title: "Could not generate receipt.",
                    detail: "The receipt could not be generated. Please try again later.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("shared/{recordId}")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadSharedReceipt(Guid recordId, [FromQuery] string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
            {
                return Problem(
                    title: "Missing public access id.",
                    detail: "Public access ID is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var (pdfBytes, fileName) = await _receiptService.GenerateReceiptAsync(recordId, publicId);
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate shared receipt for record {RecordId}", recordId);
                return Problem(
                    title: "Could not generate receipt.",
                    detail: "The receipt could not be generated. Please try again later.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
