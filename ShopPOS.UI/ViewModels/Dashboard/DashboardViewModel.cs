using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly ISaleService _saleService;
    private readonly IShiftService _shiftService;
    private readonly ICustomerService _customerService;
    private readonly ICustomerBalanceQuery _customerBalanceQuery;
    private readonly IAuthService _authService;
    private readonly Services.INavigationService _navigationService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DashboardStatsDto? _stats;

    // Phase 5 Sales & Cash Metrics
    [ObservableProperty]
    private int _todaySalesCount;

    [ObservableProperty]
    private decimal _todaySalesTotal;

    [ObservableProperty]
    private decimal _todayProfit;

    [ObservableProperty]
    private decimal _cashInDrawerExpected;

    [ObservableProperty]
    private int _heldBillsCount;

    [ObservableProperty]
    private decimal _customersOwingTotal;

    [ObservableProperty]
    private bool _isCashier;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to ShopPOS";

    public DashboardViewModel(
        IInventoryService inventoryService,
        ISaleService saleService,
        IShiftService shiftService,
        ICustomerService customerService,
        ICustomerBalanceQuery customerBalanceQuery,
        IAuthService authService,
        Services.INavigationService navigationService)
    {
        _inventoryService = inventoryService;
        _saleService = saleService;
        _shiftService = shiftService;
        _customerService = customerService;
        _customerBalanceQuery = customerBalanceQuery;
        _authService = authService;
        _navigationService = navigationService;

        IsCashier = false;
        if (_authService.CurrentUser != null)
        {
            WelcomeMessage = $"Welcome back, {_authService.CurrentUser.FullName}";
        }
    }

    [RelayCommand]
    public void QuickStartPos()
    {
        _navigationService.NavigateTo("POS");
    }

    [RelayCommand]
    public void OpenLowStock()
    {
        _navigationService.NavigateTo("StockOverview", StockStatus.Low);
    }

    [RelayCommand]
    public void OpenOutOfStock()
    {
        _navigationService.NavigateTo("StockOverview", StockStatus.OutOfStock);
    }

    [RelayCommand]
    public void OpenSales()
    {
        _navigationService.NavigateTo("SalesList");
    }

    [RelayCommand]
    public void OpenHeldBills()
    {
        _navigationService.NavigateTo("POS");
    }

    [RelayCommand]
    public void OpenCustomersOwing()
    {
        _navigationService.NavigateTo("Customers");
    }

    [RelayCommand]
    public void OpenShifts()
    {
        _navigationService.NavigateTo("Shifts");
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadStatsAsync();
    }

    [RelayCommand]
    public async Task LoadStatsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var userRole = _authService.CurrentUser?.Role?.Name;
            var result = await _inventoryService.GetDashboardStatsAsync(userRole);
            if (result.Success)
            {
                Stats = result.Data;
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to load dashboard metrics.";
            }

            // Load Phase 5 Sales & POS Stats
            var todayStart = DateTime.Today;
            var todayEnd = DateTime.Today.AddDays(1).AddTicks(-1);

            var salesResult = await _saleService.SearchSalesAsync(new SaleSearchDto
            {
                DateFrom = todayStart,
                DateTo = todayEnd,
                Status = SaleStatus.Completed,
                PageSize = 1000
            });

            TodaySalesCount = salesResult.TotalCount;
            TodaySalesTotal = salesResult.Items.Sum(s => s.GrandTotal);
            TodayProfit = salesResult.Items.Sum(s => s.Profit);

            // Held bills count
            var heldResult = await _saleService.ListHeldBillsAsync();
            HeldBillsCount = heldResult.Success && heldResult.Data != null ? heldResult.Data.Count : 0;

            // Shift expected cash
            var shiftResult = await _shiftService.GetCurrentOpenShiftAsync();
            if (shiftResult.Success && shiftResult.Data != null)
            {
                var shiftSummary = await _shiftService.GetShiftSummaryAsync(shiftResult.Data.Id);
                CashInDrawerExpected = shiftSummary.Success && shiftSummary.Data != null ? shiftSummary.Data.ExpectedCash : 0m;
            }
            else
            {
                CashInDrawerExpected = 0m;
            }

            // Customers owing total
            var customersRes = await _customerService.GetFlatListAsync(false);
            if (customersRes.Success && customersRes.Data != null && customersRes.Data.Count > 0)
            {
                var customerIds = customersRes.Data.Select(c => c.Id).ToList();
                var balances = await _customerBalanceQuery.GetCustomerBalancesBatchAsync(customerIds);
                CustomersOwingTotal = balances.Values.Where(b => b > 0m).Sum();
            }
            else
            {
                CustomersOwingTotal = 0m;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
