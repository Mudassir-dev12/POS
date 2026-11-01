using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Inventory;

public partial class StockValuationViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<CategoryValuationDto> Valuations { get; } = new();

    public int TotalProducts => Valuations.Sum(v => v.ProductCount);
    public decimal TotalUnits => Valuations.Sum(v => v.TotalUnits);
    public decimal TotalCostValue => Valuations.Sum(v => v.TotalCostValue);
    public decimal TotalRetailValue => Valuations.Sum(v => v.TotalRetailValue);
    public decimal TotalPotentialProfit => Valuations.Sum(v => v.PotentialProfit);
    public decimal OverallProfitMarginPercent => TotalRetailValue > 0 ? Math.Round((TotalRetailValue - TotalCostValue) / TotalRetailValue * 100m, 1, MidpointRounding.AwayFromZero) : 0m;

    public StockValuationViewModel(
        IInventoryService inventoryService,
        IAuthService authService,
        IDialogService dialogService)
    {
        _inventoryService = inventoryService;
        _authService = authService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var userRole = _authService.CurrentUser?.Role?.Name;
            var result = await _inventoryService.GetCategoryValuationsAsync(userRole);
            if (result.Success && result.Data != null)
            {
                Valuations.Clear();
                foreach (var v in result.Data)
                {
                    Valuations.Add(v);
                }

                OnPropertyChanged(nameof(TotalProducts));
                OnPropertyChanged(nameof(TotalUnits));
                OnPropertyChanged(nameof(TotalCostValue));
                OnPropertyChanged(nameof(TotalRetailValue));
                OnPropertyChanged(nameof(TotalPotentialProfit));
                OnPropertyChanged(nameof(OverallProfitMarginPercent));
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to load stock valuation.";
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

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        try
        {
            var filePath = _dialogService.ShowSaveFileDialog("PDF Files (*.pdf)|*.pdf", $"StockValuation_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var userRole = _authService.CurrentUser?.Role?.Name;
            var exportResult = await _inventoryService.ExportValuationPdfAsync(userRole);
            if (exportResult.Success && exportResult.Data != null)
            {
                await File.WriteAllBytesAsync(filePath, exportResult.Data);
                _dialogService.ShowMessage("Stock valuation report generated successfully.", "Export Complete");
            }
            else
            {
                _dialogService.ShowMessage(exportResult.ErrorMessage ?? "Failed to generate PDF.", "Export Failed");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export error: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        try
        {
            var filePath = _dialogService.ShowSaveFileDialog("CSV Files (*.csv)|*.csv", $"StockValuation_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var userRole = _authService.CurrentUser?.Role?.Name;
            var exportResult = await _inventoryService.ExportValuationCsvAsync(userRole);
            if (exportResult.Success && exportResult.Data != null)
            {
                await File.WriteAllBytesAsync(filePath, exportResult.Data);
                _dialogService.ShowMessage("Stock valuation exported successfully.", "Export Complete");
            }
            else
            {
                _dialogService.ShowMessage(exportResult.ErrorMessage ?? "Failed to export CSV.", "Export Failed");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export error: {ex.Message}", "Error");
        }
    }
}
