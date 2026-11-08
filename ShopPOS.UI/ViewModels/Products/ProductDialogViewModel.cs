using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Products;

public partial class ProductDialogViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IBrandService _brandService;
    private readonly IUnitService _unitService;
    private readonly IPriceLevelService _priceLevelService;
    private readonly IDialogService _dialogService;
    private readonly int? _currentUserId;
    
    [ObservableProperty]
    private string _dialogTitle = "Add Product";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    private bool _isCreateMode = true;

    public bool IsEditMode => !IsCreateMode;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _barcode = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private string _fullImagePreviewPath = string.Empty;

    [ObservableProperty]
    private int _selectedCategoryId;

    [ObservableProperty]
    private int? _selectedBrandId;

    [ObservableProperty]
    private int _selectedUnitId;

    [ObservableProperty]
    private decimal _costPrice;

    [ObservableProperty]
    private decimal _salePrice;

    [ObservableProperty]
    private decimal? _minSalePrice;

    [ObservableProperty]
    private decimal _taxPercent;

    [ObservableProperty]
    private decimal _reorderLevel = 10m;

    [ObservableProperty]
    private decimal _openingQuantity = 0m;

    [ObservableProperty]
    private decimal _currentStock = 0m;

    [ObservableProperty]
    private decimal? _adjustedStock;

    [ObservableProperty]
    private string _stockAdjustmentNote = string.Empty;

    [ObservableProperty]
    private bool _trackStock = true;

    [ObservableProperty]
    private bool _isWeighable = false;

    [ObservableProperty]
    private bool _allowDecimalQuantity = false;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _newAdditionalBarcode = string.Empty;

    // Field-specific validation errors for inline display
    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _skuError;

    [ObservableProperty]
    private string? _categoryError;

    [ObservableProperty]
    private string? _unitError;

    [ObservableProperty]
    private string? _costPriceError;

    [ObservableProperty]
    private string? _salePriceError;

    [ObservableProperty]
    private string? _minSalePriceError;

    [ObservableProperty]
    private string? _generalError;

    [ObservableProperty]
    private string? _barcodeWarning;

    [ObservableProperty]
    private string? _stockAdjustmentError;

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<BrandDto> Brands { get; } = new();
    public ObservableCollection<UnitDto> Units { get; } = new();
    public ObservableCollection<string> AdditionalBarcodes { get; } = new();
    public ObservableCollection<ProductPriceDto> ProductPrices { get; } = new();

    public event Action<bool>? RequestClose;

    public ProductDialogViewModel(
        IProductService productService,
        ICategoryService categoryService,
        IBrandService brandService,
        IUnitService unitService,
        IPriceLevelService priceLevelService,
        IDialogService dialogService,
        int? currentUserId)
    {
        _productService = productService;
        _categoryService = categoryService;
        _brandService = brandService;
        _unitService = unitService;
        _priceLevelService = priceLevelService;
        _dialogService = dialogService;
        _currentUserId = currentUserId;    }

    public async Task InitializeAsync(int? productId = null)
    {
        // 1. Load Dropdowns
        var catRes = await _categoryService.GetFlatListAsync(false);
        if (catRes != null && catRes.Success && catRes.Data != null)
        {
            Categories.Clear();
            foreach (var c in catRes.Data)
            {
                Categories.Add(c);
            }
        }

        var brandRes = await _brandService.GetPagedBrandsAsync(new MasterDataSearchQuery { PageSize = 1000, IsActive = true });
        if (brandRes != null && brandRes.Success && brandRes.Data != null)
        {
            Brands.Clear();
            foreach (var b in brandRes.Data.Items)
            {
                Brands.Add(b);
            }
        }

        var unitRes = await _unitService.GetPagedUnitsAsync(new MasterDataSearchQuery { PageSize = 1000, IsActive = true });
        if (unitRes != null && unitRes.Success && unitRes.Data != null)
        {
            Units.Clear();
            foreach (var u in unitRes.Data.Items)
            {
                Units.Add(u);
            }
        }

        // Load price levels
        var priceLevelsRes = await _priceLevelService.GetAllAsync();
        var allLevels = priceLevelsRes != null && priceLevelsRes.Success && priceLevelsRes.Data != null
            ? priceLevelsRes.Data
            : new List<PriceLevelDto>();

        if (productId.HasValue && productId.Value > 0)
        {
            // Edit Mode
            IsCreateMode = false;
            DialogTitle = "Edit Product";

            var prodRes = await _productService.GetProductByIdAsync(productId.Value);
            if (prodRes.Success && prodRes.Data != null)
            {
                var p = prodRes.Data;
                Id = p.Id;
                Name = p.Name;
                Sku = p.Sku;
                Barcode = p.Barcode ?? string.Empty;
                Description = p.Description ?? string.Empty;
                ImagePath = p.ImagePath ?? string.Empty;
                SelectedCategoryId = p.CategoryId;
                SelectedBrandId = p.BrandId;
                SelectedUnitId = p.UnitId;
                CostPrice = p.CostPrice;
                SalePrice = p.SalePrice;
                MinSalePrice = p.MinSalePrice;
                TaxPercent = p.TaxPercent;
                ReorderLevel = p.ReorderLevel;
                CurrentStock = p.CurrentStock;
                AdjustedStock = p.CurrentStock;
                TrackStock = p.TrackStock;
                IsWeighable = p.IsWeighable;
                AllowDecimalQuantity = p.AllowDecimalQuantity;
                IsActive = p.IsActive;

                AdditionalBarcodes.Clear();
                foreach (var ab in p.AdditionalBarcodes)
                {
                    AdditionalBarcodes.Add(ab.Barcode);
                }

                ProductPrices.Clear();
                foreach (var level in allLevels)
                {
                    var existingPrice = p.ProductPrices.FirstOrDefault(pr => pr.PriceLevelId == level.Id);
                    ProductPrices.Add(new ProductPriceDto
                    {
                        PriceLevelId = level.Id,
                        PriceLevelName = level.Name,
                        Price = existingPrice?.Price ?? 0m
                    });
                }

                UpdateImagePreview(ImagePath);
            }
        }
        else
        {
            // Create Mode
            IsCreateMode = true;
            DialogTitle = "Add Product";
            IsActive = true;
            TrackStock = true;
            ReorderLevel = 10m;
            CostPrice = 0.00m;
            SalePrice = 0.00m;
            OpeningQuantity = 0m;

            if (Categories.Any()) SelectedCategoryId = Categories.First().Id;
            if (Units.Any()) SelectedUnitId = Units.First().Id;

            ProductPrices.Clear();
            foreach (var level in allLevels)
            {
                ProductPrices.Add(new ProductPriceDto
                {
                    PriceLevelId = level.Id,
                    PriceLevelName = level.Name,
                    Price = 0m
                });
            }

            // Auto-generate SKU preview
            var skuRes = await _productService.GenerateNextSkuAsync();
            if (skuRes.Success && !string.IsNullOrWhiteSpace(skuRes.Data))
            {
                Sku = skuRes.Data;
            }
        }
    }

    [RelayCommand]
    public async Task CheckBarcodeUniquenessAsync()
    {
        BarcodeWarning = null;
        if (string.IsNullOrWhiteSpace(Barcode)) return;

        var conflictRes = await _productService.ValidateBarcodeAsync(Barcode, IsCreateMode ? null : Id);
        if (!conflictRes.Success)
        {
            BarcodeWarning = conflictRes.ErrorMessage;
        }
    }

    [RelayCommand]
    public void AddAdditionalBarcode()
    {
        BarcodeWarning = null;
        if (string.IsNullOrWhiteSpace(NewAdditionalBarcode)) return;

        string code = NewAdditionalBarcode.Trim();
        if (code.Equals(Barcode, StringComparison.OrdinalIgnoreCase) || AdditionalBarcodes.Contains(code))
        {
            BarcodeWarning = $"Barcode '{code}' is already added to this product.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Barcode))
        {
            Barcode = code;
        }
        else
        {
            AdditionalBarcodes.Add(code);
        }

        NewAdditionalBarcode = string.Empty;
    }

    [RelayCommand]
    public void SetAsPrimaryBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;

        string oldPrimary = Barcode;
        Barcode = barcode;
        AdditionalBarcodes.Remove(barcode);
        if (!string.IsNullOrWhiteSpace(oldPrimary))
        {
            AdditionalBarcodes.Add(oldPrimary);
        }
    }

    [RelayCommand]
    public void RemoveAdditionalBarcode(string barcode)
    {
        if (AdditionalBarcodes.Contains(barcode))
        {
            AdditionalBarcodes.Remove(barcode);
        }
    }

    [RelayCommand]
    public void BrowseImage()
    {
        string? selectedFile = _dialogService.ShowOpenFileDialog(
            "Image Files (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
            "Select Product Image");

        if (!string.IsNullOrEmpty(selectedFile))
        {
            try
            {
                string targetDir = @"C:\ShopData\Images";
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string ext = Path.GetExtension(selectedFile);
                string newFileName = $"prod_{Guid.NewGuid():N}{ext}";
                string destPath = Path.Combine(targetDir, newFileName);

                File.Copy(selectedFile, destPath, overwrite: true);

                ImagePath = $"Images/{newFileName}";
                FullImagePreviewPath = destPath;
            }
            catch (Exception ex)
            {
                GeneralError = $"Failed to copy image: {ex.Message}";
            }
        }
    }

    private void UpdateImagePreview(string? relPath)
    {
        if (!string.IsNullOrWhiteSpace(relPath))
        {
            string fullPath = Path.Combine(@"C:\ShopData", relPath.Replace('/', '\\'));
            if (File.Exists(fullPath))
            {
                FullImagePreviewPath = fullPath;
                return;
            }
        }
        FullImagePreviewPath = string.Empty;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ClearErrors();

        // Warning if sale price < cost price
        if (SalePrice > 0 && CostPrice > 0 && SalePrice < CostPrice)
        {
            GeneralError = "Note: Sale price is lower than cost price.";
        }

        if (IsCreateMode)
        {
            var createDto = new CreateProductDto
            {
                Name = Name,
                Sku = Sku,
                Barcode = Barcode,
                Description = Description,
                ImagePath = ImagePath,
                CategoryId = SelectedCategoryId,
                BrandId = SelectedBrandId,
                UnitId = SelectedUnitId,
                CostPrice = CostPrice,
                SalePrice = SalePrice,
                MinSalePrice = MinSalePrice,
                TaxPercent = TaxPercent,
                ReorderLevel = ReorderLevel,
                OpeningQuantity = OpeningQuantity,
                TrackStock = TrackStock,
                IsWeighable = IsWeighable,
                AllowDecimalQuantity = AllowDecimalQuantity,
                AdditionalBarcodes = AdditionalBarcodes.ToList(),
                ProductPrices = ProductPrices.Where(p => p.Price > 0).ToList()
            };

            var res = await _productService.CreateProductAsync(createDto, _currentUserId);
            if (res.Success)
            {
                RequestClose?.Invoke(true);
            }
            else
            {
                ApplyValidationErrors(res);
            }
        }
        else
        {
            if (AdjustedStock.HasValue && AdjustedStock.Value != CurrentStock && string.IsNullOrWhiteSpace(StockAdjustmentNote))
            {
                StockAdjustmentError = "A reason is required to adjust stock quantity.";
                GeneralError = StockAdjustmentError;
                return;
            }

            var updateDto = new UpdateProductDto
            {
                Id = Id,
                Name = Name,
                Sku = Sku,
                Barcode = Barcode,
                Description = Description,
                ImagePath = ImagePath,
                CategoryId = SelectedCategoryId,
                BrandId = SelectedBrandId,
                UnitId = SelectedUnitId,
                CostPrice = CostPrice,
                SalePrice = SalePrice,
                MinSalePrice = MinSalePrice,
                TaxPercent = TaxPercent,
                ReorderLevel = ReorderLevel,
                TrackStock = TrackStock,
                IsWeighable = IsWeighable,
                AllowDecimalQuantity = AllowDecimalQuantity,
                IsActive = IsActive,
                AdditionalBarcodes = AdditionalBarcodes.ToList(),
                ProductPrices = ProductPrices.ToList(),
                NewStockQuantity = AdjustedStock,
                StockAdjustmentNote = StockAdjustmentNote
            };

            var res = await _productService.UpdateProductAsync(updateDto, _currentUserId);
            if (res.Success)
            {
                RequestClose?.Invoke(true);
            }
            else
            {
                ApplyValidationErrors(res);
            }
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void ClearErrors()
    {
        NameError = null;
        SkuError = null;
        CategoryError = null;
        UnitError = null;
        CostPriceError = null;
        SalePriceError = null;
        MinSalePriceError = null;
        StockAdjustmentError = null;
        GeneralError = null;
    }

    private void ApplyValidationErrors(Result result)
    {
        GeneralError = result.ErrorMessage;

        if (result.ValidationErrors.TryGetValue(nameof(Name), out var nameErrors))
            NameError = string.Join("; ", nameErrors);

        if (result.ValidationErrors.TryGetValue(nameof(Sku), out var skuErrors))
            SkuError = string.Join("; ", skuErrors);

        if (result.ValidationErrors.TryGetValue(nameof(SelectedCategoryId), out var catErrors) ||
            result.ValidationErrors.TryGetValue("CategoryId", out catErrors))
            CategoryError = string.Join("; ", catErrors);

        if (result.ValidationErrors.TryGetValue(nameof(SelectedUnitId), out var unitErrors) ||
            result.ValidationErrors.TryGetValue("UnitId", out unitErrors))
            UnitError = string.Join("; ", unitErrors);

        if (result.ValidationErrors.TryGetValue(nameof(CostPrice), out var costErrors))
            CostPriceError = string.Join("; ", costErrors);

        if (result.ValidationErrors.TryGetValue(nameof(SalePrice), out var saleErrors))
            SalePriceError = string.Join("; ", saleErrors);

        if (result.ValidationErrors.TryGetValue(nameof(MinSalePrice), out var minErrors))
            MinSalePriceError = string.Join("; ", minErrors);

        if (result.ValidationErrors.TryGetValue(nameof(AdjustedStock), out var stockErrors) ||
            result.ValidationErrors.TryGetValue("StockAdjustmentNote", out stockErrors) ||
            result.ValidationErrors.TryGetValue("NewStockQuantity", out stockErrors))
        {
            StockAdjustmentError = string.Join("; ", stockErrors);
        }
        else if (!string.IsNullOrWhiteSpace(result.ErrorMessage) &&
            (result.ErrorMessage.Contains("reason", StringComparison.OrdinalIgnoreCase) ||
             result.ErrorMessage.Contains("adjust", StringComparison.OrdinalIgnoreCase) ||
             result.ErrorMessage.Contains("stock", StringComparison.OrdinalIgnoreCase)))
        {
            StockAdjustmentError = result.ErrorMessage;
        }
    }
}
