using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IShopSettingService _shopSettingService;
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly IProductRepository _productRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ISupplierService _supplierService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IDialogService _dialogService;

    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _shopName = "Shop POS";

    [ObservableProperty]
    private string _currentUserName = "Owner";

    [ObservableProperty]
    private string _currentUserRole = "Owner";

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        foreach (var item in AllNavigationItems)
        {
            item.IsSelected = (item == value);
        }
    }

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private string _currentTheme = "Light";

    // Global Search
    [ObservableProperty]
    private string _globalSearchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private bool _isSearching;

    public ObservableCollection<GlobalSearchResultItem> SearchResults { get; } = new();
    public ObservableCollection<NavigationGroup> NavigationGroups { get; } = new();
    public ObservableCollection<NavigationItem> AllNavigationItems { get; } = new();

    public MainWindowViewModel(
        ICurrentUserProvider currentUserProvider,
        IShopSettingService shopSettingService,
        INavigationService navigationService,
        IThemeService themeService,
        IProductRepository productRepository,
        ISaleRepository saleRepository,
        ISupplierService supplierService,
        IPurchaseRepository purchaseRepository,
        IDialogService dialogService)
    {
        _currentUserProvider = currentUserProvider;
        _shopSettingService = shopSettingService;
        _navigationService = navigationService;
        _themeService = themeService;
        _productRepository = productRepository;
        _saleRepository = saleRepository;
        _supplierService = supplierService;
        _purchaseRepository = purchaseRepository;
        _dialogService = dialogService;

        _currentTheme = _themeService.CurrentTheme;
        _themeService.ThemeChanged += theme => CurrentTheme = theme;

        _navigationService.CurrentViewChanged += view =>
        {
            CurrentView = view;
            UpdateSelectedNavigationItemForView(view);
        };

        PopulateNavigation();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            ShopName = settings.ShopName;
        }
        catch
        {
            ShopName = "Shop POS";
        }

        CurrentUserName = _currentUserProvider.FullName;
        if (string.IsNullOrWhiteSpace(CurrentUserName))
        {
            CurrentUserName = "Owner";
        }

        // Navigate to Dashboard initially if no view active
        if (CurrentView == null)
        {
            var dashboardItem = AllNavigationItems.FirstOrDefault(n => n.Key == "Dashboard");
            if (dashboardItem != null)
            {
                Navigate(dashboardItem);
            }
        }
    }

    private void PopulateNavigation()
    {
        NavigationGroups.Clear();
        AllNavigationItems.Clear();

        var sellItems = new List<NavigationItem>
        {
            new("Dashboard", "Dashboard", "Home", "", "SELL"),
            new("Sales", "Sales (POS)", "MoneyHand", "F2", "SELL"),
            new("SalesList", "Sales List", "DocumentBulletList", "", "SELL"),
            new("SaleReturns", "Sale Returns", "ArrowUndo", "", "SELL")
        };

        var stockItems = new List<NavigationItem>
        {
            new("Products", "Products", "Box", "", "STOCK"),
            new("Categories", "Categories", "Folder", "", "STOCK"),
            new("Brands", "Brands", "Tag", "", "STOCK"),
            new("Units", "Units", "Ruler", "", "STOCK"),
            new("StockOverview", "Stock Overview", "BoxMultiple", "", "STOCK"),
            new("StockMovements", "Stock Movements", "History", "", "STOCK"),
            new("StockAdjustments", "Stock Adjustments", "ArrowSync", "", "STOCK"),
            new("Reorder", "Reorder List", "CartAlert", "", "STOCK"),
            new("Valuation", "Stock Valuation", "DataUsage", "", "STOCK")
        };

        var buyingItems = new List<NavigationItem>
        {
            new("Suppliers", "Suppliers", "Building", "", "BUYING"),
            new("Purchases", "Purchases", "Cart", "", "BUYING"),
            new("SupplierPayments", "Supplier Payments", "Payment", "", "BUYING"),
            new("PurchaseReturns", "Purchase Returns", "ArrowUndo", "", "BUYING"),
            new("SupplierLedger", "Supplier Ledger", "DocumentBulletList", "", "BUYING")
        };

        var moneyItems = new List<NavigationItem>
        {
            new("Reports", "Reports", "ChartMultiple", "", "MONEY")
        };

        var systemItems = new List<NavigationItem>
        {
            new("Settings", "Settings", "Settings", "", "SYSTEM")
        };

        NavigationGroups.Add(new NavigationGroup("SELL", sellItems));
        NavigationGroups.Add(new NavigationGroup("STOCK", stockItems));
        NavigationGroups.Add(new NavigationGroup("BUYING", buyingItems));
        NavigationGroups.Add(new NavigationGroup("MONEY", moneyItems));
        NavigationGroups.Add(new NavigationGroup("SYSTEM", systemItems));

        foreach (var group in NavigationGroups)
        {
            foreach (var item in group.Items)
            {
                AllNavigationItems.Add(item);
            }
        }
    }

    [RelayCommand]
    public void Navigate(NavigationItem? item)
    {
        if (item == null) return;
        SelectedNavigationItem = item;
        _navigationService.NavigateTo(item.Key);
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        _themeService.ToggleTheme();
        CurrentTheme = _themeService.CurrentTheme;
    }

    partial void OnGlobalSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            SearchResults.Clear();
            IsSearchOpen = false;
            return;
        }

        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, token);
                if (token.IsCancellationRequested) return;

                await ExecuteGlobalSearchAsync(value.Trim(), token);
            }
            catch (OperationCanceledException)
            {
                // Debounce cancelled
            }
        }, token);
    }

    private async Task ExecuteGlobalSearchAsync(string term, CancellationToken ct)
    {
        IsSearching = true;
        var results = new List<GlobalSearchResultItem>();

        try
        {
            // 1. Products
            var prodResult = await _productRepository.GetPagedAsync(new ProductSearchQuery { SearchTerm = term, PageSize = 5 }, ct);
            foreach (var p in prodResult.Items)
            {
                results.Add(new GlobalSearchResultItem
                {
                    Group = "Products",
                    Title = p.Name,
                    Subtitle = $"SKU: {p.Sku} | Barcode: {p.Barcode ?? "—"}",
                    NavigationTarget = "Products",
                    EntityId = p.Id
                });
            }

            // 2. Sales (by sale number)
            var salesResult = await _saleRepository.SearchSalesAsync(new SaleSearchDto { SearchTerm = term, PageSize = 5 }, ct);
            foreach (var s in salesResult.Items)
            {
                results.Add(new GlobalSearchResultItem
                {
                    Group = "Sales",
                    Title = s.SaleNumber ?? s.HoldNumber ?? $"Sale #{s.Id}",
                    Subtitle = $"{s.SaleDate:dd MMM yyyy} | Total: Rs {s.GrandTotal:N2}",
                    NavigationTarget = "SalesList",
                    EntityId = s.Id
                });
            }

            // 3. Suppliers
            var suppliersRes = await _supplierService.GetFlatListAsync(false, null, ct);
            if (suppliersRes.Success && suppliersRes.Data != null)
            {
                var matchingSuppliers = suppliersRes.Data
                    .Where(s => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (s.Phone != null && s.Phone.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    .Take(5);

                foreach (var s in matchingSuppliers)
                {
                    results.Add(new GlobalSearchResultItem
                    {
                        Group = "Suppliers",
                        Title = s.Name,
                        Subtitle = $"Phone: {s.Phone ?? "—"}",
                        NavigationTarget = "Suppliers",
                        EntityId = s.Id
                    });
                }
            }

            // 4. Purchases (by purchase number)
            var purchasesResult = await _purchaseRepository.GetPurchasesAsync(new PurchaseSearchQuery { SearchTerm = term, PageSize = 5 }, ct);
            foreach (var p in purchasesResult.Items)
            {
                results.Add(new GlobalSearchResultItem
                {
                    Group = "Purchases",
                    Title = p.PurchaseNumber,
                    Subtitle = $"{p.PurchaseDate:dd MMM yyyy} | Total: Rs {p.GrandTotal:N2}",
                    NavigationTarget = "Purchases",
                    EntityId = p.Id
                });
            }
        }
        catch
        {
            // Ignore search cancellation / repository read exceptions
        }
        finally
        {
            IsSearching = false;
        }

        if (!ct.IsCancellationRequested)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                SearchResults.Clear();
                foreach (var r in results)
                {
                    SearchResults.Add(r);
                }
                IsSearchOpen = SearchResults.Count > 0;
            });
        }
    }

    [RelayCommand]
    public void SelectSearchResult(GlobalSearchResultItem? item)
    {
        if (item == null) return;

        IsSearchOpen = false;
        GlobalSearchText = string.Empty;

        _navigationService.NavigateTo(item.NavigationTarget);
    }

    [RelayCommand]
    public void CloseSearch()
    {
        IsSearchOpen = false;
        GlobalSearchText = string.Empty;
    }

    private void UpdateSelectedNavigationItemForView(ObservableObject? view)
    {
        if (view == null) return;
        string viewTypeName = view.GetType().Name;

        var matching = AllNavigationItems.FirstOrDefault(n =>
            viewTypeName.StartsWith(n.Key, StringComparison.OrdinalIgnoreCase) ||
            (n.Key == "Sales" && viewTypeName.Contains("PosSale")) ||
            (n.Key == "SalesList" && viewTypeName.Equals("SalesViewModel", StringComparison.OrdinalIgnoreCase)) ||
            (n.Key == "StockMovements" && viewTypeName.Contains("StockMovementHistory")));

        if (matching != null)
        {
            SelectedNavigationItem = matching;
        }
    }
}
