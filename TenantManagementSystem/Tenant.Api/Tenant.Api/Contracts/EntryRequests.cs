namespace Tenant.Api.Contracts;

// DTOs are plain POCOs — all validation lives in the corresponding
// FluentValidation validators in Tenant.Api.Validators.

public sealed class CreateEntryRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Address { get; set; }
    public string? AadhaarNumber { get; set; }
    public string? PropertyName { get; set; }
}

public sealed class UpdateEntryRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Address { get; set; }
    public string? AadhaarNumber { get; set; }
    public string? PropertyName { get; set; }
}

public sealed class CreateRecordRequest
{
    public DateTime? RentPeriod { get; set; }
    public decimal Amount { get; set; }
    public DateTime? ReceivedDate { get; set; }
}

public sealed class UpdateRecordRequest
{
    public DateTime? RentPeriod { get; set; }
    public decimal Amount { get; set; }
    public DateTime? ReceivedDate { get; set; }
}

public sealed class TenantSignRequest
{
    public string TenantSign { get; set; } = string.Empty;
}
