using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Seeding;

namespace ShopPOS.Data;

/// <summary>
/// Extension methods for configuring Data layer dependencies in DI.
/// </summary>
public static class DataServiceRegistration
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, DatabaseOptions dbOptions)
    {
        // Ensure folder exists
        var dbDirectory = Path.GetDirectoryName(dbOptions.DbPath);
        if (!string.IsNullOrWhiteSpace(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        services.AddSingleton<SqlitePragmaInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<SqlitePragmaInterceptor>();
            options.UseSqlite(dbOptions.ConnectionString)
                   .AddInterceptors(interceptor);
        });

        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IShopSettingRepository, ShopSettingRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IPriceLevelRepository, PriceLevelRepository>();

        // Phase 3 Purchase Repositories & Services
        services.AddScoped<ITransactionRunner, ShopPOS.Data.Services.TransactionRunner>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<ISupplierPaymentRepository, SupplierPaymentRepository>();
        services.AddScoped<IPurchaseReturnRepository, PurchaseReturnRepository>();
        services.AddScoped<ISupplierLedgerRepository, SupplierLedgerRepository>();
        services.AddScoped<INumberSequenceService, ShopPOS.Data.Services.NumberSequenceService>();
        services.AddScoped<IPdfReportService, ShopPOS.Data.Services.PdfReportService>();
        services.AddScoped<IStockLedgerQuery, ShopPOS.Data.Services.StockLedgerQuery>();
        services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();

        // Phase 5 Sales Repositories & Services
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleReturnRepository, SaleReturnRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<ICustomerBalanceQuery, ShopPOS.Data.Services.CustomerBalanceQuery>();
        services.AddScoped<ISalesExportService, ShopPOS.Data.Services.SalesExportService>();
#pragma warning disable CA1416
        services.AddScoped<IReceiptPrinterService, ShopPOS.Data.Services.ReceiptPrinterService>();
#pragma warning restore CA1416

        // Register CSV Service
        services.AddScoped<ICsvProductService, ShopPOS.Data.Services.CsvProductService>();

        // Register seeder
        services.AddScoped<IDataSeeder, DataSeeder>();

        return services;
    }
}
