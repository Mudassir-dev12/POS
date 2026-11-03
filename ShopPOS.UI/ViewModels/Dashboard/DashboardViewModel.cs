using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly ISaleService _saleService;
    private readonly ISupplierService _supplierService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DashboardStatsDto? _stats;

    // Temporal Header
    [ObservableProperty]
    private string _todayDateFormatted = DateTime.Today.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

    // Sales & POS Metrics
    [ObservableProperty]
    private int _todaySalesCount;

    [ObservableProperty]
    private decimal _todaySalesTotal;

    [ObservableProperty]
    private decimal _todayProfit;

    [ObservableProperty]
    private string _salesVsYesterdayText = string.Empty;

    [ObservableProperty]
    private string _profitVsYesterdayText = string.Empty;

    [ObservableProperty]
    private string _salesCountVsYesterdayText = string.Empty;

    [ObservableProperty]
    private int _heldBillsCount;

    [ObservableProperty]
    private decimal _supplierPayables;

    // Daily Checklist
    [ObservableProperty]
    private bool _checklistSaleMade;

    [ObservableProperty]
    private bool _checklistLowStockChecked;

    [ObservableProperty]
    private bool _checklistBackupCurrent;

    public DashboardViewModel(
        IInventoryService inventoryService,
        ISaleService saleService,
        ISupplierService supplierService,
        ICurrentUserProvider currentUserProvider,
        INavigationService navigationService)
    {
        _inventoryService = inventoryService;
        _saleService = saleService;
        _supplierService = supplierService;
        _currentUserProvider = currentUserProvider;
        _navigationService = navigationService;
    }

    [RelayCommand]
    public void StartSelling()
    {
        _navigationService.NavigateTo("Sales");
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
    public void OpenProducts()
    {
        _navigationService.NavigateTo("Products");
    }

    [RelayCommand]
    public void OpenSalesList()
    {
        _navigationService.NavigateTo("SalesList");
    }

    [RelayCommand]
    public void OpenHeldBills()
    {
        _navigationService.NavigateTo("Sales");
    }

    [RelayCommand]
    public void OpenSupplierPayables()
    {
        _navigationService.NavigateTo("SupplierLedger");
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
        TodayDateFormatted = DateTime.Today.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

        try
        {
            // 1. Inventory & Stock Stats
            var result = await _inventoryService.GetDashboardStatsAsync("Owner");
            if (result.Success && result.Data != null)
            {
                Stats = result.Data;
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }

            // 2. Today's Sales
            var todayStart = DateTime.Today;
            var todayEnd = DateTime.Today.AddDays(1).AddTicks(-1);

            var todaySalesResult = await _saleService.SearchSalesAsync(new SaleSearchDto
            {
                DateFrom = todayStart,
                DateTo = todayEnd,
                Status = SaleStatus.Completed,
                PageSize = 1000
            });

            TodaySalesCount = todaySalesResult.TotalCount;
            TodaySalesTotal = todaySalesResult.Items.Sum(s => s.GrandTotal);
            TodayProfit = todaySalesResult.Items.Sum(s => s.Profit);

            // 3. Yesterday's Sales for genuine comparison
            var yesterdayStart = DateTime.Today.AddDays(-1);
            var yesterdayEnd = DateTime.Today.AddTicks(-1);

            var yesterdayResult = await _saleService.SearchSalesAsync(new SaleSearchDto
            {
                DateFrom = yesterdayStart,
                DateTo = yesterdayEnd,
                Status = SaleStatus.Completed,
                PageSize = 1000
            });

            decimal yesterdaySalesTotal = yesterdayResult.Items.Sum(s => s.GrandTotal);
            decimal yesterdayProfit = yesterdayResult.Items.Sum(s => s.Profit);
            int yesterdayCount = yesterdayResult.TotalCount;

            if (yesterdaySalesTotal > 0)
            {
                decimal salesDiff = ((TodaySalesTotal - yesterdaySalesTotal) / yesterdaySalesTotal) * 100m;
                SalesVsYesterdayText = $"{(salesDiff >= 0 ? "+" : "")}{salesDiff:N1}% vs yesterday";
            }
            else
            {
                SalesVsYesterdayText = string.Empty;
            }

            if (yesterdayProfit > 0)
            {
                decimal profitDiff = ((TodayProfit - yesterdayProfit) / yesterdayProfit) * 100m;
                ProfitVsYesterdayText = $"{(profitDiff >= 0 ? "+" : "")}{profitDiff:N1}% vs yesterday";
            }
            else
            {
                ProfitVsYesterdayText = string.Empty;
            }

            if (yesterdayCount > 0)
            {
                int countDiff = TodaySalesCount - yesterdayCount;
                SalesCountVsYesterdayText = $"{(countDiff >= 0 ? "+" : "")}{countDiff} vs yesterday";
            }
            else
            {
                SalesCountVsYesterdayText = string.Empty;
            }

            // 4. Held Bills
            var heldResult = await _saleService.ListHeldBillsAsync();
            HeldBillsCount = heldResult.Success && heldResult.Data != null ? heldResult.Data.Count : 0;

            // 5. Supplier Payables
            var suppliersRes = await _supplierService.GetFlatListAsync();
            if (suppliersRes.Success && suppliersRes.Data != null)
            {
                SupplierPayables = suppliersRes.Data.Sum(s => Math.Max(0m, s.CurrentBalance));
            }

            // 6. Checklist Status (derived from real data)
            ChecklistSaleMade = TodaySalesCount > 0;
            ChecklistLowStockChecked = Stats != null && Stats.LowStockCount == 0;
            ChecklistBackupCurrent = CheckIfBackupExists();
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

    private bool CheckIfBackupExists()
    {
        try
        {
            string backupDir = @"C:\ShopData\Backups";
            if (Directory.Exists(backupDir))
            {
                var files = Directory.GetFiles(backupDir, "*.db");
                return files.Length > 0;
            }
        }
        catch
        {
            // Ignore access errors
        }
        return false;
    }
}
