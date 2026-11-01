using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<ICurrentUserProvider, CurrentUserProvider>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IShopSettingService, ShopSettingService>();
        services.AddScoped<IAuthService, AuthService>();

        // Phase 2 Master Data Services
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ICustomerService, CustomerService>();
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

        // Phase 3 Validators
        services.AddScoped<FluentValidation.IValidator<DTOs.CreatePurchaseDto>, Validators.CreatePurchaseValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.UpdatePurchaseDto>, Validators.UpdatePurchaseValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.CreateSupplierPaymentDto>, Validators.CreateSupplierPaymentValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.CreatePurchaseReturnDto>, Validators.CreatePurchaseReturnValidator>();

        // Phase 4 Validators
        services.AddScoped<FluentValidation.IValidator<DTOs.CreateStockAdjustmentDto>, Validators.CreateStockAdjustmentValidator>();
        services.AddScoped<FluentValidation.IValidator<DTOs.UpdateStockAdjustmentDto>, Validators.UpdateStockAdjustmentValidator>();

        return services;
    }
}
