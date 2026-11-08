using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs.Reports;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Dashboard;

public class ChartBarItem
{
    public DateTime Date { get; set; }
    public decimal NetSales { get; set; }
    public double BarHeight { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public string FormattedAmount { get; set; } = string.Empty;
    public string TooltipText => $"{Date:dd MMM yyyy}: {FormattedAmount}";
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly IReportService? _reportService;
    private readonly INavigationService? _navigationService;
    private readonly ShopPOS.Core.Interfaces.IBackupService? _backupService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

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

    // Stock & Catalog Metrics
    [ObservableProperty]
    private int _lowStockCount;

    [ObservableProperty]
    private int _outOfStockCount;

    [ObservableProperty]
    private int _totalProductsCount;

    // Chart properties
    public ObservableCollection<ChartBarItem> ChartBars { get; } = new();

    [ObservableProperty]
    private string _chartMaxLabel = "Rs 0";

    [ObservableProperty]
    private string _chartMidLabel = "Rs 0";

    [ObservableProperty]
    private string _chartZeroLabel = "Rs 0";

    // Top 5 Products
    public ObservableCollection<TopSellingProductDto> Top5Products { get; } = new();

    [ObservableProperty]
    private bool _hasTopProducts;

    // Backup Status & Warning
    [ObservableProperty]
    private string _lastBackupText = "Never";

    [ObservableProperty]
    private bool _hasBackupWarning;

    [ObservableProperty]
    private string? _backupWarningMessage;

    // Cloud Backup Status & Warning
    [ObservableProperty]
    private bool _hasCloudBackupWarning;

    [ObservableProperty]
    private string? _cloudBackupWarningMessage;

    [ObservableProperty]
    private bool _isDriveConnected;

    [ObservableProperty]
    private string _lastDriveUploadText = "Never";

    // Daily Checklist
    [ObservableProperty]
    private bool _checklistSaleMade;

    [ObservableProperty]
    private bool _checklistLowStockChecked;

    [ObservableProperty]
    private bool _checklistBackupCurrent;

    public DashboardViewModel()
    {
        // Parameterless constructor for testing and designer
    }

    public DashboardViewModel(
        IReportService reportService,
        INavigationService navigationService,
        ShopPOS.Core.Interfaces.IBackupService? backupService = null)
    {
        _reportService = reportService;
        _navigationService = navigationService;
        _backupService = backupService;

        _ = InitializeAsync();
    }

    [RelayCommand]
    public async Task BackupNowAsync()
    {
        if (_backupService != null)
        {
            await _backupService.CreateBackupAsync(ShopPOS.Core.Enums.BackupKind.Manual);
            await LoadStatsAsync();
        }
    }

    [RelayCommand]
    public void StartSelling()
    {
        _navigationService?.NavigateTo("Sales");
    }

    [RelayCommand]
    public void OpenLowStock()
    {
        _navigationService?.NavigateTo("StockOverview", StockStatus.Low);
    }

    [RelayCommand]
    public void OpenOutOfStock()
    {
        _navigationService?.NavigateTo("StockOverview", StockStatus.OutOfStock);
    }

    [RelayCommand]
    public void OpenReorder()
    {
        _navigationService?.NavigateTo("Reorder");
    }

    [RelayCommand]
    public void OpenProducts()
    {
        _navigationService?.NavigateTo("Products");
    }

    [RelayCommand]
    public void OpenSalesList()
    {
        _navigationService?.NavigateTo("SalesList");
    }

    [RelayCommand]
    public void OpenHeldBills()
    {
        _navigationService?.NavigateTo("Sales");
    }

    [RelayCommand]
    public void OpenBackupRestore()
    {
        _navigationService?.NavigateTo("BackupRestore");
    }

    [RelayCommand]
    public void OpenSupplierPayables()
    {
        _navigationService?.NavigateTo("SupplierLedger");
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadStatsAsync();
    }

    [RelayCommand]
    public async Task LoadStatsAsync()
    {
        if (_reportService == null) return;

        IsLoading = true;
        ErrorMessage = null;
        TodayDateFormatted = DateTime.Today.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

        try
        {
            var summary = await _reportService.GetDashboardSummaryAsync();

            TodaySalesCount = summary.TodaySalesCount;
            TodaySalesTotal = summary.TodayNetSales;
            TodayProfit = summary.TodayGrossProfit;
            HeldBillsCount = summary.HeldBillsCount;
            SupplierPayables = summary.SupplierPayables;

            LowStockCount = summary.LowStockCount;
            OutOfStockCount = summary.OutOfStockCount;
            TotalProductsCount = summary.TotalProductsCount;

            // vs yesterday text (only when yesterday has data)
            if (summary.SalesVsYesterdayPercent.HasValue)
            {
                decimal diff = summary.SalesVsYesterdayPercent.Value;
                SalesVsYesterdayText = $"{(diff >= 0 ? "+" : "")}{diff:N1}% vs yesterday";
            }
            else
            {
                SalesVsYesterdayText = string.Empty;
            }

            if (summary.ProfitVsYesterdayPercent.HasValue)
            {
                decimal diff = summary.ProfitVsYesterdayPercent.Value;
                ProfitVsYesterdayText = $"{(diff >= 0 ? "+" : "")}{diff:N1}% vs yesterday";
            }
            else
            {
                ProfitVsYesterdayText = string.Empty;
            }

            if (summary.SalesCountVsYesterdayDiff.HasValue)
            {
                int diff = summary.SalesCountVsYesterdayDiff.Value;
                SalesCountVsYesterdayText = $"{(diff >= 0 ? "+" : "")}{diff} vs yesterday";
            }
            else
            {
                SalesCountVsYesterdayText = string.Empty;
            }

            // Backup Overview & Checklist
            if (_backupService != null)
            {
                var backupSummary = await _backupService.GetBackupSummaryAsync();
                LastBackupText = backupSummary.LastBackupAtFormatted ?? "Never";
                HasBackupWarning = backupSummary.HasWarning;
                BackupWarningMessage = backupSummary.WarningMessage;
                ChecklistBackupCurrent = backupSummary.LastBackupAt.HasValue && backupSummary.LastBackupAt.Value.Date == DateTime.Today;

                IsDriveConnected = backupSummary.IsDriveConnected;
                LastDriveUploadText = backupSummary.LastDriveUploadFormatted ?? "Never";

                HasCloudBackupWarning = false;
                CloudBackupWarningMessage = null;

                if (backupSummary.IsGitHubConnected)
                {
                    if (backupSummary.IsGitHubReconnectNeeded)
                    {
                        HasCloudBackupWarning = true;
                        CloudBackupWarningMessage = "GitHub personal access token has expired or is unauthorized. Please reconnect in Backup & Restore.";
                    }
                    else if (backupSummary.IsGitHubRepoPublic)
                    {
                        HasCloudBackupWarning = true;
                        CloudBackupWarningMessage = "GitHub repository is marked public. For security reasons, backups will only upload to a private repository.";
                    }
                    else if (backupSummary.IsGitHubFileTooLarge)
                    {
                        HasCloudBackupWarning = true;
                        CloudBackupWarningMessage = "Backup release asset exceeds 1.8 GiB. Please exclude images or archive older data.";
                    }
                    else if (backupSummary.TotalBackupsCount > 0 &&
                             (!backupSummary.LastGitHubUploadAt.HasValue || (DateTime.UtcNow - backupSummary.LastGitHubUploadAt.Value).TotalDays >= 7))
                    {
                        HasCloudBackupWarning = true;
                        CloudBackupWarningMessage = "Warning: No GitHub backup has succeeded in the last 7 days. Check your GitHub settings and internet connection.";
                    }
                }
                else if (backupSummary.IsDriveConnected)
                {
                    if (backupSummary.IsDriveReconnectNeeded)
                    {
                        HasCloudBackupWarning = true;
                        CloudBackupWarningMessage = "Google Drive authorization has expired. Please reconnect in Backup & Restore.";
                    }
                    else if (backupSummary.IsDriveFull)
                    {
                        HasCloudBackupWarning = true;
                        CloudBackupWarningMessage = "Google Drive storage quota is full. Cloud backup uploads are paused.";
                    }
                    else if (backupSummary.TotalBackupsCount > 0 &&
                             (!backupSummary.LastDriveUploadAt.HasValue || (DateTime.UtcNow - backupSummary.LastDriveUploadAt.Value).TotalDays >= 7))
                    {
                        HasCloudBackupWarning = true;
                        CloudBackupWarningMessage = "No backup has been uploaded to Google Drive in over 7 days. Please check your cloud connection.";
                    }
                }
            }
            else
            {
                ChecklistBackupCurrent = summary.DatabaseBackupPresent;
            }

            // Checklist
            ChecklistSaleMade = summary.HasCompletedSaleToday;
            ChecklistLowStockChecked = summary.LowStockReviewed;

            // Top 5 Products
            Top5Products.Clear();
            foreach (var p in summary.Top5ProductsToday)
            {
                Top5Products.Add(p);
            }
            HasTopProducts = Top5Products.Count > 0;

            // 30-Day Net Sales Chart
            ChartBars.Clear();
            decimal maxSales = summary.Last30DaysTrend.Count > 0 ? summary.Last30DaysTrend.Max(p => p.NetSales) : 0m;
            if (maxSales <= 0) maxSales = 1000m;

            ChartMaxLabel = $"Rs {maxSales:N0}";
            ChartMidLabel = $"Rs {(maxSales / 2m):N0}";
            ChartZeroLabel = "Rs 0";

            foreach (var point in summary.Last30DaysTrend)
            {
                double height = 0.0;
                if (point.NetSales > 0)
                {
                    height = (double)(point.NetSales / maxSales) * 120.0;
                    if (height < 4.0) height = 4.0;
                    if (height > 120.0) height = 120.0;
                }

                ChartBars.Add(new ChartBarItem
                {
                    Date = point.Date,
                    NetSales = point.NetSales,
                    BarHeight = height,
                    DateLabel = point.FormattedDate,
                    FormattedAmount = $"Rs {point.NetSales:N2}"
                });
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
