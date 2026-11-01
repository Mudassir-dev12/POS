using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Purchases;

public partial class ProductSearchItemViewModel : ObservableObject
{
    public ProductListItemDto Product { get; }
    public int Id => Product.Id;
    public string Sku => Product.Sku;
    public string? Barcode => Product.Barcode;
    public string Name => Product.Name;
    public string CategoryName => Product.CategoryName;
    public string UnitName => Product.UnitName;
    public decimal CostPrice => Product.CostPrice;
    public decimal CurrentStock => Product.CurrentStock;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isAlreadyInDocument;

    public ProductSearchItemViewModel(ProductListItemDto product, bool isAlreadyInDocument)
    {
        Product = product;
        _isAlreadyInDocument = isAlreadyInDocument;
    }
}

public partial class ProductSearchDialogViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly HashSet<int> _existingProductIds = new();
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ProductSearchItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _selectAll;

    public ObservableCollection<ProductSearchItemViewModel> Products { get; } = new();

    public List<ProductListItemDto> SelectedProducts { get; } = new();
    public ProductListItemDto? SelectedProduct => SelectedProducts.FirstOrDefault();

    public event Action<IReadOnlyList<ProductListItemDto>?>? RequestClose;

    public ProductSearchDialogViewModel(IProductService productService)
    {
        _productService = productService;
    }

    public void SetExistingProductIds(IEnumerable<int> ids)
    {
        _existingProductIds.Clear();
        foreach (var id in ids)
        {
            _existingProductIds.Add(id);
        }
    }

    public async Task InitializeAsync()
    {
        await SearchProductsAsync(string.Empty);
    }

    partial void OnSelectAllChanged(bool value)
    {
        foreach (var item in Products)
        {
            if (!item.IsAlreadyInDocument)
            {
                item.IsSelected = value;
            }
        }
    }

    partial void OnSearchTermChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                {
                    await SearchProductsAsync(value, token);
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    public async Task SearchProductsAsync(string term, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var query = new ProductSearchQuery
            {
                SearchTerm = term,
                IsActive = true,
                PageNumber = 1,
                PageSize = 50
            };

            var res = await _productService.GetPagedProductsAsync(query, cancellationToken);
            if (res.Success && res.Data != null)
            {
                Products.Clear();
                foreach (var p in res.Data.Items)
                {
                    bool isAlreadyAdded = _existingProductIds.Contains(p.Id);
                    Products.Add(new ProductSearchItemViewModel(p, isAlreadyAdded));
                }
                SelectedItem = Products.FirstOrDefault(p => !p.IsAlreadyInDocument) ?? Products.FirstOrDefault();
                SelectAll = false;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void AddSelected()
    {
        SelectedProducts.Clear();
        var checkedItems = Products.Where(p => p.IsSelected && !p.IsAlreadyInDocument).Select(p => p.Product).ToList();
        if (checkedItems.Count > 0)
        {
            SelectedProducts.AddRange(checkedItems);
        }
        else if (SelectedItem != null && !SelectedItem.IsAlreadyInDocument)
        {
            SelectedProducts.Add(SelectedItem.Product);
        }

        if (SelectedProducts.Count > 0)
        {
            RequestClose?.Invoke(SelectedProducts);
        }
    }

    [RelayCommand]
    public void ConfirmSelection()
    {
        if (SelectedItem != null && !SelectedItem.IsAlreadyInDocument)
        {
            SelectedProducts.Clear();
            SelectedProducts.Add(SelectedItem.Product);
            RequestClose?.Invoke(SelectedProducts);
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        SelectedProducts.Clear();
        RequestClose?.Invoke(null);
    }
}
