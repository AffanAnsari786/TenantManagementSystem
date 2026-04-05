using FluentValidation;
using Tenant.Api.Contracts;

namespace Tenant.Api.Validators;

internal abstract class RecordRequestValidatorBase<T> : AbstractValidator<T>
{
    protected RecordRequestValidatorBase(
        Func<T, DateTime?> rentPeriod,
        Func<T, decimal> amount,
        Func<T, DateTime?> receivedDate)
    {
        RuleFor(x => rentPeriod(x))
            .NotNull().WithMessage("RentPeriod is required.");

        RuleFor(x => amount(x))
            .GreaterThan(0m).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(10_000_000m).WithMessage("Amount is unreasonably large.");

        RuleFor(x => receivedDate(x))
            .NotNull().WithMessage("ReceivedDate is required.");
    }
}

internal sealed class CreateRecordRequestValidator : RecordRequestValidatorBase<CreateRecordRequest>
{
    public CreateRecordRequestValidator() : base(x => x.RentPeriod, x => x.Amount, x => x.ReceivedDate) { }
}

internal sealed class UpdateRecordRequestValidator : RecordRequestValidatorBase<UpdateRecordRequest>
{
    public UpdateRecordRequestValidator() : base(x => x.RentPeriod, x => x.Amount, x => x.ReceivedDate) { }
}

internal sealed class TenantSignRequestValidator : AbstractValidator<TenantSignRequest>
{
    public TenantSignRequestValidator()
    {
        RuleFor(x => x.TenantSign)
            .NotEmpty().WithMessage("TenantSign is required.")
            .MaximumLength(5000);
    }
}
