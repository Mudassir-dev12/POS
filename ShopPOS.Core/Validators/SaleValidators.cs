using FluentValidation;
using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Validators;

public class SaveSaleValidator : AbstractValidator<SaveSaleDto>
{
    public SaveSaleValidator()
    {
        RuleFor(s => s.Items)
            .NotEmpty().WithMessage("Held bill must contain at least one item.");

        RuleForEach(s => s.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("ProductId is required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("UnitPrice cannot be negative.");
            item.RuleFor(i => i.DiscountPercent).InclusiveBetween(0, 100).WithMessage("Discount percent must be between 0 and 100.");
            item.RuleFor(i => i.DiscountAmount).GreaterThanOrEqualTo(0).WithMessage("Discount amount cannot be negative.");
        });

        RuleFor(s => s.BillDiscountPercent).InclusiveBetween(0, 100).WithMessage("Bill discount percent must be between 0 and 100.");
        RuleFor(s => s.BillDiscountAmount).GreaterThanOrEqualTo(0).WithMessage("Bill discount amount cannot be negative.");
    }
}

public class CompleteSaleValidator : AbstractValidator<CompleteSaleDto>
{
    public CompleteSaleValidator()
    {
        RuleFor(s => s.Items)
            .NotEmpty().WithMessage("Sale must contain at least one item.");

        RuleForEach(s => s.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("ProductId is required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("UnitPrice cannot be negative.");
            item.RuleFor(i => i.DiscountPercent).InclusiveBetween(0, 100).WithMessage("Discount percent must be between 0 and 100.");
            item.RuleFor(i => i.DiscountAmount).GreaterThanOrEqualTo(0).WithMessage("Discount amount cannot be negative.");
        });

        RuleForEach(s => s.Payments).ChildRules(payment =>
        {
            payment.RuleFor(p => p.Amount).GreaterThanOrEqualTo(0).WithMessage("Payment amount cannot be negative.");
            payment.RuleFor(p => p.TenderedAmount).GreaterThanOrEqualTo(0)
                .When(p => p.TenderedAmount.HasValue)
                .WithMessage("Tendered amount cannot be negative.");
        });

        RuleFor(s => s.BillDiscountPercent).InclusiveBetween(0, 100).WithMessage("Bill discount percent must be between 0 and 100.");
        RuleFor(s => s.BillDiscountAmount).GreaterThanOrEqualTo(0).WithMessage("Bill discount amount cannot be negative.");
    }
}

public class CreateSaleReturnValidator : AbstractValidator<CreateSaleReturnDto>
{
    public CreateSaleReturnValidator()
    {
        RuleFor(r => r.Items)
            .NotEmpty().WithMessage("Return must contain at least one item.");

        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("ProductId is required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Return quantity must be greater than zero.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0)
                .When(i => i.UnitPrice.HasValue)
                .WithMessage("Unit price cannot be negative.");
        });
    }
}

public class OpenShiftValidator : AbstractValidator<OpenShiftDto>
{
    public OpenShiftValidator()
    {
        RuleFor(s => s.OpeningCash)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Opening cash balance cannot be negative.");
    }
}

public class CloseShiftValidator : AbstractValidator<CloseShiftDto>
{
    public CloseShiftValidator()
    {
        RuleFor(s => s.ShiftId)
            .GreaterThan(0)
            .WithMessage("ShiftId is required.");

        RuleFor(s => s.CountedCash)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Counted cash cannot be negative.");
    }
}

public class CashMovementValidator : AbstractValidator<CashMovementDto>
{
    public CashMovementValidator()
    {
        RuleFor(m => m.ShiftId)
            .GreaterThan(0)
            .WithMessage("ShiftId is required.");

        RuleFor(m => m.Amount)
            .GreaterThan(0)
            .WithMessage("Cash movement amount must be greater than zero.");

        RuleFor(m => m.Reason)
            .NotEmpty()
            .WithMessage("A reason is mandatory for cash movements.")
            .MaximumLength(300)
            .WithMessage("Reason cannot exceed 300 characters.");
    }
}

