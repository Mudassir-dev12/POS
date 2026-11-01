using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class BrandRepository : Repository<Brand>, IBrandRepository
{
    public BrandRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<BrandDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = Context.Brands.AsNoTracking().AsQueryable();

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(b => b.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            baseQuery = baseQuery.Where(b => b.Name.ToLower().Contains(term) || (b.Description != null && b.Description.ToLower().Contains(term)));
        }

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        baseQuery = query.SortDescending ? baseQuery.OrderByDescending(b => b.Name) : baseQuery.OrderBy(b => b.Name);

        int pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
        int pageSize = query.PageSize > 0 ? query.PageSize : 50;

        var items = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BrandDto
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                IsActive = b.IsActive,
                ProductCount = b.Products.Count(p => !p.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<BrandDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<Brand>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Context.Brands.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }
        return await query.OrderBy(b => b.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> HasProductsAsync(int brandId, CancellationToken cancellationToken = default)
    {
        return await Context.Products.AnyAsync(p => p.BrandId == brandId, cancellationToken);
    }

    public async Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var clean = name.Trim().ToLower();
        var query = Context.Brands.Where(b => b.Name.ToLower() == clean);
        if (excludeId.HasValue)
        {
            query = query.Where(b => b.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }
}

public class UnitRepository : Repository<Unit>, IUnitRepository
{
    public UnitRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<UnitDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = Context.Units.AsNoTracking().AsQueryable();

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            baseQuery = baseQuery.Where(u => u.Name.ToLower().Contains(term) || u.ShortName.ToLower().Contains(term));
        }

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        baseQuery = query.SortDescending ? baseQuery.OrderByDescending(u => u.Name) : baseQuery.OrderBy(u => u.Name);

        int pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
        int pageSize = query.PageSize > 0 ? query.PageSize : 50;

        var items = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UnitDto
            {
                Id = u.Id,
                Name = u.Name,
                ShortName = u.ShortName,
                AllowDecimal = u.AllowDecimal,
                IsActive = u.IsActive,
                ProductCount = u.Products.Count(p => !p.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<UnitDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<Unit>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Context.Units.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }
        return await query.OrderBy(u => u.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> HasProductsAsync(int unitId, CancellationToken cancellationToken = default)
    {
        return await Context.Products.AnyAsync(p => p.UnitId == unitId, cancellationToken);
    }

    public async Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var clean = name.Trim().ToLower();
        var query = Context.Units.Where(u => u.Name.ToLower() == clean);
        if (excludeId.HasValue)
        {
            query = query.Where(u => u.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }
}

public class SupplierRepository : Repository<Supplier>, ISupplierRepository
{
    public SupplierRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<SupplierDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = Context.Suppliers.AsNoTracking().AsQueryable();

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            baseQuery = baseQuery.Where(s =>
                s.Name.ToLower().Contains(term) ||
                (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(term)) ||
                (s.Phone != null && s.Phone.ToLower().Contains(term)) ||
                (s.Email != null && s.Email.ToLower().Contains(term)));
        }

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        baseQuery = query.SortDescending ? baseQuery.OrderByDescending(s => s.Name) : baseQuery.OrderBy(s => s.Name);

        int pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
        int pageSize = query.PageSize > 0 ? query.PageSize : 50;

        var items = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierDto
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
                IsActive = s.IsActive,
                HasTransactions = false // In Phase 2, purchases/receiving don't exist yet
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<Supplier>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Context.Suppliers.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }
        return await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> HasTransactionsAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        // Phase 2: Purchases/transactions will be implemented in future phase
        return Task.FromResult(false);
    }

    public async Task<bool> IsNamePhoneTakenAsync(string name, string? phone, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var cleanName = name.Trim().ToLower();
        var cleanPhone = phone?.Trim().ToLower();

        var query = Context.Suppliers.Where(s => s.Name.ToLower() == cleanName && s.Phone != null && s.Phone.ToLower() == cleanPhone);
        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }
}

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<CustomerDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = Context.Customers
            .AsNoTracking()
            .Include(c => c.DefaultPriceLevel)
            .AsQueryable();

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            baseQuery = baseQuery.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term)));
        }

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        baseQuery = query.SortDescending ? baseQuery.OrderByDescending(c => c.Name) : baseQuery.OrderBy(c => c.Name);

        int pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
        int pageSize = query.PageSize > 0 ? query.PageSize : 50;

        var items = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto
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
                DefaultPriceLevelName = c.DefaultPriceLevel != null ? c.DefaultPriceLevel.Name : null,
                IsActive = c.IsActive,
                HasTransactions = false // Phase 2: Sales don't exist yet
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<Customer>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Context.Customers.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }
        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> HasTransactionsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        // Phase 2: Sales/invoices will be implemented in future phase
        return Task.FromResult(false);
    }

    public async Task<bool> IsPhoneTakenAsync(string phone, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var cleanPhone = phone.Trim().ToLower();

        var query = Context.Customers.Where(c => c.Phone != null && c.Phone.ToLower() == cleanPhone);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }
}

public class PriceLevelRepository : Repository<PriceLevel>, IPriceLevelRepository
{
    public PriceLevelRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<PriceLevel>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await Context.PriceLevels
            .AsNoTracking()
            .OrderBy(pl => pl.Name)
            .ToListAsync(cancellationToken);
    }
}

