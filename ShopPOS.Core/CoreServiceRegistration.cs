using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;

namespace ShopPOS.Core;

/// <summary>
/// Extension methods for configuring Core domain services in DI.
/// </summary>
public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IUserSettingsService, UserSettingsService>();
        services.AddSingleton<ICurrentUserProvider, CurrentUserProvider>();
        services.AddSingleton<IMoneyFormatter>(MoneyFormatter.Instance);
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IShopSettingService, ShopSettingService>();

        // Phase 2 Master Data Services
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPriceLevelService, PriceLevelService>();

        // Phase 3 Purchase Services
        services.AddSingleton<IPurchaseCalculator, PurchaseCalculatorService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
        services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();
        services.AddScoped<ISupplierLedgerService, SupplierLedgerService>();

        // Phase 4 Inventory Services
        services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
        services.AddScoped<IInventoryService, InventoryService>();

        // Phase 5 Sales Services
        services.AddSingleton<ISaleCalculator, SaleCalculator>();
        services.AddScoped<IProductLookupService, ProductLookupService>();
        services.AddScoped<ISaleReturnService, SaleReturnService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddSingleton<IReceiptBuilder, ReceiptBuilder>();

        // Phase 3 Validators
        services.AddScoped<FluentValidation.IValidator<DTOs.CreatePurchaseDto>, Validators.CreatePurchaseValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.UpdatePurchaseDto>, Validators.UpdatePurchaseValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.CreateSupplierPaymentDto>, Validators.CreateSupplierPaymentValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.CreatePurchaseReturnDto>, Validators.CreatePurchaseReturnValidator>();

        // Phase 4 Validators
        services.AddScoped<FluentValidation.IValidator<DTOs.CreateStockAdjustmentDto>, Validators.CreateStockAdjustmentValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.UpdateStockAdjustmentDto>, Validators.UpdateStockAdjustmentValidator>();

        // Phase 5 Validators
        services.AddScoped<FluentValidation.IValidator<DTOs.SaveSaleDto>, Validators.SaveSaleValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.CompleteSaleDto>, Validators.CompleteSaleValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.CreateSaleReturnDto>, Validators.CreateSaleReturnValidator>();

        return services;
    }
}
