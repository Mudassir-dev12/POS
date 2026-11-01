using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Repositories;

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoryTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Context.Categories.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var all = await query.ToListAsync(cancellationToken);

        // Grouped query for product counts
        var productCounts = await Context.Products
            .AsNoTracking()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        var dtoList = all.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            ParentCategoryId = c.ParentCategoryId,
            IsActive = c.IsActive,
            ProductCount = productCounts.TryGetValue(c.Id, out var cnt) ? cnt : 0
        }).ToList();

        var dtoLookup = dtoList.ToDictionary(d => d.Id);

        // Assign ParentCategoryName and link children
        var roots = new List<CategoryDto>();
        foreach (var item in dtoList)
        {
            if (item.ParentCategoryId.HasValue && dtoLookup.TryGetValue(item.ParentCategoryId.Value, out var parent))
            {
                item.ParentCategoryName = parent.Name;
                parent.SubCategories.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        return roots.OrderBy(r => r.Name).ToList();
    }

    public async Task<IReadOnlyList<Category>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Context.Categories.AsNoTracking().Include(c => c.ParentCategory).AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }
        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> HasProductsOrChildrenAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        bool hasProducts = await Context.Products.AnyAsync(p => p.CategoryId == categoryId, cancellationToken);
        if (hasProducts) return true;

        bool hasChildren = await Context.Categories.AnyAsync(c => c.ParentCategoryId == categoryId, cancellationToken);
        return hasChildren;
    }

    public async Task<bool> WouldCreateCycleAsync(int categoryId, int? newParentId, CancellationToken cancellationToken = default)
    {
        if (!newParentId.HasValue) return false;
        if (newParentId.Value == categoryId) return true;

        // Traverse up hierarchy from newParentId
        var currentId = newParentId;
        var visited = new HashSet<int>();

        while (currentId.HasValue)
        {
            if (currentId.Value == categoryId)
            {
                return true; // Cycle detected!
            }

            if (!visited.Add(currentId.Value))
            {
                return true; // Malformed existing loop
            }

            var parent = await Context.Categories
                .AsNoTracking()
                .Where(c => c.Id == currentId.Value)
                .Select(c => c.ParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken);

            currentId = parent;
        }

        return false;
    }

    public async Task<bool> IsNameTakenUnderParentAsync(string name, int? parentId, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var cleanName = name.Trim().ToLower();
        var query = Context.Categories.Where(c => c.ParentCategoryId == parentId && c.Name.ToLower() == cleanName);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }
}

