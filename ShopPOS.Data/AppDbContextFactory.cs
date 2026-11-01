using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShopPOS.Data.Interceptors;

namespace ShopPOS.Data;

/// <summary>
/// Design-time DbContext factory used by EF Core migration tools.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=C:\\ShopData\\shop.db")
                      .AddInterceptors(new SqlitePragmaInterceptor());

        return new AppDbContext(optionsBuilder.Options);
    }
}

