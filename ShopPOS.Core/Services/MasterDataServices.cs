using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Validators;

namespace ShopPOS.Core.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;
    private readonly CreateCategoryValidator _createValidator = new();
    private readonly UpdateCategoryValidator _updateValidator = new();

    public CategoryService(
        ICategoryRepository categoryRepository,
        IAuditLogService auditLogService,
        IPermissionService permissionService)
    {
        _categoryRepository = categoryRepository;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetCategoryTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var tree = await _categoryRepository.GetCategoryTreeAsync(includeInactive, cancellationToken);
            return Result.Ok(tree);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<CategoryDto>>($"Error loading category tree: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var flat = await _categoryRepository.GetAllFlatAsync(includeInactive, cancellationToken);
        var dtos = flat.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            ParentCategoryId = c.ParentCategoryId,
            ParentCategoryName = c.ParentCategory?.Name,
            IsActive = c.IsActive
        }).ToList();
        return Result.Ok<IReadOnlyList<CategoryDto>>(dtos);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure<CategoryDto>("Permission denied. Only Admins and Managers can add categories.");
        }

        var val = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<CategoryDto>(val.ToDictionary(), "Validation failed for category.");
        }

        int? parentId = dto.ParentCategoryId > 0 ? dto.ParentCategoryId : null;
        if (await _categoryRepository.IsNameTakenUnderParentAsync(dto.Name, parentId, null, cancellationToken))
        {
            return Result.Failure<CategoryDto>($"A category named '{dto.Name}' already exists under this parent level.");
        }

        var category = new Category
        {
            Name = dto.Name.Trim(),
            ParentCategoryId = parentId,
            IsActive = true,
            CreatedByUserId = userId
        };

        await _categoryRepository.AddAsync(category, cancellationToken);

        await _auditLogService.LogAsync(userId, "CreateCategory", "Category", category.Id, $"Created category '{category.Name}'");

        return Result.Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            ParentCategoryId = category.ParentCategoryId,
            IsActive = category.IsActive
        });
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(UpdateCategoryDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure<CategoryDto>("Permission denied. Only Admins and Managers can edit categories.");
        }

        var val = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<CategoryDto>(val.ToDictionary(), "Validation failed for category.");
        }

        var existing = await _categoryRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (existing == null)
        {
            return Result.Failure<CategoryDto>($"Category with ID {dto.Id} was not found.");
        }

        int? newParentId = dto.ParentCategoryId > 0 ? dto.ParentCategoryId : null;

        // Prevent setting category as its own ancestor (cycle prevention)
        if (await _categoryRepository.WouldCreateCycleAsync(dto.Id, newParentId, cancellationToken))
        {
            return Result.Failure<CategoryDto>("Cannot move category: a category cannot be its own ancestor.");
        }

        // Check name uniqueness under the new parent
        if (await _categoryRepository.IsNameTakenUnderParentAsync(dto.Name, newParentId, dto.Id, cancellationToken))
        {
            return Result.Failure<CategoryDto>($"A category named '{dto.Name}' already exists under this parent.");
        }

        existing.Name = dto.Name.Trim();
        existing.ParentCategoryId = newParentId;
        existing.IsActive = dto.IsActive;

        await _categoryRepository.UpdateAsync(existing, cancellationToken);

        await _auditLogService.LogAsync(userId, "UpdateCategory", "Category", existing.Id, $"Updated category '{existing.Name}'");

        return Result.Ok(new CategoryDto
        {
            Id = existing.Id,
            Name = existing.Name,
            ParentCategoryId = existing.ParentCategoryId,
            IsActive = existing.IsActive
        });
    }

    public async Task<Result> DeactivateCategoryAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure("Permission denied. Only Admins and Managers can deactivate categories.");
        }

        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            return Result.Failure($"Category with ID {id} was not found.");
        }

        category.IsActive = false;
        await _categoryRepository.UpdateAsync(category, cancellationToken);

        await _auditLogService.LogAsync(userId, "DeactivateCategory", "Category", category.Id, $"Deactivated category '{category.Name}'");

        return Result.Ok();
    }

    public async Task<Result> DeleteCategoryAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure("Permission denied. Only Admins and Managers can delete categories.");
        }

        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            return Result.Failure($"Category with ID {id} was not found.");
        }

        if (await _categoryRepository.HasProductsOrChildrenAsync(id, cancellationToken))
        {
            return Result.Failure("REFERENCED_CANNOT_DELETE");
        }

        await _categoryRepository.SoftDeleteAsync(id, cancellationToken);

        await _auditLogService.LogAsync(userId, "DeleteCategory", "Category", category.Id, $"Soft-deleted category '{category.Name}'");

        return Result.Ok();
    }
}

public class BrandService : IBrandService
{
    private readonly IBrandRepository _brandRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;
    private readonly CreateBrandValidator _createValidator = new();
    private readonly UpdateBrandValidator _updateValidator = new();

    public BrandService(
        IBrandRepository brandRepository,
        IAuditLogService auditLogService,
        IPermissionService permissionService)
    {
        _brandRepository = brandRepository;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }

    public async Task<Result<PagedResult<BrandDto>>> GetPagedBrandsAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default)
    {
        var paged = await _brandRepository.GetPagedAsync(query, cancellationToken);
        return Result.Ok(paged);
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _brandRepository.GetAllFlatAsync(includeInactive, cancellationToken);
        var dtos = list.Select(b => new BrandDto
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description,
            IsActive = b.IsActive
        }).ToList();
        return Result.Ok<IReadOnlyList<BrandDto>>(dtos);
    }

    public async Task<Result<BrandDto>> CreateBrandAsync(CreateBrandDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure<BrandDto>("Permission denied. Only Admins and Managers can create brands.");
        }

        var val = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<BrandDto>(val.ToDictionary());
        }

        if (await _brandRepository.IsNameTakenAsync(dto.Name, null, cancellationToken))
        {
            return Result.Failure<BrandDto>($"Brand '{dto.Name}' already exists.");
        }

        var brand = new Brand
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            IsActive = true,
            CreatedByUserId = userId
        };

        await _brandRepository.AddAsync(brand, cancellationToken);
        await _auditLogService.LogAsync(userId, "CreateBrand", "Brand", brand.Id, $"Created brand '{brand.Name}'");

        return Result.Ok(new BrandDto { Id = brand.Id, Name = brand.Name, Description = brand.Description, IsActive = brand.IsActive });
    }

    public async Task<Result<BrandDto>> UpdateBrandAsync(UpdateBrandDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure<BrandDto>("Permission denied. Only Admins and Managers can edit brands.");
        }

        var val = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<BrandDto>(val.ToDictionary());
        }

        var existing = await _brandRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (existing == null)
        {
            return Result.Failure<BrandDto>($"Brand with ID {dto.Id} was not found.");
        }

        if (await _brandRepository.IsNameTakenAsync(dto.Name, dto.Id, cancellationToken))
        {
            return Result.Failure<BrandDto>($"Brand '{dto.Name}' already exists.");
        }

        existing.Name = dto.Name.Trim();
        existing.Description = dto.Description?.Trim();
        existing.IsActive = dto.IsActive;

        await _brandRepository.UpdateAsync(existing, cancellationToken);
        await _auditLogService.LogAsync(userId, "UpdateBrand", "Brand", existing.Id, $"Updated brand '{existing.Name}'");

        return Result.Ok(new BrandDto { Id = existing.Id, Name = existing.Name, Description = existing.Description, IsActive = existing.IsActive });
    }

    public async Task<Result> DeactivateBrandAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure("Permission denied. Only Admins and Managers can deactivate brands.");
        }

        var brand = await _brandRepository.GetByIdAsync(id, cancellationToken);
        if (brand == null)
        {
            return Result.Failure($"Brand with ID {id} was not found.");
        }

        brand.IsActive = false;
        await _brandRepository.UpdateAsync(brand, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeactivateBrand", "Brand", brand.Id, $"Deactivated brand '{brand.Name}'");

        return Result.Ok();
    }

    public async Task<Result> DeleteBrandAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure("Permission denied. Only Admins and Managers can delete brands.");
        }

        var brand = await _brandRepository.GetByIdAsync(id, cancellationToken);
        if (brand == null)
        {
            return Result.Failure($"Brand with ID {id} was not found.");
        }

        if (await _brandRepository.HasProductsAsync(id, cancellationToken))
        {
            return Result.Failure("REFERENCED_CANNOT_DELETE");
        }

        await _brandRepository.SoftDeleteAsync(id, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeleteBrand", "Brand", brand.Id, $"Deleted brand '{brand.Name}'");

        return Result.Ok();
    }
}

public class UnitService : IUnitService
{
    private readonly IUnitRepository _unitRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;
    private readonly CreateUnitValidator _createValidator = new();
    private readonly UpdateUnitValidator _updateValidator = new();

    public UnitService(
        IUnitRepository unitRepository,
        IAuditLogService auditLogService,
        IPermissionService permissionService)
    {
        _unitRepository = unitRepository;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }

    public async Task<Result<PagedResult<UnitDto>>> GetPagedUnitsAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default)
    {
        var paged = await _unitRepository.GetPagedAsync(query, cancellationToken);
        return Result.Ok(paged);
    }

    public async Task<Result<IReadOnlyList<UnitDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _unitRepository.GetAllFlatAsync(includeInactive, cancellationToken);
        var dtos = list.Select(u => new UnitDto
        {
            Id = u.Id,
            Name = u.Name,
            ShortName = u.ShortName,
            AllowDecimal = u.AllowDecimal,
            IsActive = u.IsActive
        }).ToList();
        return Result.Ok<IReadOnlyList<UnitDto>>(dtos);
    }

    public async Task<Result<UnitDto>> CreateUnitAsync(CreateUnitDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure<UnitDto>("Permission denied. Only Admins and Managers can add units.");
        }

        var val = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<UnitDto>(val.ToDictionary());
        }

        if (await _unitRepository.IsNameTakenAsync(dto.Name, null, cancellationToken))
        {
            return Result.Failure<UnitDto>($"Unit '{dto.Name}' already exists.");
        }

        var unit = new Unit
        {
            Name = dto.Name.Trim(),
            ShortName = dto.ShortName.Trim(),
            AllowDecimal = dto.AllowDecimal,
            IsActive = true,
            CreatedByUserId = userId
        };

        await _unitRepository.AddAsync(unit, cancellationToken);
        await _auditLogService.LogAsync(userId, "CreateUnit", "Unit", unit.Id, $"Created unit '{unit.Name}' ({unit.ShortName})");

        return Result.Ok(new UnitDto { Id = unit.Id, Name = unit.Name, ShortName = unit.ShortName, AllowDecimal = unit.AllowDecimal, IsActive = unit.IsActive });
    }

    public async Task<Result<UnitDto>> UpdateUnitAsync(UpdateUnitDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure<UnitDto>("Permission denied. Only Admins and Managers can edit units.");
        }

        var val = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<UnitDto>(val.ToDictionary());
        }

        var existing = await _unitRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (existing == null)
        {
            return Result.Failure<UnitDto>($"Unit with ID {dto.Id} was not found.");
        }

        if (await _unitRepository.IsNameTakenAsync(dto.Name, dto.Id, cancellationToken))
        {
            return Result.Failure<UnitDto>($"Unit '{dto.Name}' already exists.");
        }

        existing.Name = dto.Name.Trim();
        existing.ShortName = dto.ShortName.Trim();
        existing.AllowDecimal = dto.AllowDecimal;
        existing.IsActive = dto.IsActive;

        await _unitRepository.UpdateAsync(existing, cancellationToken);
        await _auditLogService.LogAsync(userId, "UpdateUnit", "Unit", existing.Id, $"Updated unit '{existing.Name}' ({existing.ShortName})");

        return Result.Ok(new UnitDto { Id = existing.Id, Name = existing.Name, ShortName = existing.ShortName, AllowDecimal = existing.AllowDecimal, IsActive = existing.IsActive });
    }

    public async Task<Result> DeactivateUnitAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure("Permission denied. Only Admins and Managers can deactivate units.");
        }

        var unit = await _unitRepository.GetByIdAsync(id, cancellationToken);
        if (unit == null)
        {
            return Result.Failure($"Unit with ID {id} was not found.");
        }

        unit.IsActive = false;
        await _unitRepository.UpdateAsync(unit, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeactivateUnit", "Unit", unit.Id, $"Deactivated unit '{unit.Name}'");

        return Result.Ok();
    }

    public async Task<Result> DeleteUnitAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManageCatalog(userRole))
        {
            return Result.Failure("Permission denied. Only Admins and Managers can delete units.");
        }

        var unit = await _unitRepository.GetByIdAsync(id, cancellationToken);
        if (unit == null)
        {
            return Result.Failure($"Unit with ID {id} was not found.");
        }

        // Block deleting units in use
        if (await _unitRepository.HasProductsAsync(id, cancellationToken))
        {
            return Result.Failure("REFERENCED_CANNOT_DELETE");
        }

        await _unitRepository.SoftDeleteAsync(id, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeleteUnit", "Unit", unit.Id, $"Deleted unit '{unit.Name}'");

        return Result.Ok();
    }
}

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;
    private readonly CreateSupplierValidator _createValidator = new();
    private readonly UpdateSupplierValidator _updateValidator = new();

    public SupplierService(
        ISupplierRepository supplierRepository,
        IAuditLogService auditLogService,
        IPermissionService permissionService)
    {
        _supplierRepository = supplierRepository;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }

    public async Task<Result<PagedResult<SupplierDto>>> GetPagedSuppliersAsync(MasterDataSearchQuery query, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanAccessSuppliers(userRole))
        {
            return Result.Failure<PagedResult<SupplierDto>>("Access denied. Cashiers cannot access suppliers.");
        }

        var paged = await _supplierRepository.GetPagedAsync(query, cancellationToken);
        return Result.Ok(paged);
    }

    public async Task<Result<IReadOnlyList<SupplierDto>>> GetFlatListAsync(bool includeInactive = false, string? userRole = null, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanAccessSuppliers(userRole))
        {
            return Result.Failure<IReadOnlyList<SupplierDto>>("Access denied. Cashiers cannot access suppliers.");
        }

        var list = await _supplierRepository.GetAllFlatAsync(includeInactive, cancellationToken);
        var dtos = list.Select(s => new SupplierDto
        {
            Id = s.Id,
            Name = s.Name,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Email = s.Email,
            TaxNumber = s.TaxNumber,
            Address = s.Address,
            Notes = s.Notes,
            OpeningBalance = s.OpeningBalance,
            IsActive = s.IsActive
        }).ToList();
        return Result.Ok<IReadOnlyList<SupplierDto>>(dtos);
    }

    public async Task<Result<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanAccessSuppliers(userRole))
        {
            return Result.Failure<SupplierDto>("Access denied. Cashiers cannot access suppliers.");
        }

        var val = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<SupplierDto>(val.ToDictionary());
        }

        if (await _supplierRepository.IsNamePhoneTakenAsync(dto.Name, dto.Phone, null, cancellationToken))
        {
            return Result.Failure<SupplierDto>($"A supplier with name '{dto.Name}' and phone '{dto.Phone}' already exists.");
        }

        var supplier = new Supplier
        {
            Name = dto.Name.Trim(),
            ContactPerson = dto.ContactPerson?.Trim(),
            Phone = dto.Phone?.Trim(),
            Email = dto.Email?.Trim(),
            TaxNumber = dto.TaxNumber?.Trim(),
            Address = dto.Address?.Trim(),
            Notes = dto.Notes?.Trim(),
            OpeningBalance = dto.OpeningBalance,
            IsActive = true,
            CreatedByUserId = userId
        };

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        await _auditLogService.LogAsync(userId, "CreateSupplier", "Supplier", supplier.Id, $"Created supplier '{supplier.Name}', Opening balance: {supplier.OpeningBalance}");

        return Result.Ok(new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Email = supplier.Email,
            TaxNumber = supplier.TaxNumber,
            Address = supplier.Address,
            Notes = supplier.Notes,
            OpeningBalance = supplier.OpeningBalance,
            IsActive = supplier.IsActive
        });
    }

    public async Task<Result<SupplierDto>> UpdateSupplierAsync(UpdateSupplierDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanAccessSuppliers(userRole))
        {
            return Result.Failure<SupplierDto>("Access denied. Cashiers cannot access suppliers.");
        }

        var val = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<SupplierDto>(val.ToDictionary());
        }

        var existing = await _supplierRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (existing == null)
        {
            return Result.Failure<SupplierDto>($"Supplier with ID {dto.Id} was not found.");
        }

        if (await _supplierRepository.IsNamePhoneTakenAsync(dto.Name, dto.Phone, dto.Id, cancellationToken))
        {
            return Result.Failure<SupplierDto>($"A supplier with name '{dto.Name}' and phone '{dto.Phone}' already exists.");
        }

        existing.Name = dto.Name.Trim();
        existing.ContactPerson = dto.ContactPerson?.Trim();
        existing.Phone = dto.Phone?.Trim();
        existing.Email = dto.Email?.Trim();
        existing.TaxNumber = dto.TaxNumber?.Trim();
        existing.Address = dto.Address?.Trim();
        existing.Notes = dto.Notes?.Trim();
        existing.IsActive = dto.IsActive;

        // Opening balance is only editable until transactions exist
        bool hasTx = await _supplierRepository.HasTransactionsAsync(existing.Id, cancellationToken);
        if (!hasTx)
        {
            existing.OpeningBalance = dto.OpeningBalance;
        }

        await _supplierRepository.UpdateAsync(existing, cancellationToken);
        await _auditLogService.LogAsync(userId, "UpdateSupplier", "Supplier", existing.Id, $"Updated supplier '{existing.Name}'");

        return Result.Ok(new SupplierDto
        {
            Id = existing.Id,
            Name = existing.Name,
            ContactPerson = existing.ContactPerson,
            Phone = existing.Phone,
            Email = existing.Email,
            TaxNumber = existing.TaxNumber,
            Address = existing.Address,
            Notes = existing.Notes,
            OpeningBalance = existing.OpeningBalance,
            IsActive = existing.IsActive
        });
    }

    public async Task<Result> DeactivateSupplierAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanAccessSuppliers(userRole))
        {
            return Result.Failure("Access denied. Cashiers cannot access suppliers.");
        }

        var supplier = await _supplierRepository.GetByIdAsync(id, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure($"Supplier with ID {id} was not found.");
        }

        supplier.IsActive = false;
        await _supplierRepository.UpdateAsync(supplier, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeactivateSupplier", "Supplier", supplier.Id, $"Deactivated supplier '{supplier.Name}'");

        return Result.Ok();
    }

    public async Task<Result> DeleteSupplierAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanAccessSuppliers(userRole))
        {
            return Result.Failure("Access denied. Cashiers cannot access suppliers.");
        }

        var supplier = await _supplierRepository.GetByIdAsync(id, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure($"Supplier with ID {id} was not found.");
        }

        if (await _supplierRepository.HasTransactionsAsync(id, cancellationToken))
        {
            return Result.Failure("REFERENCED_CANNOT_DELETE");
        }

        await _supplierRepository.SoftDeleteAsync(id, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeleteSupplier", "Supplier", supplier.Id, $"Deleted supplier '{supplier.Name}'");

        return Result.Ok();
    }
}

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;
    private readonly CreateCustomerValidator _createValidator = new();
    private readonly UpdateCustomerValidator _updateValidator = new();

    public CustomerService(
        ICustomerRepository customerRepository,
        IAuditLogService auditLogService,
        IPermissionService permissionService)
    {
        _customerRepository = customerRepository;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }

    public async Task<Result<PagedResult<CustomerDto>>> GetPagedCustomersAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default)
    {
        var paged = await _customerRepository.GetPagedAsync(query, cancellationToken);
        return Result.Ok(paged);
    }

    public async Task<Result<IReadOnlyList<CustomerDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _customerRepository.GetAllFlatAsync(includeInactive, cancellationToken);
        var dtos = list.Select(c => new CustomerDto
        {
            Id = c.Id,
            Name = c.Name,
            Phone = c.Phone,
            Email = c.Email,
            TaxNumber = c.TaxNumber,
            Address = c.Address,
            Notes = c.Notes,
            CreditLimit = c.CreditLimit,
            OpeningBalance = c.OpeningBalance,
            DefaultPriceLevelId = c.DefaultPriceLevelId,
            IsActive = c.IsActive
        }).ToList();
        return Result.Ok<IReadOnlyList<CustomerDto>>(dtos);
    }

    public async Task<Result<CustomerDto>> CreateCustomerAsync(CreateCustomerDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanCreateEditCustomers(userRole))
        {
            return Result.Failure<CustomerDto>("Permission denied. Cannot create customer.");
        }

        var val = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<CustomerDto>(val.ToDictionary());
        }

        if (!string.IsNullOrWhiteSpace(dto.Phone) && await _customerRepository.IsPhoneTakenAsync(dto.Phone, null, cancellationToken))
        {
            return Result.Failure<CustomerDto>($"Customer phone '{dto.Phone}' is already in use by another customer.");
        }

        var customer = new Customer
        {
            Name = dto.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Email = dto.Email?.Trim(),
            TaxNumber = dto.TaxNumber?.Trim(),
            Address = dto.Address?.Trim(),
            Notes = dto.Notes?.Trim(),
            CreditLimit = dto.CreditLimit,
            OpeningBalance = dto.OpeningBalance,
            DefaultPriceLevelId = dto.DefaultPriceLevelId > 0 ? dto.DefaultPriceLevelId : null,
            IsActive = true,
            CreatedByUserId = userId
        };

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _auditLogService.LogAsync(userId, "CreateCustomer", "Customer", customer.Id, $"Created customer '{customer.Name}', Opening balance: {customer.OpeningBalance}");

        return Result.Ok(new CustomerDto
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            TaxNumber = customer.TaxNumber,
            Address = customer.Address,
            Notes = customer.Notes,
            CreditLimit = customer.CreditLimit,
            OpeningBalance = customer.OpeningBalance,
            DefaultPriceLevelId = customer.DefaultPriceLevelId,
            IsActive = customer.IsActive
        });
    }

    public async Task<Result<CustomerDto>> UpdateCustomerAsync(UpdateCustomerDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanCreateEditCustomers(userRole))
        {
            return Result.Failure<CustomerDto>("Permission denied. Cannot edit customer.");
        }

        var val = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<CustomerDto>(val.ToDictionary());
        }

        var existing = await _customerRepository.GetByIdAsync(dto.Id, cancellationToken);
        if (existing == null)
        {
            return Result.Failure<CustomerDto>($"Customer with ID {dto.Id} was not found.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Phone) && await _customerRepository.IsPhoneTakenAsync(dto.Phone, dto.Id, cancellationToken))
        {
            return Result.Failure<CustomerDto>($"Customer phone '{dto.Phone}' is already in use by another customer.");
        }

        existing.Name = dto.Name.Trim();
        existing.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        existing.Email = dto.Email?.Trim();
        existing.TaxNumber = dto.TaxNumber?.Trim();
        existing.Address = dto.Address?.Trim();
        existing.Notes = dto.Notes?.Trim();
        existing.CreditLimit = dto.CreditLimit;
        existing.DefaultPriceLevelId = dto.DefaultPriceLevelId > 0 ? dto.DefaultPriceLevelId : null;

        // If Cashier, cannot change IsActive (deactivate)
        if (_permissionService.CanDeleteCustomers(userRole))
        {
            existing.IsActive = dto.IsActive;
        }

        // Opening balance is editable until transactions exist
        bool hasTx = await _customerRepository.HasTransactionsAsync(existing.Id, cancellationToken);
        if (!hasTx)
        {
            existing.OpeningBalance = dto.OpeningBalance;
        }

        await _customerRepository.UpdateAsync(existing, cancellationToken);
        await _auditLogService.LogAsync(userId, "UpdateCustomer", "Customer", existing.Id, $"Updated customer '{existing.Name}'");

        return Result.Ok(new CustomerDto
        {
            Id = existing.Id,
            Name = existing.Name,
            Phone = existing.Phone,
            Email = existing.Email,
            TaxNumber = existing.TaxNumber,
            Address = existing.Address,
            Notes = existing.Notes,
            CreditLimit = existing.CreditLimit,
            OpeningBalance = existing.OpeningBalance,
            DefaultPriceLevelId = existing.DefaultPriceLevelId,
            IsActive = existing.IsActive
        });
    }

    public async Task<Result> DeactivateCustomerAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanDeleteCustomers(userRole))
        {
            return Result.Failure("Permission denied. Cashiers cannot deactivate customers.");
        }

        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer == null)
        {
            return Result.Failure($"Customer with ID {id} was not found.");
        }

        customer.IsActive = false;
        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeactivateCustomer", "Customer", customer.Id, $"Deactivated customer '{customer.Name}'");

        return Result.Ok();
    }

    public async Task<Result> DeleteCustomerAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanDeleteCustomers(userRole))
        {
            return Result.Failure("Permission denied. Cashiers cannot delete customers.");
        }

        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer == null)
        {
            return Result.Failure($"Customer with ID {id} was not found.");
        }

        if (await _customerRepository.HasTransactionsAsync(id, cancellationToken))
        {
            return Result.Failure("REFERENCED_CANNOT_DELETE");
        }

        await _customerRepository.SoftDeleteAsync(id, cancellationToken);
        await _auditLogService.LogAsync(userId, "DeleteCustomer", "Customer", customer.Id, $"Deleted customer '{customer.Name}'");

        return Result.Ok();
    }
}

public class PriceLevelService : IPriceLevelService
{
    private readonly IPriceLevelRepository _priceLevelRepository;

    public PriceLevelService(IPriceLevelRepository priceLevelRepository)
    {
        _priceLevelRepository = priceLevelRepository;
    }

    public async Task<Result<IReadOnlyList<PriceLevelDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var levels = await _priceLevelRepository.GetAllActiveAsync(cancellationToken);
        var dtos = levels.Select(pl => new PriceLevelDto
        {
            Id = pl.Id,
            Name = pl.Name
        }).ToList();
        return Result.Ok<IReadOnlyList<PriceLevelDto>>(dtos);
    }
}
