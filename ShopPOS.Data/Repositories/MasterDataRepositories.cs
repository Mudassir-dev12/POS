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
        return await Context.Products.AnyAsync(p => p.BrandId == brandId && !p.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(string? searchTerm, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = Context.Brands.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(term));
        }
        return await query.Select(b => b.Id).ToListAsync(cancellationToken);
    }

    public async Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var cleanName = name.Trim().ToLower();
        var query = Context.Brands.Where(b => b.Name.ToLower() == cleanName);
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
        return await Context.Products.AnyAsync(p => p.UnitId == unitId && !p.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(string? searchTerm, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = Context.Units.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(u => u.Name.ToLower().Contains(term) || u.ShortName.ToLower().Contains(term));
        }
        return await query.Select(u => u.Id).ToListAsync(cancellationToken);
    }

    public async Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var cleanName = name.Trim().ToLower();
        var query = Context.Units.Where(u => u.Name.ToLower() == cleanName);
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
                CurrentBalance = s.OpeningBalance,
                IsActive = s.IsActive,
                HasTransactions = false
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

    public async Task<bool> HasTransactionsAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        return await Context.Purchases.AnyAsync(p => p.SupplierId == supplierId && !p.IsDeleted, cancellationToken) ||
               await Context.SupplierPayments.AnyAsync(sp => sp.SupplierId == supplierId && !sp.IsDeleted, cancellationToken) ||
               await Context.PurchaseReturns.AnyAsync(pr => pr.SupplierId == supplierId && !pr.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(string? searchTerm, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = Context.Suppliers.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term) || (s.Phone != null && s.Phone.ToLower().Contains(term)) || (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(term)));
        }
        return await query.Select(s => s.Id).ToListAsync(cancellationToken);
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
