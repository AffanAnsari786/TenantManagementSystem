using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tenant.Api.Contracts;
using Tenant.Api.Data;
using Tenant.Api.Hubs;
using Tenant.Api.Models;
using Tenant.Api.Services;

namespace Tenant.Api.Controllers
{
    [ApiVersion("1.0")]
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EntriesController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly ICurrentUserService _currentUser;
        private readonly IHubContext<SharedDashboardHub> _hubContext;
        private readonly IReceiptJobQueue _receiptJobQueue;

        public EntriesController(
            ITenantService tenantService,
            ICurrentUserService currentUser,
            IHubContext<SharedDashboardHub> hubContext,
            IReceiptJobQueue receiptJobQueue)
        {
            _tenantService = tenantService;
            _currentUser = currentUser;
            _hubContext = hubContext;
            _receiptJobQueue = receiptJobQueue;
        }

        // GET: api/entries - returns paginated entries belonging to the logged-in user.
        [HttpGet]
        public async Task<ActionResult<PagedResponse<EntryDto>>> GetEntries([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(new { message = "Please log in(Session expired)." });

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var response = await _tenantService.GetEntriesAsync(userId.Value, page, pageSize);
            return Ok(response);
        }

        // GET: api/entries/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<EntryDto>> GetEntry(Guid id)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized(new { message = "Please log in." });

            var entry = await _tenantService.GetEntryAsync(id, userId.Value);
            if (entry == null) return NotFound();
            return Ok(entry);
        }

        // POST: api/entries
        [HttpPost]
        public async Task<ActionResult<EntryDto>> CreateEntry([FromBody] CreateEntryRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized(new { message = "Please log in to create entries." });

            var entry = await _tenantService.CreateEntryAsync(userId.Value, request);
            return Ok(entry);
        }

        // PUT: api/entries/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<EntryDto>> UpdateEntry(Guid id, [FromBody] UpdateEntryRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized(new { message = "Please log in to update entries." });

            var entry = await _tenantService.UpdateEntryAsync(id, userId.Value, request);
            if (entry == null) return NotFound(new { message = "Tenant not found or unauthorized." });

            return Ok(entry);
        }

        // DELETE: api/entries/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteEntry(Guid id)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized(new { message = "Please log in to delete entries." });

            var success = await _tenantService.DeleteEntryAsync(id, userId.Value);
            if (!success) return NotFound(new { message = "Tenant not found or unauthorized." });

            return Ok(new { message = "Tenant deleted successfully" });
        }

        // POST: api/entries/{entryId}/records
        [HttpPost("{entryId}/records")]
        public async Task<ActionResult<RecordDto>> AddRecord(Guid entryId, [FromBody] CreateRecordRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var record = await _tenantService.AddRecordAsync(entryId, userId.Value, request);
            if (record == null) return NotFound();

            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            _receiptJobQueue.Enqueue(record.Id);
            return Ok(record);
        }

        // PUT: api/entries/{entryId}/records/{recordId}
        [HttpPut("{entryId}/records/{recordId}")]
        public async Task<ActionResult<RecordDto>> UpdateRecord(Guid entryId, Guid recordId, [FromBody] UpdateRecordRequest updatedRecord)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var record = await _tenantService.UpdateRecordAsync(entryId, recordId, userId.Value, updatedRecord);
            if (record == null) return NotFound();

            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            _receiptJobQueue.Enqueue(record.Id);
            return Ok(record);
        }

        // DELETE: api/entries/{entryId}/records/{recordId}
        [HttpDelete("{entryId}/records/{recordId}")]
        public async Task<ActionResult> DeleteRecord(Guid entryId, Guid recordId)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var success = await _tenantService.DeleteRecordAsync(entryId, recordId, userId.Value);
            if (!success) return NotFound();

            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            return Ok(new { message = "Record deleted successfully" });
        }

        // PUT: api/entries/{entryId}/records/{recordId}/tenant-sign
        [HttpPut("{entryId}/records/{recordId}/tenant-sign")]
        public async Task<ActionResult<RecordDto>> UpdateTenantSign(Guid entryId, Guid recordId, [FromBody] TenantSignRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var record = await _tenantService.UpdateTenantSignAsync(entryId, recordId, userId.Value, request);
            if (record == null) return NotFound();

            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            _receiptJobQueue.Enqueue(record.Id);
            return Ok(record);
        }
    }
}
