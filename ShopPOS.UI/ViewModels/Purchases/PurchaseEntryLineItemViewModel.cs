using CommunityToolkit.Mvvm.ComponentModel;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;

namespace ShopPOS.UI.ViewModels.Purchases;

public partial class PurchaseEntryLineItemViewModel : ObservableObject
{
    private readonly IPurchaseCalculator _calculator;
    private readonly Action? _onItemChanged;
    private bool _suppressRecalculate;

    public int? Id { get; set; }
    public int ProductId { get; set; }

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string? _barcode;

    [ObservableProperty]
    private string? _categoryName;

    [ObservableProperty]
    private string? _unitName;

    [ObservableProperty]
    private decimal _quantity = 1m;

    [ObservableProperty]
    private decimal _unitCost = 0m;

    [ObservableProperty]
    private decimal _discountPercent = 0m;

    [ObservableProperty]
    private decimal _taxPercent = 0m;

    [ObservableProperty]
    private decimal _lineTotal = 0m;

    [ObservableProperty]
    private decimal _effectiveNetUnitCost = 0m;

    public LineCalculationResult? LastCalculation { get; private set; }

    public PurchaseEntryLineItemViewModel(IPurchaseCalculator calculator, Action? onItemChanged = null)
    {
        _calculator = calculator;
        _onItemChanged = onItemChanged;
    }

    public void SetInitialValues(
        int productId,
        string productName,
        string sku,
        string? barcode,
        string? categoryName,
        string? unitName,
        decimal quantity,
        decimal unitCost,
        decimal discountPercent,
        decimal taxPercent)
    {
        _suppressRecalculate = true;
        try
        {
            ProductId = productId;
            ProductName = productName;
            Sku = sku;
            Barcode = barcode;
            CategoryName = categoryName;
            UnitName = unitName;
            Quantity = quantity;
            UnitCost = unitCost;
            DiscountPercent = discountPercent;
            TaxPercent = taxPercent;
        }
        finally
        {
            _suppressRecalculate = false;
        }

        Recalculate();
    }

    public void Recalculate()
    {
        if (_suppressRecalculate) return;

        LastCalculation = _calculator.CalculateLine(Quantity, UnitCost, DiscountPercent, TaxPercent);
        LineTotal = LastCalculation.LineTotal;
        EffectiveNetUnitCost = LastCalculation.EffectiveNetUnitCost;
        _onItemChanged?.Invoke();
    }

    partial void OnQuantityChanged(decimal value) => Recalculate();
    partial void OnUnitCostChanged(decimal value) => Recalculate();
    partial void OnDiscountPercentChanged(decimal value) => Recalculate();
    partial void OnTaxPercentChanged(decimal value) => Recalculate();
}
