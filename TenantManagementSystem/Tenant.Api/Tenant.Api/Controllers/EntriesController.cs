using Microsoft.AspNetCore.Mvc;
using Tenant.Api.Models;

namespace Tenant.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EntriesController : ControllerBase
    {
        // Static in-memory data
        private static List<Entry> Entries = new List<Entry>();
        private static int entryIdCounter = 1;
        private static int recordIdCounter = 1;

        // Static constructor to initialize empty data
        static EntriesController()
        {
            // Initialize with empty data - entries will be created by the dashboard
        }

        // GET: api/entries
        [HttpGet]
        public ActionResult<IEnumerable<Entry>> GetEntries()
        {
            return Ok(Entries);
        }

        // GET: api/entries/{id}
        [HttpGet("{id}")]
        public ActionResult<Entry> GetEntry(int id)
        {
            var entry = Entries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return NotFound();
            return Ok(entry);
        }

        // Static method for internal use by ShareController
        public static Entry? GetEntryById(int id)
        {
            return Entries.FirstOrDefault(e => e.Id == id);
        }

        // POST: api/entries (admin only)
        [HttpPost]
        public ActionResult<Entry> CreateEntry([FromBody] Entry entry)
        {
            entry.Id = entryIdCounter++;
            entry.Records = new List<Record>();
            Entries.Add(entry);
            return Ok(entry);
        }

        // POST: api/entries/{entryId}/records (admin only)
        [HttpPost("{entryId}/records")]
        public ActionResult<Record> AddRecord(int entryId, [FromBody] Record record)
        {
            var entry = Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) return NotFound();
            record.Id = recordIdCounter++;
            entry.Records.Add(record);
            return Ok(record);
        }

        // PUT: api/entries/{entryId}/records/{recordId}
        [HttpPut("{entryId}/records/{recordId}")]
        public ActionResult<Record> UpdateRecord(int entryId, int recordId, [FromBody] Record updatedRecord)
        {
            var entry = Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) return NotFound();
            var record = entry.Records.FirstOrDefault(r => r.Id == recordId);
            if (record == null) return NotFound();

            // Update record properties
            record.RentPeriod = updatedRecord.RentPeriod;
            record.Amount = updatedRecord.Amount;
            record.ReceivedDate = updatedRecord.ReceivedDate;
            // Keep original ID and CreatedDate

            return Ok(record);
        }

        // DELETE: api/entries/{entryId}/records/{recordId}
        [HttpDelete("{entryId}/records/{recordId}")]
        public ActionResult DeleteRecord(int entryId, int recordId)
        {
            var entry = Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) return NotFound();
            var record = entry.Records.FirstOrDefault(r => r.Id == recordId);
            if (record == null) return NotFound();

            entry.Records.Remove(record);
            return Ok(new { message = "Record deleted successfully" });
        }

        // PUT: api/entries/{entryId}/records/{recordId}/tenant-sign (tenant only)
        [HttpPut("{entryId}/records/{recordId}/tenant-sign")]
        public ActionResult<Record> UpdateTenantSign(int entryId, int recordId, [FromBody] string tenantSign)
        {
            var entry = Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) return NotFound();
            var record = entry.Records.FirstOrDefault(r => r.Id == recordId);
            if (record == null) return NotFound();
            record.TenantSign = tenantSign;
            return Ok(record);
        }
    }
}
