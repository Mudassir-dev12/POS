using FluentValidation;
using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");

        RuleFor(p => p.Sku)
            .MaximumLength(60).WithMessage("SKU cannot exceed 60 characters.")
            .When(p => !string.IsNullOrWhiteSpace(p.Sku));

        RuleFor(p => p.CategoryId)
            .GreaterThan(0).WithMessage("Please select a valid category.");

        RuleFor(p => p.UnitId)
            .GreaterThan(0).WithMessage("Please select a valid unit of measure.");

        RuleFor(p => p.CostPrice)
            .GreaterThanOrEqualTo(0m).WithMessage("Cost price must be greater than or equal to 0.");

        RuleFor(p => p.SalePrice)
            .GreaterThanOrEqualTo(0m).WithMessage("Sale price must be greater than or equal to 0.");

        RuleFor(p => p.MinSalePrice)
            .GreaterThanOrEqualTo(0m).WithMessage("Minimum sale price must be greater than or equal to 0.")
            .Must((dto, min) => !min.HasValue || min.Value <= dto.SalePrice)
            .WithMessage("Minimum sale price cannot be greater than standard sale price.")
            .When(p => p.MinSalePrice.HasValue);

        RuleFor(p => p.TaxPercent)
            .InclusiveBetween(0m, 100m).WithMessage("Tax percent must be between 0 and 100.");

        RuleFor(p => p.ReorderLevel)
            .GreaterThanOrEqualTo(0m).WithMessage("Reorder level must be 0 or greater.");

        RuleFor(p => p.OpeningQuantity)
            .GreaterThanOrEqualTo(0m).WithMessage("Opening quantity must be 0 or greater.");
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductValidator()
    {
        RuleFor(p => p.Id)
            .GreaterThan(0).WithMessage("Invalid product ID.");

        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");

        RuleFor(p => p.Sku)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(60).WithMessage("SKU cannot exceed 60 characters.");

        RuleFor(p => p.CategoryId)
            .GreaterThan(0).WithMessage("Please select a valid category.");

        RuleFor(p => p.UnitId)
            .GreaterThan(0).WithMessage("Please select a valid unit of measure.");

        RuleFor(p => p.CostPrice)
            .GreaterThanOrEqualTo(0m).WithMessage("Cost price must be greater than or equal to 0.");

        RuleFor(p => p.SalePrice)
            .GreaterThanOrEqualTo(0m).WithMessage("Sale price must be greater than or equal to 0.");

        RuleFor(p => p.MinSalePrice)
            .GreaterThanOrEqualTo(0m).WithMessage("Minimum sale price must be greater than or equal to 0.")
            .Must((dto, min) => !min.HasValue || min.Value <= dto.SalePrice)
            .WithMessage("Minimum sale price cannot be greater than standard sale price.")
            .When(p => p.MinSalePrice.HasValue);

        RuleFor(p => p.TaxPercent)
            .InclusiveBetween(0m, 100m).WithMessage("Tax percent must be between 0 and 100.");

        RuleFor(p => p.ReorderLevel)
            .GreaterThanOrEqualTo(0m).WithMessage("Reorder level must be 0 or greater.");
    }
}

public class CreateCategoryValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(150).WithMessage("Category name cannot exceed 150 characters.");
    }
}

public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryDto>
{
    public UpdateCategoryValidator()
    {
        RuleFor(c => c.Id).GreaterThan(0).WithMessage("Invalid category ID.");
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(150).WithMessage("Category name cannot exceed 150 characters.");
    }
}

public class CreateBrandValidator : AbstractValidator<CreateBrandDto>
{
    public CreateBrandValidator()
    {
        RuleFor(b => b.Name)
            .NotEmpty().WithMessage("Brand name is required.")
            .MaximumLength(150).WithMessage("Brand name cannot exceed 150 characters.");
    }
}

public class UpdateBrandValidator : AbstractValidator<UpdateBrandDto>
{
    public UpdateBrandValidator()
    {
        RuleFor(b => b.Id).GreaterThan(0).WithMessage("Invalid brand ID.");
        RuleFor(b => b.Name)
            .NotEmpty().WithMessage("Brand name is required.")
            .MaximumLength(150).WithMessage("Brand name cannot exceed 150 characters.");
    }
}

public class CreateUnitValidator : AbstractValidator<CreateUnitDto>
{
    public CreateUnitValidator()
    {
        RuleFor(u => u.Name)
            .NotEmpty().WithMessage("Unit name is required.")
            .MaximumLength(100).WithMessage("Unit name cannot exceed 100 characters.");

        RuleFor(u => u.ShortName)
            .NotEmpty().WithMessage("Short abbreviation is required (e.g. kg, pcs).")
            .MaximumLength(50).WithMessage("Short name cannot exceed 50 characters.");
    }
}

public class UpdateUnitValidator : AbstractValidator<UpdateUnitDto>
{
    public UpdateUnitValidator()
    {
        RuleFor(u => u.Id).GreaterThan(0).WithMessage("Invalid unit ID.");
        RuleFor(u => u.Name)
            .NotEmpty().WithMessage("Unit name is required.")
            .MaximumLength(100).WithMessage("Unit name cannot exceed 100 characters.");

        RuleFor(u => u.ShortName)
            .NotEmpty().WithMessage("Short abbreviation is required.")
            .MaximumLength(50).WithMessage("Short name cannot exceed 50 characters.");
    }
}

public class CreateSupplierValidator : AbstractValidator<CreateSupplierDto>
{
    public CreateSupplierValidator()
    {
        RuleFor(s => s.Name)
            .NotEmpty().WithMessage("Supplier name is required.")
            .MaximumLength(150).WithMessage("Supplier name cannot exceed 150 characters.");

        RuleFor(s => s.Phone)
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters.");

        RuleFor(s => s.Email)
            .EmailAddress().WithMessage("Please enter a valid email address.")
            .When(s => !string.IsNullOrWhiteSpace(s.Email));
    }
}

public class UpdateSupplierValidator : AbstractValidator<UpdateSupplierDto>
{
    public UpdateSupplierValidator()
    {
        RuleFor(s => s.Id).GreaterThan(0).WithMessage("Invalid supplier ID.");
        RuleFor(s => s.Name)
            .NotEmpty().WithMessage("Supplier name is required.")
            .MaximumLength(150).WithMessage("Supplier name cannot exceed 150 characters.");

        RuleFor(s => s.Phone)
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters.");

        RuleFor(s => s.Email)
            .EmailAddress().WithMessage("Please enter a valid email address.")
            .When(s => !string.IsNullOrWhiteSpace(s.Email));
    }
}

public class CreateCustomerValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(150).WithMessage("Customer name cannot exceed 150 characters.");

        RuleFor(c => c.Phone)
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters.");

        RuleFor(c => c.Email)
            .EmailAddress().WithMessage("Please enter a valid email address.")
            .When(c => !string.IsNullOrWhiteSpace(c.Email));

        RuleFor(c => c.CreditLimit)
            .GreaterThanOrEqualTo(0m).WithMessage("Credit limit cannot be negative.");
    }
}

public class UpdateCustomerValidator : AbstractValidator<UpdateCustomerDto>
{
    public UpdateCustomerValidator()
    {
        RuleFor(c => c.Id).GreaterThan(0).WithMessage("Invalid customer ID.");
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(150).WithMessage("Customer name cannot exceed 150 characters.");

        RuleFor(c => c.Phone)
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters.");

        RuleFor(c => c.Email)
            .EmailAddress().WithMessage("Please enter a valid email address.")
            .When(c => !string.IsNullOrWhiteSpace(c.Email));

        RuleFor(c => c.CreditLimit)
            .GreaterThanOrEqualTo(0m).WithMessage("Credit limit cannot be negative.");
    }
}
