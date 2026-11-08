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

        services.AddLogging();

        // Ensure folder exists

        var dbDirectory = Path.GetDirectoryName(dbOptions.DbPath);

        if (!string.IsNullOrWhiteSpace(dbDirectory) && !Directory.Exists(dbDirectory))

        {

            Directory.CreateDirectory(dbDirectory);

        }



        services.AddSingleton<SqlitePragmaInterceptor>();

        services.AddSingleton<DbCommandExecutionWatchdogInterceptor>();



        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<SqlitePragmaInterceptor>();
            var watchdog = sp.GetRequiredService<DbCommandExecutionWatchdogInterceptor>();
            options.UseSqlite(dbOptions.ConnectionString)
                   .AddInterceptors(interceptor, watchdog);
        });

        services.AddDbContext<AppDbContext>((sp, options) =>

        {

            var interceptor = sp.GetRequiredService<SqlitePragmaInterceptor>();

            var watchdog = sp.GetRequiredService<DbCommandExecutionWatchdogInterceptor>();

            options.UseSqlite(dbOptions.ConnectionString)

                   .AddInterceptors(interceptor, watchdog);

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

        services.AddScoped<ISalesExportService, ShopPOS.Data.Services.SalesExportService>();

        services.AddScoped<IProductSearchService, ShopPOS.Data.Services.ProductSearchService>();

#pragma warning disable CA1416

        services.AddScoped<IReceiptPrinterService, ShopPOS.Data.Services.ReceiptPrinterService>();

#pragma warning restore CA1416



        // Register CSV Service

        services.AddScoped<ICsvProductService, ShopPOS.Data.Services.CsvProductService>();



        // Barcode Management Service

        services.AddScoped<IBarcodeService, ShopPOS.Data.Services.BarcodeService>();







        // Phase 6B Reports Service



        services.AddScoped<IReportService, ShopPOS.Data.Services.ReportService>();



        // Phase 6A Data Tools Service

        services.AddScoped<IDataToolsService, ShopPOS.Data.Services.DataToolsService>();



        // Phase 7A Backup, Restore, and Database Safety Services

        services.AddSingleton<IBackupCryptoService, ShopPOS.Data.Services.BackupCryptoService>();

        services.AddScoped<IBackupService, ShopPOS.Data.Services.BackupService>();

        services.AddScoped<IRestoreService, ShopPOS.Data.Services.RestoreService>();

        services.AddSingleton<IDatabaseSafetyService, ShopPOS.Data.Services.DatabaseSafetyService>();

        services.AddSingleton<IBackupScheduler, ShopPOS.Data.Services.BackupScheduler>();



        // Phase 7B Google Drive Cloud Backup Services

        services.AddSingleton<IGoogleDriveConfigService, ShopPOS.Data.Services.GoogleDriveConfigService>();

        services.AddSingleton<IDriveTokenStore, ShopPOS.Data.Services.DpapiDriveTokenStore>();

        services.AddSingleton<IDriveClient, ShopPOS.Data.Services.GoogleDriveClient>();

        services.AddSingleton<ShopPOS.Data.Services.BackupUploader>(sp =>
            new ShopPOS.Data.Services.BackupUploader(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IDriveClient>(),
                sp.GetRequiredService<IDriveTokenStore>(),
                sp.GetRequiredService<IGoogleDriveConfigService>(),
                sp.GetRequiredService<IAppPaths>(),
                sp.GetService<Microsoft.Extensions.Logging.ILogger<ShopPOS.Data.Services.BackupUploader>>()));

        // Phase 7B-GitHub Private Repository Backup Services
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IGitHubTokenStore, ShopPOS.Data.Services.DpapiGitHubTokenStore>();
        services.AddSingleton<IGitHubClient, ShopPOS.Data.Services.GitHubClient>();
        services.AddSingleton<IGitHubBackupUploader, ShopPOS.Data.Services.GitHubBackupUploader>();

        // Composite Uploader (Bridge for uploader-agnostic calls)
        services.AddSingleton<IBackupUploader, ShopPOS.Data.Services.CompositeBackupUploader>();

        services.AddSingleton<ShopPOS.Data.Services.NetworkConnectivityService>();



        // Phase 7C Hardening, Diagnostics & Startup Services

        services.AddScoped<IDiagnosticsService, ShopPOS.Data.Services.DiagnosticsService>();
        services.AddScoped<ISelfCheckService, ShopPOS.Data.Services.SelfCheckService>();

        services.AddSingleton<IStartupSafetyChecker, ShopPOS.Data.Services.StartupSafetyChecker>();



        // Register seeder

        services.AddScoped<IDataSeeder, DataSeeder>();



        return services;

    }

}

