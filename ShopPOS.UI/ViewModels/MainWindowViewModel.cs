using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;
    private readonly IShopSettingService _shopSettingService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _shopName = "Shop POS";

    [ObservableProperty]
    private string _currentUserName = string.Empty;

    [ObservableProperty]
    private string _currentUserRole = string.Empty;

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    private ObservableObject? _currentView;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public MainWindowViewModel(
        IAuthService authService,
        IPermissionService permissionService,
        IShopSettingService shopSettingService,
        INavigationService navigationService)
    {
        _authService = authService;
        _permissionService = permissionService;
        _shopSettingService = shopSettingService;
        _navigationService = navigationService;

        _navigationService.CurrentViewChanged += view => CurrentView = view;
    }

    public async Task InitializeAsync()
    {
        // 1. Load Shop Settings
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            ShopName = settings.ShopName;
        }
        catch
        {
            ShopName = "Shop POS";
        }

        // 2. Load User Information
        var user = _authService.CurrentUser;
        CurrentUserName = user?.FullName ?? "Owner";
        CurrentUserRole = "Owner";

        // 3. Populate Navigation Items
        PopulateNavigationItems();
    }

    private void PopulateNavigationItems()
    {
        NavigationItems.Clear();

        var candidateItems = new List<NavigationItem>
        {
            new("Dashboard", "Dashboard", "Home24"),
            new("Sales", "Sales (POS)", "MoneyHand24"),
            new("SalesList", "Sales List", "DocumentBulletList24"),
            new("SaleReturns", "Sale Returns", "ArrowUndo24"),
            new("Shifts", "Shifts", "Clock24"),
            new("Products", "Products", "Box24"),
            new("Categories", "Categories", "Folder24"),
            new("Brands", "Brands", "Tag24"),
            new("Units", "Units", "Ruler24"),
            new("Customers", "Customers", "People24"),
            new("Suppliers", "Suppliers", "Building24"),
            new("Purchases", "Purchases", "Cart24"),
            new("SupplierPayments", "Supplier Payments", "Payment24"),
            new("PurchaseReturns", "Purchase Returns", "ArrowUndo24"),
            new("SupplierLedger", "Supplier Ledger", "DocumentBulletList24"),
            new("StockOverview", "Stock Overview", "BoxMultiple24"),
            new("StockMovements", "Stock Movements", "History24"),
            new("StockAdjustments", "Stock Adjustments", "ArrowSync24"),
            new("Reorder", "Reorder List", "CartAlert24"),
            new("Valuation", "Stock Valuation", "DataUsage24"),
            new("Reports", "Reports", "ChartMultiple24"),
            new("Settings", "Settings", "Settings24")
        };

        foreach (var item in candidateItems)
        {
            NavigationItems.Add(item);
        }

        // Select initial item
        var firstItem = NavigationItems.FirstOrDefault();
        if (firstItem != null)
        {
            SelectedNavigationItem = firstItem;
            _navigationService.NavigateTo(firstItem.Key);
        }
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value != null)
        {
            _navigationService.NavigateTo(value.Key);
        }
    }
}
