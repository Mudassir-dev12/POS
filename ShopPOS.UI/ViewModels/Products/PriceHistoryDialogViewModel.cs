using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Products;

public partial class PriceHistoryDialogViewModel : ObservableObject
{
    private readonly IProductService _productService;

    [ObservableProperty]
    private string _productTitle = string.Empty;

    public ObservableCollection<ProductPriceHistoryDto> Histories { get; } = new();

    public event Action? RequestClose;

    public PriceHistoryDialogViewModel(IProductService productService)
    {
        _productService = productService;
    }

    public async Task InitializeAsync(int productId, string productName, string sku)
    {
        ProductTitle = $"{productName} ({sku})";

        var res = await _productService.GetPriceHistoryAsync(productId);
        if (res.Success && res.Data != null)
        {
            Histories.Clear();
            foreach (var h in res.Data)
            {
                Histories.Add(h);
            }
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}

