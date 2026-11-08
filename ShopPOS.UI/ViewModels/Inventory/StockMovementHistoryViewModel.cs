using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Inventory;

public partial class StockMovementHistoryViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;
        private readonly IDialogService _dialogService;

    [ObservableProperty]
    private int? _selectedProductId;

    [ObservableProperty]
    private string? _selectedProductName;

    [ObservableProperty]
    private StockMovementType? _selectedMovementType;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private string _referenceSearch = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string _rowCountText = "Showing 0 movements";

    [ObservableProperty]
    private bool _showRunningBalance;

    public ObservableCollection<MovementHistoryItemDto> Items { get; } = new();

    public StockMovementHistoryViewModel(
        IInventoryService inventoryService,
        IProductService productService,
        IDialogService dialogService)
    {
        _inventoryService = inventoryService;
        _productService = productService;
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
        {            var query = new MovementHistoryFilterQuery
            {
                ProductId = SelectedProductId,
                MovementType = SelectedMovementType,
                FromDate = FromDate,
                ToDate = ToDate,
                ReferenceSearch = ReferenceSearch,
                PageNumber = CurrentPage,
                PageSize = PageSize
            };

            ShowRunningBalance = SelectedProductId.HasValue && SelectedProductId.Value > 0;

            var result = await _inventoryService.GetMovementHistoryAsync(query);
            if (result.Success && result.Data != null)
            {
                Items.Clear();
                foreach (var m in result.Data.Items)
                {
                    Items.Add(m);
                }

                TotalCount = result.Data.TotalCount;
                TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
                int start = TotalCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
                int end = Math.Min(CurrentPage * PageSize, TotalCount);
                RowCountText = $"Showing {start}-{end} of {TotalCount} movements";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to load movement history.";
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
    public async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task ClearFilterAsync()
    {
        SelectedProductId = null;
        SelectedProductName = null;
        SelectedMovementType = null;
        FromDate = null;
        ToDate = null;
        ReferenceSearch = string.Empty;
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    public void SelectProduct()
    {
        var vm = new Purchases.ProductSearchDialogViewModel(_productService);
        var product = _dialogService.ShowProductSearchDialog(vm);
        if (product != null)
        {
            SelectedProductId = product.Id;
            SelectedProductName = $"{product.Name} ({product.Sku})";
            CurrentPage = 1;
            _ = LoadDataAsync();
        }
    }

    [RelayCommand]
    public void ClearProduct()
    {
        SelectedProductId = null;
        SelectedProductName = null;
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        try
        {
            var filePath = _dialogService.ShowSaveFileDialog("CSV Files (*.csv)|*.csv", $"MovementHistory_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            if (string.IsNullOrWhiteSpace(filePath)) return;            var query = new MovementHistoryFilterQuery
            {
                ProductId = SelectedProductId,
                MovementType = SelectedMovementType,
                FromDate = FromDate,
                ToDate = ToDate,
                ReferenceSearch = ReferenceSearch,
                PageNumber = 1,
                PageSize = 10000
            };

            var exportResult = await _inventoryService.ExportMovementHistoryCsvAsync(query);
            if (exportResult.Success && exportResult.Data != null)
            {
                await File.WriteAllBytesAsync(filePath, exportResult.Data);
                _dialogService.ShowMessage("Movement history exported successfully.", "Export Complete");
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
