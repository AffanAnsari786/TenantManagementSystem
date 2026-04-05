using FluentValidation;
using Tenant.Api.Contracts;

namespace Tenant.Api.Validators;

/// <summary>
/// Shared rules for Create/Update Entry requests. Keep a single source of
/// truth — the concrete Create/Update validators just inherit this base.
/// </summary>
internal abstract class EntryRequestValidatorBase<T> : AbstractValidator<T>
{
    protected EntryRequestValidatorBase(
        Func<T, string> name,
        Func<T, DateTime?> startDate,
        Func<T, DateTime?> endDate,
        Func<T, string?> address,
        Func<T, string?> aadhaar,
        Func<T, string?> propertyName)
    {
        RuleFor(x => name(x))
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => address(x))
            .MaximumLength(500);

        RuleFor(x => propertyName(x))
            .MaximumLength(200);

        RuleFor(x => aadhaar(x))
            .Must(a => a == null || System.Text.RegularExpressions.Regex.IsMatch(
                a.Replace(" ", string.Empty).Replace("-", string.Empty), "^\\d{12}$"))
            .WithMessage("AadhaarNumber must be exactly 12 digits.");

        RuleFor(x => startDate(x))
            .NotNull().WithMessage("StartDate is required.");

        RuleFor(x => endDate(x))
            .NotNull().WithMessage("EndDate is required.");

        RuleFor(x => x)
            .Must(x =>
            {
                var s = startDate(x);
                var e = endDate(x);
                return s is null || e is null || s.Value < e.Value;
            })
            .WithMessage("StartDate must be earlier than EndDate.")
            .WithName("StartDate");

        RuleFor(x => x)
            .Must(x =>
            {
                var s = startDate(x);
                var e = endDate(x);
                if (s is null || e is null) return true;
                return (e.Value - s.Value).TotalDays >= 30;
            })
            .WithMessage("Rent period must be at least 30 days (1 month).")
            .WithName("StartDate");
    }
}

internal sealed class CreateEntryRequestValidator : EntryRequestValidatorBase<CreateEntryRequest>
{
    public CreateEntryRequestValidator() : base(
        x => x.Name,
        x => x.StartDate,
        x => x.EndDate,
        x => x.Address,
        x => x.AadhaarNumber,
        x => x.PropertyName)
    { }
}

internal sealed class UpdateEntryRequestValidator : EntryRequestValidatorBase<UpdateEntryRequest>
{
    public UpdateEntryRequestValidator() : base(
        x => x.Name,
        x => x.StartDate,
        x => x.EndDate,
        x => x.Address,
        x => x.AadhaarNumber,
        x => x.PropertyName)
    { }
}
