using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.Products;

public partial class ProductsViewModel : BaseCrudViewModel<ProductListItemDto>
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IBrandService _brandService;
    private readonly IUnitService _unitService;
    private readonly IPriceLevelService _priceLevelService;
    private readonly ICsvProductService _csvProductService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private int? _filterCategoryId;

    [ObservableProperty]
    private int? _filterBrandId;

    [ObservableProperty]
    private bool _lowStockOnly = false;

    [ObservableProperty]
    private bool _outOfStockOnly = false;

    public ObservableCollection<CategoryDto> FilterCategories { get; } = new();
    public ObservableCollection<BrandDto> FilterBrands { get; } = new();

    public ProductsViewModel(
        IProductService productService,
        ICategoryService categoryService,
        IBrandService brandService,
        IUnitService unitService,
        IPriceLevelService priceLevelService,
        ICsvProductService csvProductService,
        IShopSettingService shopSettingService,
        IDialogService dialogService)
        : base(shopSettingService, dialogService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _brandService = brandService;
        _unitService = unitService;
        _priceLevelService = priceLevelService;
        _csvProductService = csvProductService;
        _dialogService = dialogService;
    }

    protected override void ApplyPermissions()
    {
        CanAdd = true;
        CanEdit = true;
        CanDelete = true;
        CanDeactivate = true;
    }

    public override async Task InitializeAsync()
    {
        // Pre-load filter dropdowns
        var catRes = await _categoryService.GetFlatListAsync(false);
        if (catRes != null && catRes.Success && catRes.Data != null)
        {
            FilterCategories.Clear();
            FilterCategories.Add(new CategoryDto { Id = 0, Name = "All Categories" });
            foreach (var c in catRes.Data) FilterCategories.Add(c);
        }

        var brandRes = await _brandService.GetFlatListAsync(false);
        if (brandRes != null && brandRes.Success && brandRes.Data != null)
        {
            FilterBrands.Clear();
            FilterBrands.Add(new BrandDto { Id = 0, Name = "All Brands" });
            foreach (var b in brandRes.Data) FilterBrands.Add(b);
        }

        await base.InitializeAsync();
    }

    partial void OnFilterCategoryIdChanged(int? value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    partial void OnFilterBrandIdChanged(int? value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    partial void OnLowStockOnlyChanged(bool value)
    {
        if (value) OutOfStockOnly = false;
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    partial void OnOutOfStockOnlyChanged(bool value)
    {
        if (value) LowStockOnly = false;
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    public override async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var query = new ProductSearchQuery
            {
                SearchTerm = SearchTerm,
                CategoryId = FilterCategoryId > 0 ? FilterCategoryId : null,
                BrandId = FilterBrandId > 0 ? FilterBrandId : null,
                IsActive = ShowInactive ? null : true,
                LowStockOnly = LowStockOnly,
                OutOfStockOnly = OutOfStockOnly,
                PageNumber = CurrentPage,
                PageSize = PageSize
            };

            var res = await _productService.GetPagedProductsAsync(query, cancellationToken);
            if (res.Success && res.Data != null)
            {
                Items.Clear();
                foreach (var item in res.Data.Items)
                {
                    Items.Add(item);
                }

                UpdatePagingState(res.Data.TotalCount, res.Data.PageNumber, res.Data.PageSize);
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task AddAsync()
    {
        if (!CanAdd) return;

        var dialogVm = new ProductDialogViewModel(
            _productService,
            _categoryService,
            _brandService,
            _unitService,
            _priceLevelService,
            _dialogService,
            GetCurrentUserId());

        await dialogVm.InitializeAsync(null);

        if (_dialogService.ShowProductDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task EditAsync()
    {
        if (!CanEdit || SelectedItem == null) return;

        var dialogVm = new ProductDialogViewModel(
            _productService,
            _categoryService,
            _brandService,
            _unitService,
            _priceLevelService,
            _dialogService,
            GetCurrentUserId());

        await dialogVm.InitializeAsync(SelectedItem.Id);

        if (_dialogService.ShowProductDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task DeleteOrDeactivateAsync()
    {
        if (!CanDelete || SelectedItem == null) return;

        int id = SelectedItem.Id;
        string name = SelectedItem.Name;

        // Try deleting first: service returns REFERENCED_CANNOT_DELETE if in use
        var delRes = await _productService.DeleteProductAsync(id, GetCurrentUserId());

        if (delRes.Success)
        {
            await LoadDataAsync();
            return;
        }

        if (delRes.ErrorMessage == "REFERENCED_CANNOT_DELETE")
        {
            // Offer deactivate dialog
            var result = _dialogService.ShowConfirmActionDialog(
                "Product In Use",
                $"Product '{name}' cannot be deleted because it is recorded in inventory movements or sales.\n\nWould you like to deactivate it instead? Deactivated products are archived and hidden from sales lists.",
                isReferenced: true);

            if (result == ConfirmActionResult.Deactivate)
            {
                await _productService.DeactivateProductAsync(id, GetCurrentUserId());
                await LoadDataAsync();
            }
        }
        else
        {
            ErrorMessage = delRes.ErrorMessage;
        }
    }

    [RelayCommand]
    public async Task ViewPriceHistoryAsync()
    {
        if (SelectedItem == null) return;

        var historyVm = new PriceHistoryDialogViewModel(_productService);
        await historyVm.InitializeAsync(SelectedItem.Id, SelectedItem.Name, SelectedItem.Sku);

        _dialogService.ShowPriceHistoryDialog(historyVm);
    }

    [RelayCommand]
    public async Task DuplicateProductAsync()
    {
        if (!CanAdd || SelectedItem == null) return;

        var res = await _productService.DuplicateProductAsync(SelectedItem.Id, GetCurrentUserId());
        if (res.Success && res.Data != null)
        {
            await LoadDataAsync();
        }
        else
        {
            ErrorMessage = res.ErrorMessage;
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        var filePath = _dialogService.ShowSaveFileDialog(
            "Excel Workbook (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv",
            $"Products_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            "Export Products Catalog (Excel or CSV)");

        if (!string.IsNullOrEmpty(filePath))
        {
            var query = new ProductSearchQuery
            {
                SearchTerm = SearchTerm,
                CategoryId = FilterCategoryId > 0 ? FilterCategoryId : null,
                BrandId = FilterBrandId > 0 ? FilterBrandId : null,
                IsActive = ShowInactive ? null : true,
                LowStockOnly = LowStockOnly
            };

            byte[] bytes;
            if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                bytes = await _csvProductService.ExportProductsToExcelAsync(query);
            }
            else
            {
                bytes = await _csvProductService.ExportProductsToCsvAsync(query);
            }

            await File.WriteAllBytesAsync(filePath, bytes);
        }
    }

    [RelayCommand]
    public async Task ImportCsvAsync()
    {
        if (!CanAdd) return;

        var wizardVm = new CsvImportWizardViewModel(_csvProductService, _dialogService, GetCurrentUserId());

        if (_dialogService.ShowCsvImportWizardDialog(wizardVm) == true)
        {
            await LoadDataAsync();
        }
    }

    protected override async Task<IReadOnlyList<int>> FetchAllFilteredIdsAsync()
    {
        var query = new ProductSearchQuery
        {
            SearchTerm = SearchTerm,
            CategoryId = FilterCategoryId,
            BrandId = FilterBrandId,
            IsActive = ShowInactive ? null : true,
            LowStockOnly = LowStockOnly,
            OutOfStockOnly = OutOfStockOnly
        };
        return await _productService.GetFilteredProductIdsAsync(query);
    }

    protected override async Task<BulkOperationResult> ExecuteBulkDeleteAsync(IReadOnlyList<int> ids)
    {
        return await _productService.BulkDeleteAsync(ids, GetCurrentUserId());
    }

    protected override async Task<BulkOperationResult> ExecuteBulkDeactivateAsync(IReadOnlyList<int> ids)
    {
        return await _productService.BulkDeactivateAsync(ids, GetCurrentUserId());
    }

}
