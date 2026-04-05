namespace Tenant.Api.Contracts;

public class EntryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Address { get; set; }
    public string? AadhaarNumber { get; set; }
    public string? PropertyName { get; set; }
    public IEnumerable<RecordDto> Records { get; set; } = Array.Empty<RecordDto>();
}

public class RecordDto
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public DateTime RentPeriod { get; set; }
    public decimal Amount { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? TenantSign { get; set; }
}
