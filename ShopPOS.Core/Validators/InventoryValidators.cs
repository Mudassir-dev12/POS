using FluentValidation;
using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Validators;

public class CreateStockAdjustmentItemValidator : AbstractValidator<CreateStockAdjustmentItemDto>
{
    public CreateStockAdjustmentItemValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Product is required.");
    }
}

public class CreateStockAdjustmentValidator : AbstractValidator<CreateStockAdjustmentDto>
{
    public CreateStockAdjustmentValidator()
    {
        RuleFor(x => x.AdjustmentDate)
            .Must(d => d.Date <= DateTime.Today.AddDays(1))
            .WithMessage("Adjustment date cannot be in the future by more than 1 day.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one item is required for a stock adjustment.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateStockAdjustmentItemValidator());
    }
}

public class UpdateStockAdjustmentValidator : AbstractValidator<UpdateStockAdjustmentDto>
{
    public UpdateStockAdjustmentValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Adjustment Id is required.");

        RuleFor(x => x.AdjustmentDate)
            .Must(d => d.Date <= DateTime.Today.AddDays(1))
            .WithMessage("Adjustment date cannot be in the future by more than 1 day.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one item is required for a stock adjustment.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateStockAdjustmentItemValidator());
    }
}

