using Microsoft.EntityFrameworkCore;
using Tenant.Api.Common;
using Tenant.Api.Contracts;
using Tenant.Api.Data;
using Tenant.Api.Models;

namespace Tenant.Api.Services;

public class TenantService : ITenantService
{
    private readonly AppDbContext _context;
    private readonly IShareService _shareService;

    public TenantService(AppDbContext context, IShareService shareService)
    {
        _context = context;
        _shareService = shareService;
    }

    private static EntryDto MapToDto(Entry entry)
    {
        return new EntryDto
        {
            Id = entry.PublicId,
            Name = entry.Name,
            StartDate = entry.StartDate,
            EndDate = entry.EndDate,
            Address = entry.Address,
            AadhaarNumber = PiiMasking.MaskAadhaar(entry.AadhaarNumber),
            PropertyName = entry.PropertyName,
            Records = entry.Records?.Select(MapToDto).ToList() ?? new List<RecordDto>()
        };
    }

    private static RecordDto MapToDto(Record record)
    {
        // Notice we rely on eager loading or passing entry via tracking if we needed the Entry's PublicId.
        // Assuming we always load Entry or at least we know the caller knows the Entry PublicId
        return new RecordDto
        {
            Id = record.PublicId,
            EntryId = record.Entry?.PublicId ?? Guid.Empty, // Will map properly if Entry is included
            RentPeriod = record.RentPeriod,
            Amount = record.Amount,
            ReceivedDate = record.ReceivedDate,
            CreatedDate = record.CreatedDate,
            TenantSign = record.TenantSign
        };
    }

    public async Task<PagedResponse<EntryDto>> GetEntriesAsync(int userId, int page, int pageSize)
    {
        var query = _context.Entries
            .Where(e => e.UserId == userId);
            
        var totalRecords = await query.CountAsync();

        var entries = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<EntryDto>
        {
            Data = entries.Select(MapToDto),
            TotalRecords = totalRecords,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<EntryDto?> GetEntryAsync(Guid id, int userId)
    {
        var entry = await _context.Entries
            .Include(e => e.Records)
            .FirstOrDefaultAsync(e => e.PublicId == id && e.UserId == userId);

        return entry == null ? null : MapToDto(entry);
    }

    public async Task<EntryDto> CreateEntryAsync(int userId, CreateEntryRequest request)
    {
        var entry = new Entry
        {
            PublicId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate!.Value,
            Address = request.Address?.Trim(),
            AadhaarNumber = PiiMasking.NormaliseAadhaar(request.AadhaarNumber),
            PropertyName = request.PropertyName?.Trim(),
            UserId = userId
        };

        _context.Entries.Add(entry);
        await _context.SaveChangesAsync();
        return MapToDto(entry);
    }

    public async Task<EntryDto?> UpdateEntryAsync(Guid id, int userId, UpdateEntryRequest request)
    {
        var entry = await _context.Entries
            .Include(e => e.Records)
            .FirstOrDefaultAsync(e => e.PublicId == id && e.UserId == userId);

        if (entry == null) return null;

        entry.Name = request.Name.Trim();
        entry.StartDate = request.StartDate!.Value;
        entry.EndDate = request.EndDate!.Value;
        entry.Address = request.Address?.Trim();
        entry.AadhaarNumber = PiiMasking.NormaliseAadhaar(request.AadhaarNumber);
        entry.PropertyName = request.PropertyName?.Trim();

        await _context.SaveChangesAsync();
        return MapToDto(entry);
    }

    public async Task<bool> DeleteEntryAsync(Guid id, int userId)
    {
        var entry = await _context.Entries
            .FirstOrDefaultAsync(e => e.PublicId == id && e.UserId == userId);

        if (entry == null) return false;

        _context.Entries.Remove(entry);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<RecordDto?> AddRecordAsync(Guid entryId, int userId, CreateRecordRequest request)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.PublicId == entryId && e.UserId == userId);
        if (entry == null) return null;

        var record = new Record
        {
            PublicId = Guid.NewGuid(),
            EntryId = entry.Id,
            Entry = entry,
            RentPeriod = request.RentPeriod!.Value,
            Amount = request.Amount,
            ReceivedDate = request.ReceivedDate!.Value,
            CreatedDate = DateTime.UtcNow
        };

        _context.Records.Add(record);
        await _context.SaveChangesAsync();
        _shareService.InvalidateEntry(entry.PublicId);
        return MapToDto(record);
    }

    public async Task<RecordDto?> UpdateRecordAsync(Guid entryId, Guid recordId, int userId, UpdateRecordRequest request)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.PublicId == entryId && e.UserId == userId);
        if (entry == null) return null;

        var record = await _context.Records
            .Include(r => r.Entry)
            .FirstOrDefaultAsync(r => r.PublicId == recordId && r.EntryId == entry.Id);
        if (record == null) return null;

        record.RentPeriod = request.RentPeriod!.Value;
        record.Amount = request.Amount;
        record.ReceivedDate = request.ReceivedDate!.Value;

        await _context.SaveChangesAsync();
        _shareService.InvalidateEntry(entry.PublicId);
        return MapToDto(record);
    }

    public async Task<bool> DeleteRecordAsync(Guid entryId, Guid recordId, int userId)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.PublicId == entryId && e.UserId == userId);
        if (entry == null) return false;

        var record = await _context.Records.FirstOrDefaultAsync(r => r.PublicId == recordId && r.EntryId == entry.Id);
        if (record == null) return false;

        _context.Records.Remove(record);
        await _context.SaveChangesAsync();
        _shareService.InvalidateEntry(entry.PublicId);
        return true;
    }

    public async Task<RecordDto?> UpdateTenantSignAsync(Guid entryId, Guid recordId, int userId, TenantSignRequest request)
    {
        var entry = await _context.Entries.FirstOrDefaultAsync(e => e.PublicId == entryId && e.UserId == userId);
        if (entry == null) return null;

        var record = await _context.Records
            .Include(r => r.Entry)
            .FirstOrDefaultAsync(r => r.PublicId == recordId && r.EntryId == entry.Id);
        if (record == null) return null;

        record.TenantSign = request.TenantSign;
        await _context.SaveChangesAsync();
        _shareService.InvalidateEntry(entry.PublicId);
        return MapToDto(record);
    }
}
