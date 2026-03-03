using System.ComponentModel.DataAnnotations;

namespace Tenant.Api.Contracts;

public sealed class CreateEntryRequest : IValidatableObject
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is null || EndDate is null)
        {
            yield break;
        }

        if (StartDate.Value >= EndDate.Value)
        {
            yield return new ValidationResult(
                "StartDate must be earlier than EndDate.",
                new[] { nameof(StartDate), nameof(EndDate) });
            yield break;
        }

        var days = (EndDate.Value - StartDate.Value).TotalDays;
        if (days < 30)
        {
            yield return new ValidationResult(
                "Rent period must be at least 30 days (1 month).",
                new[] { nameof(StartDate), nameof(EndDate) });
        }
    }
}

public sealed class CreateRecordRequest
{
    [Required]
    public DateTime? RentPeriod { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime? ReceivedDate { get; set; }
}

public sealed class UpdateRecordRequest
{
    [Required]
    public DateTime? RentPeriod { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime? ReceivedDate { get; set; }
}

public sealed class TenantSignRequest
{
    [Required]
    [StringLength(5000)]
    public string TenantSign { get; set; } = string.Empty;
}

