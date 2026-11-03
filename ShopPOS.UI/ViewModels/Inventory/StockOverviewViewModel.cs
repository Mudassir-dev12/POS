using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Inventory;

public partial class StockOverviewViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;
    private readonly IBrandService _brandService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private int? _selectedCategoryId;

    [ObservableProperty]
    private int? _selectedBrandId;

    [ObservableProperty]
    private StockStatus? _selectedStatus;

    partial void OnSelectedStatusChanged(StockStatus? value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    [ObservableProperty]
    private bool _lowOrOutOnly;

    [ObservableProperty]
    private bool _includeNonTracked;

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
    private string _rowCountText = "Showing 0 products";

    [ObservableProperty]
    private bool _isCashier = false;

    [ObservableProperty]
    private StockOverviewSummaryDto? _summary;

    public ObservableCollection<StockOverviewItemDto> Items { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<BrandDto> Brands { get; } = new();

    public StockOverviewViewModel(
        IInventoryService inventoryService,
        ICategoryService categoryService,
        IBrandService brandService,
        IDialogService dialogService)
    {
        _inventoryService = inventoryService;
        _categoryService = categoryService;
        _brandService = brandService;
        _dialogService = dialogService;
        _isCashier = false;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadLookupsAsync();
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadLookupsAsync()
    {
        try
        {
            var catResult = await _categoryService.GetFlatListAsync(false);
            if (catResult.Success && catResult.Data != null)
            {
                Categories.Clear();
                foreach (var c in catResult.Data) Categories.Add(c);
            }

            var brandResult = await _brandService.GetFlatListAsync(false);
            if (brandResult.Success && brandResult.Data != null)
            {
                Brands.Clear();
                foreach (var b in brandResult.Data) Brands.Add(b);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var query = new StockOverviewFilterQuery
            {
                SearchTerm = SearchTerm,
                CategoryId = SelectedCategoryId,
                BrandId = SelectedBrandId,
                Status = SelectedStatus,
                LowOrOutOnly = LowOrOutOnly,
                IncludeNonTracked = IncludeNonTracked,
                PageNumber = CurrentPage,
                PageSize = PageSize
            };

            var pagedResult = await _inventoryService.GetStockOverviewAsync(query, "Owner");
            if (pagedResult.Success && pagedResult.Data != null)
            {
                Items.Clear();
                foreach (var item in pagedResult.Data.Items)
                {
                    Items.Add(item);
                }

                TotalCount = pagedResult.Data.TotalCount;
                TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
                int start = TotalCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
                int end = Math.Min(CurrentPage * PageSize, TotalCount);
                RowCountText = $"Showing {start}-{end} of {TotalCount} items";
            }
            else
            {
                ErrorMessage = pagedResult.ErrorMessage ?? "Failed to load stock overview.";
            }

            var sumResult = await _inventoryService.GetStockOverviewSummaryAsync("Owner");
            if (sumResult.Success)
            {
                Summary = sumResult.Data;
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
    public async Task ResetFiltersAsync()
    {
        SearchTerm = string.Empty;
        SelectedCategoryId = null;
        SelectedBrandId = null;
        SelectedStatus = null;
        LowOrOutOnly = false;
        IncludeNonTracked = false;
        CurrentPage = 1;
        await LoadDataAsync();
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
    public void AdjustStock(StockOverviewItemDto? item)
    {
        if (item == null) return;
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        try
        {
            string? filePath = _dialogService.ShowSaveFileDialog("CSV Files (*.csv)|*.csv", $"StockOverview_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var query = new StockOverviewFilterQuery
            {
                SearchTerm = SearchTerm,
                CategoryId = SelectedCategoryId,
                BrandId = SelectedBrandId,
                Status = SelectedStatus,
                LowOrOutOnly = LowOrOutOnly,
                IncludeNonTracked = IncludeNonTracked
            };

            var csvRes = await _inventoryService.ExportStockOverviewCsvAsync(query, "Owner");
            if (csvRes.Success && csvRes.Data != null)
            {
                await File.WriteAllBytesAsync(filePath, csvRes.Data);
                _dialogService.ShowMessage("Stock Overview CSV successfully exported.", "Export Complete");
            }
            else
            {
                _dialogService.ShowMessage($"Export failed: {csvRes.ErrorMessage}", "Export Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export failed: {ex.Message}", "Export Error");
        }
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        try
        {
            string? filePath = _dialogService.ShowSaveFileDialog("PDF Files (*.pdf)|*.pdf", $"StockOverview_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var query = new StockOverviewFilterQuery
            {
                SearchTerm = SearchTerm,
                CategoryId = SelectedCategoryId,
                BrandId = SelectedBrandId,
                Status = SelectedStatus,
                LowOrOutOnly = LowOrOutOnly,
                IncludeNonTracked = IncludeNonTracked
            };

            var pdfRes = await _inventoryService.ExportStockOverviewPdfAsync(query, "Owner");
            if (pdfRes.Success && pdfRes.Data != null)
            {
                await File.WriteAllBytesAsync(filePath, pdfRes.Data);
                _dialogService.ShowMessage("Stock Overview PDF successfully exported.", "Export Complete");
            }
            else
            {
                _dialogService.ShowMessage($"Export failed: {pdfRes.ErrorMessage}", "Export Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export failed: {ex.Message}", "Export Error");
        }
    }
}
