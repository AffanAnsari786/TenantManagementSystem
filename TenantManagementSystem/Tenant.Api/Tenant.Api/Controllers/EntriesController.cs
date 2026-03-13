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
    [ApiController]
    [Route("api/[controller]")]
    public class EntriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IHubContext<SharedDashboardHub> _hubContext;

        public EntriesController(AppDbContext context, ICurrentUserService currentUser, IHubContext<SharedDashboardHub> hubContext)
        {
            _context = context;
            _currentUser = currentUser;
            _hubContext = hubContext;
        }

        // GET: api/entries - returns only entries belonging to the logged-in user.
        // Entries with UserId null (e.g. created before user-scoping) are "claimed" by the current user so they show after reload.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Entry>>> GetEntries()
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(new { message = "Please log in to view your entries." });

            var entries = await _context.Entries
                .Include(e => e.Records)
                .Where(e => e.UserId == userId || e.UserId == null)
                .OrderBy(e => e.Id)
                .ToListAsync();

            // Claim unassigned entries so they stay with this user on next load
            var toClaim = entries.Where(e => e.UserId == null).ToList();
            foreach (var entry in toClaim)
            {
                entry.UserId = userId;
            }
            if (toClaim.Count > 0)
                await _context.SaveChangesAsync();

            return Ok(entries);
        }

        // GET: api/entries/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Entry>> GetEntry(int id)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(new { message = "Please log in." });

            var entry = await _context.Entries
                .Include(e => e.Records)
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (entry == null) return NotFound();
            return Ok(entry);
        }

        // POST: api/entries
        [HttpPost]
        public async Task<ActionResult<Entry>> CreateEntry([FromBody] CreateEntryRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(new { message = "Please log in to create entries." });

            var entry = new Entry
            {
                Name = request.Name.Trim(),
                StartDate = request.StartDate!.Value,
                EndDate = request.EndDate!.Value,
                UserId = userId
            };

            _context.Entries.Add(entry);
            await _context.SaveChangesAsync();
            return Ok(entry);
        }

        // POST: api/entries/{entryId}/records
        [HttpPost("{entryId}/records")]
        public async Task<ActionResult<Record>> AddRecord(int entryId, [FromBody] CreateRecordRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var entryExists = await _context.Entries.AnyAsync(e => e.Id == entryId && e.UserId == userId);
            if (!entryExists) return NotFound();

            var record = new Record
            {
                EntryId = entryId,
                RentPeriod = request.RentPeriod!.Value,
                Amount = request.Amount,
                ReceivedDate = request.ReceivedDate!.Value,
                CreatedDate = DateTime.UtcNow
            };

            _context.Records.Add(record);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            return Ok(record);
        }

        // PUT: api/entries/{entryId}/records/{recordId}
        [HttpPut("{entryId}/records/{recordId}")]
        public async Task<ActionResult<Record>> UpdateRecord(int entryId, int recordId, [FromBody] UpdateRecordRequest updatedRecord)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var entry = await _context.Entries.FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId);
            if (entry == null) return NotFound();

            var record = await _context.Records
                .FirstOrDefaultAsync(r => r.Id == recordId && r.EntryId == entryId);
            if (record == null) return NotFound();

            record.RentPeriod = updatedRecord.RentPeriod!.Value;
            record.Amount = updatedRecord.Amount;
            record.ReceivedDate = updatedRecord.ReceivedDate!.Value;

            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            return Ok(record);
        }

        // DELETE: api/entries/{entryId}/records/{recordId}
        [HttpDelete("{entryId}/records/{recordId}")]
        public async Task<ActionResult> DeleteRecord(int entryId, int recordId)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var entry = await _context.Entries.FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId);
            if (entry == null) return NotFound();

            var record = await _context.Records
                .FirstOrDefaultAsync(r => r.Id == recordId && r.EntryId == entryId);
            if (record == null) return NotFound();

            _context.Records.Remove(record);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            return Ok(new { message = "Record deleted successfully" });
        }

        // PUT: api/entries/{entryId}/records/{recordId}/tenant-sign
        [HttpPut("{entryId}/records/{recordId}/tenant-sign")]
        public async Task<ActionResult<Record>> UpdateTenantSign(int entryId, int recordId, [FromBody] TenantSignRequest request)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var entry = await _context.Entries.FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId);
            if (entry == null) return NotFound();

            var record = await _context.Records
                .FirstOrDefaultAsync(r => r.Id == recordId && r.EntryId == entryId);
            if (record == null) return NotFound();

            record.TenantSign = request.TenantSign;
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Entry_{entryId}").SendAsync(SharedDashboardHub.EntryUpdatedMethod, entryId);
            return Ok(record);
        }
    }
}
