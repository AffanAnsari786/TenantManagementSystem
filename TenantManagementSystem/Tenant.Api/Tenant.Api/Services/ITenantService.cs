using Tenant.Api.Contracts;
using Tenant.Api.Models;

namespace Tenant.Api.Services;

public interface ITenantService
{
    Task<PagedResponse<EntryDto>> GetEntriesAsync(int userId, int page, int pageSize);
    Task<EntryDto?> GetEntryAsync(Guid id, int userId);
    Task<EntryDto> CreateEntryAsync(int userId, CreateEntryRequest request);
    Task<EntryDto?> UpdateEntryAsync(Guid id, int userId, UpdateEntryRequest request);
    Task<bool> DeleteEntryAsync(Guid id, int userId);
    Task<RecordDto?> AddRecordAsync(Guid entryId, int userId, CreateRecordRequest request);
    Task<RecordDto?> UpdateRecordAsync(Guid entryId, Guid recordId, int userId, UpdateRecordRequest request);
    Task<bool> DeleteRecordAsync(Guid entryId, Guid recordId, int userId);
    Task<RecordDto?> UpdateTenantSignAsync(Guid entryId, Guid recordId, int userId, TenantSignRequest request);
}
