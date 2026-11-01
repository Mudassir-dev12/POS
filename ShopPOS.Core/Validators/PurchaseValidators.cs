using FluentValidation;
using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Validators;

public class CreatePurchaseItemValidator : AbstractValidator<CreatePurchaseItemDto>
{
    public CreatePurchaseItemValidator()
    {
        RuleFor(i => i.ProductId)
            .GreaterThan(0).WithMessage("Please select a valid product.");

        RuleFor(i => i.Quantity)
            .GreaterThan(0m).WithMessage("Quantity must be greater than 0.");

        RuleFor(i => i.UnitCost)
            .GreaterThanOrEqualTo(0m).WithMessage("Unit cost cannot be negative.");

        RuleFor(i => i.DiscountPercent)
            .InclusiveBetween(0m, 100m).WithMessage("Line discount percent must be between 0 and 100.");

        RuleFor(i => i.TaxPercent)
            .InclusiveBetween(0m, 100m).WithMessage("Tax percent must be between 0 and 100.");
    }
}

public class CreatePurchaseValidator : AbstractValidator<CreatePurchaseDto>
{
    public CreatePurchaseValidator()
    {
        RuleFor(p => p.SupplierId)
            .GreaterThan(0).WithMessage("Please select a supplier.");

        RuleFor(p => p.PurchaseDate)
            .Must(d => d.Date <= DateTime.Today.AddDays(1))
            .WithMessage("Purchase date cannot be in the future by more than 1 day.");

        RuleFor(p => p.DiscountAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Invoice discount amount cannot be negative.");

        RuleFor(p => p.FreightAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Freight amount cannot be negative.");

        RuleFor(p => p.Items)
            .NotEmpty().WithMessage("Purchase invoice must contain at least one line item.");

        RuleForEach(p => p.Items)
            .SetValidator(new CreatePurchaseItemValidator());
    }
}

public class UpdatePurchaseValidator : AbstractValidator<UpdatePurchaseDto>
{
    public UpdatePurchaseValidator()
    {
        RuleFor(p => p.Id)
            .GreaterThan(0).WithMessage("Invalid purchase ID.");

        RuleFor(p => p.SupplierId)
            .GreaterThan(0).WithMessage("Please select a supplier.");

        RuleFor(p => p.PurchaseDate)
            .Must(d => d.Date <= DateTime.Today.AddDays(1))
            .WithMessage("Purchase date cannot be in the future by more than 1 day.");

        RuleFor(p => p.DiscountAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Invoice discount amount cannot be negative.");

        RuleFor(p => p.FreightAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Freight amount cannot be negative.");

        RuleFor(p => p.Items)
            .NotEmpty().WithMessage("Purchase invoice must contain at least one line item.");

        RuleForEach(p => p.Items)
            .SetValidator(new CreatePurchaseItemValidator());
    }
}

public class CreateSupplierPaymentValidator : AbstractValidator<CreateSupplierPaymentDto>
{
    public CreateSupplierPaymentValidator()
    {
        RuleFor(p => p.SupplierId)
            .GreaterThan(0).WithMessage("Please select a supplier.");

        RuleFor(p => p.Amount)
            .GreaterThan(0m).WithMessage("Payment amount must be greater than 0.");

        RuleFor(p => p.PaymentDate)
            .Must(d => d.Date <= DateTime.Today.AddDays(1))
            .WithMessage("Payment date cannot be in the future by more than 1 day.");
    }
}

public class CreatePurchaseReturnItemValidator : AbstractValidator<CreatePurchaseReturnItemDto>
{
    public CreatePurchaseReturnItemValidator()
    {
        RuleFor(i => i.ProductId)
            .GreaterThan(0).WithMessage("Please select a valid product.");

        RuleFor(i => i.Quantity)
            .GreaterThan(0m).WithMessage("Return quantity must be greater than 0.");

        RuleFor(i => i.UnitCost)
            .GreaterThanOrEqualTo(0m).WithMessage("Unit cost cannot be negative.");
    }
}

public class CreatePurchaseReturnValidator : AbstractValidator<CreatePurchaseReturnDto>
{
    public CreatePurchaseReturnValidator()
    {
        RuleFor(r => r.SupplierId)
            .GreaterThan(0).WithMessage("Please select a supplier.");

        RuleFor(r => r.ReturnDate)
            .Must(d => d.Date <= DateTime.Today.AddDays(1))
            .WithMessage("Return date cannot be in the future by more than 1 day.");

        RuleFor(r => r.Items)
            .NotEmpty().WithMessage("Purchase return must contain at least one item.");

        RuleForEach(r => r.Items)
            .SetValidator(new CreatePurchaseReturnItemValidator());
    }
}

