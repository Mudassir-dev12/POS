using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.HeldBills;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class PosProductSuggestion : ObservableObject
{
    public PosProductSearchResult? SearchResult { get; }
    public ProductListItemDto? Product { get; }

    public int ProductId => SearchResult?.ProductId ?? Product?.Id ?? 0;
    public string Name => SearchResult?.Name ?? Product?.Name ?? string.Empty;
    public string Sku => SearchResult?.Sku ?? Product?.Sku ?? string.Empty;
    public string? Barcode => SearchResult?.Barcode ?? Product?.Barcode;
    public decimal SalePrice => SearchResult?.SalePrice ?? Product?.SalePrice ?? 0m;
    public decimal CurrentStock => SearchResult?.CurrentStock ?? Product?.CurrentStock ?? 0m;
    public StockStatus StockStatus => SearchResult?.StockStatus ?? (Product != null && !Product.TrackStock ? StockStatus.NotTracked : (Product != null && Product.CurrentStock <= 0 ? StockStatus.OutOfStock : (Product != null && Product.CurrentStock <= Product.ReorderLevel ? StockStatus.Low : StockStatus.OK)));
    public string StockStatusDisplay => SearchResult?.StockStatusDisplay ?? StockStatus.ToString();
    public string UnitShortName => SearchResult?.UnitShortName ?? Product?.UnitShortName ?? string.Empty;
    public decimal ParsedQuantity => SearchResult?.ParsedQuantity ?? 1m;

    public PosProductSuggestion(PosProductSearchResult result)
    {
        SearchResult = result;
    }

    public PosProductSuggestion(ProductListItemDto product)
    {
        Product = product;
    }
}

public partial class PosSaleViewModel : ObservableObject
{
    private readonly ISaleService _saleService;
    private readonly ISaleCalculator _saleCalculator;
    private readonly IProductLookupService _productLookupService;
    private readonly IProductService _productService;
    private readonly IPriceLevelService _priceLevelService;
    private readonly IStockLedgerQuery _stockLedgerQuery;
    private readonly IShopSettingService _shopSettingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly IReceiptPrinterService _receiptPrinterService;
    private readonly IReceiptBuilder? _receiptBuilder;
    private readonly IAuditLogService? _auditLogService;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly IProductSearchService? _productSearchService;
    private CancellationTokenSource? _scanDebounceCts;

    [ObservableProperty]
    private bool _hasSuggestions;

    // Scan box & suggestions
    [ObservableProperty]
    private string _scanInput = string.Empty;

    [ObservableProperty]
    private string? _scanErrorMessage;

    [ObservableProperty]
    private bool _showSuggestions;

    [ObservableProperty]
    private PosProductSuggestion? _selectedSuggestion;

    public ObservableCollection<PosProductSuggestion> Suggestions { get; } = new();

    // Cart
    public ObservableCollection<PosCartLineItemViewModel> CartItems { get; } = new();

    [ObservableProperty]
    private PosCartLineItemViewModel? _selectedCartItem;

    // Price Levels
    public ObservableCollection<PriceLevelDto> PriceLevels { get; } = new();

    [ObservableProperty]
    private PriceLevelDto? _selectedPriceLevel;

    partial void OnSelectedPriceLevelChanged(PriceLevelDto? value)
    {
        if (value == null || CartItems.Count == 0) return;
        _ = UpdateCartPricesForSelectedPriceLevelAsync(value.Id);
    }

    public async Task UpdateCartPricesForSelectedPriceLevelAsync(int priceLevelId)
    {
        foreach (var item in CartItems)
        {
            var pRes = await _productService.GetProductByIdAsync(item.ProductId);
            if (pRes.Success && pRes.Data != null)
            {
                var tier = pRes.Data.Prices.FirstOrDefault(pr => pr.PriceLevelId == priceLevelId);
                item.UnitPrice = tier != null && tier.Price > 0 ? tier.Price : pRes.Data.SalePrice;
            }
        }
        RecalculateTotals();
    }

    // Notes & Discounts
    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private decimal _billDiscountPercent;

    [ObservableProperty]
    private decimal _billDiscountAmount;

    // Computed Totals (All from ISaleCalculator)
    [ObservableProperty]
    private int _itemsCount;

    [ObservableProperty]
    private decimal _totalUnits;

    [ObservableProperty]
    private decimal _subTotal;

    [ObservableProperty]
    private decimal _lineDiscountTotal;

    [ObservableProperty]
    private decimal _taxAmount;

    [ObservableProperty]
    private decimal _roundOff;

    [ObservableProperty]
    private decimal _grandTotal;

    // System Settings & State
    [ObservableProperty]
    private string _currencySymbol = "Rs";

    [ObservableProperty]
    private bool _pricesIncludeTax = true;

    [ObservableProperty]
    private decimal _cashRoundingStep = 0m;

    [ObservableProperty]
    private bool _allowNegativeStock = true;

    [ObservableProperty]
    private bool _allowSellingBelowMinPrice = false;

    [ObservableProperty]
    private bool _autoPrintReceipt = false;

    [ObservableProperty]
    private int _heldBillsCount;

    // Held bill origination
    [ObservableProperty]
    private int? _originatingHeldBillId;

    // Success Panel
    [ObservableProperty]
    private bool _isSaleSuccess;

    [ObservableProperty]
    private SaleDetailDto? _lastCompletedSale;

    [ObservableProperty]
    private decimal _lastChangeAmount;

    public bool HasCartItems => CartItems.Count > 0;

    public PosSaleViewModel(
        ISaleService saleService,
        ISaleCalculator saleCalculator,
        IProductLookupService productLookupService,
        IProductService productService,
        IPriceLevelService priceLevelService,
        IStockLedgerQuery stockLedgerQuery,
        IShopSettingService shopSettingService,
        IDialogService dialogService,
        INavigationService navigationService,
        IReceiptPrinterService receiptPrinterService,
        IReceiptBuilder? receiptBuilder = null,
        IAuditLogService? auditLogService = null,
        ICurrentUserProvider? currentUserProvider = null,
        IProductSearchService? productSearchService = null)
    {
        _saleService = saleService;
        _saleCalculator = saleCalculator;
        _productLookupService = productLookupService;
        _productService = productService;
        _priceLevelService = priceLevelService;
        _stockLedgerQuery = stockLedgerQuery;
        _shopSettingService = shopSettingService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _receiptPrinterService = receiptPrinterService;
        _receiptBuilder = receiptBuilder;
        _auditLogService = auditLogService;
        _currentUserProvider = currentUserProvider;
        _productSearchService = productSearchService;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAndPriceLevelsAsync();
        RecalculateTotals();
    }

    public async Task LoadSettingsAndPriceLevelsAsync()
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;
            PricesIncludeTax = settings.PricesIncludeTax;
            CashRoundingStep = settings.CashRoundingStep;
            AllowNegativeStock = settings.AllowNegativeStock;
            AllowSellingBelowMinPrice = settings.AllowSellingBelowMinPrice;
            AutoPrintReceipt = settings.AutoPrintReceipt;

            var plResult = await _priceLevelService.GetAllAsync();
            if (plResult.Success && plResult.Data != null)
            {
                PriceLevels.Clear();
                foreach (var pl in plResult.Data) PriceLevels.Add(pl);
                SelectedPriceLevel = PriceLevels.FirstOrDefault(p => p.Name.Equals("Retail", StringComparison.OrdinalIgnoreCase)) ?? PriceLevels.FirstOrDefault();
            }

            var heldResult = await _saleService.ListHeldBillsAsync();
            HeldBillsCount = heldResult.Success && heldResult.Data != null ? heldResult.Data.Count : 0;
        }
        catch { }
    }

    // ---------------- SCANNING & LOOKUP ----------------

    partial void OnScanInputChanged(string value)
    {
        ScanErrorMessage = null;
        _scanDebounceCts?.Cancel();

        if (string.IsNullOrWhiteSpace(value))
        {
            ShowSuggestions = false;
            HasSuggestions = false;
            Suggestions.Clear();
            SelectedSuggestion = null;
            return;
        }

        _scanDebounceCts = new CancellationTokenSource();
        var token = _scanDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, token);
                if (token.IsCancellationRequested) return;

                if (App.Current?.Dispatcher != null)
                {
                    await App.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (token.IsCancellationRequested) return;
                        await UpdateSuggestionsAsync(value, token);
                    });
                }
                else
                {
                    await UpdateSuggestionsAsync(value, token);
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    public async Task UpdateSuggestionsAsync(string query, CancellationToken token = default)
    {
        try
        {
            int? priceTier = SelectedPriceLevel?.Id;
            IReadOnlyList<PosProductSearchResult> results;

            if (_productSearchService != null)
            {
                results = await _productSearchService.SearchProductsAsync(query, priceTier, 20, token);
            }
            else
            {
                // Fallback using ProductService
                var res = await _productService.GetPagedProductsAsync(new ProductSearchQuery
                {
                    SearchTerm = query.Trim(),
                    IsActive = true,
                    PageSize = 20
                }, token);

                results = (res.Data?.Items ?? new List<ProductListItemDto>())
                    .Select(p => new PosProductSuggestion(p).SearchResult!)
                    .ToList();
            }

            if (token.IsCancellationRequested) return;

            Suggestions.Clear();
            foreach (var r in results)
            {
                Suggestions.Add(new PosProductSuggestion(r));
            }

            HasSuggestions = Suggestions.Count > 0;
            ShowSuggestions = true;
            SelectedSuggestion = Suggestions.FirstOrDefault();
        }
        catch (OperationCanceledException) { }
        catch
        {
            ShowSuggestions = false;
        }
    }

    [RelayCommand]
    public async Task ProcessScanInputAsync()
    {
        if (string.IsNullOrWhiteSpace(ScanInput)) return;

        // 1. If suggestions open and an item is selected or exactly 1 suggestion, add that suggestion
        if (ShowSuggestions && SelectedSuggestion != null)
        {
            decimal qty = SelectedSuggestion.ParsedQuantity > 0 ? SelectedSuggestion.ParsedQuantity : 1m;
            await AddProductToCartAsync(SelectedSuggestion.ProductId, qty);
            ClearScanBox();
            return;
        }

        if (ShowSuggestions && Suggestions.Count == 1)
        {
            decimal qty = Suggestions[0].ParsedQuantity > 0 ? Suggestions[0].ParsedQuantity : 1m;
            await AddProductToCartAsync(Suggestions[0].ProductId, qty);
            ClearScanBox();
            return;
        }

        // 2. Barcode or SKU direct lookup
        var lookupResult = await _productLookupService.LookupProductAsync(ScanInput.Trim(), SelectedPriceLevel?.Id);
        if (lookupResult.Found && lookupResult.Product != null)
        {
            await AddProductToCartAsync(lookupResult.Product.Id, lookupResult.ParsedQuantity);
            ClearScanBox();
        }
        else
        {
            ScanErrorMessage = lookupResult.ErrorMessage ?? $"Unknown barcode or product '{ScanInput.Trim()}'.";
            try
            {
                System.Media.SystemSounds.Beep.Play();
            }
            catch { }
        }
    }

    private void ClearScanBox()
    {
        _scanDebounceCts?.Cancel();
        ScanInput = string.Empty;
        ScanErrorMessage = null;
        ShowSuggestions = false;
        HasSuggestions = false;
        Suggestions.Clear();
        SelectedSuggestion = null;
    }

    [RelayCommand]
    public async Task SelectSuggestionAsync(PosProductSuggestion suggestion)
    {
        if (suggestion == null) return;
        decimal qty = suggestion.ParsedQuantity > 0 ? suggestion.ParsedQuantity : 1m;
        await AddProductToCartAsync(suggestion.ProductId, qty);
        ClearScanBox();
    }

    public async Task AddProductToCartAsync(int productId, decimal quantity)
    {
        var existingLine = CartItems.FirstOrDefault(c => c.ProductId == productId);
        if (existingLine != null)
        {
            existingLine.Quantity += quantity;
            RecalculateTotals();
            SelectedCartItem = existingLine;
            return;
        }

        var productResult = await _productService.GetProductByIdAsync(productId);
        if (!productResult.Success || productResult.Data == null)
        {
            ScanErrorMessage = "Could not load product details.";
            return;
        }

        var p = productResult.Data;
        decimal currentStock = p.TrackStock ? await _stockLedgerQuery.GetCurrentStockAsync(p.Id) : 0m;
        var stockStatus = ComputeStockStatus(currentStock, p.ReorderLevel, p.TrackStock);

        decimal unitPrice = p.SalePrice;
        if (SelectedPriceLevel != null)
        {
            var tier = p.Prices?.FirstOrDefault(pr => pr.PriceLevelId == SelectedPriceLevel.Id);
            if (tier != null && tier.Price > 0)
            {
                unitPrice = tier.Price;
            }
        }

        var newLine = new PosCartLineItemViewModel
        {
            LineNo = CartItems.Count + 1,
            ProductId = p.Id,
            ProductName = p.Name,
            Sku = p.Sku,
            Barcode = p.Barcode,
            Quantity = quantity,
            UnitPrice = unitPrice,
            MinSalePrice = p.MinSalePrice,
            CostPriceSnapshot = p.CostPrice,
            AllowDecimalQuantity = p.AllowDecimalQuantity,
            TaxPercent = p.TaxPercent,
            CurrentStock = currentStock,
            TrackStock = p.TrackStock,
            StockStatus = stockStatus
        };

        newLine.OnItemChanged += RecalculateTotals;
        CartItems.Add(newLine);
        RenumberLines();
        RecalculateTotals();
        SelectedCartItem = newLine;
        OnPropertyChanged(nameof(HasCartItems));
    }

    private static StockStatus ComputeStockStatus(decimal stock, decimal reorderLevel, bool trackStock)
    {
        if (!trackStock) return StockStatus.NotTracked;
        if (stock < 0m) return StockStatus.Negative;
        if (stock == 0m) return StockStatus.OutOfStock;
        if (stock <= reorderLevel) return StockStatus.Low;
        return StockStatus.OK;
    }

    private void RenumberLines()
    {
        for (int i = 0; i < CartItems.Count; i++)
        {
            CartItems[i].LineNo = i + 1;
        }
    }

    // ---------------- CART EDITING ----------------

    [RelayCommand]
    public void RemoveCartLine(PosCartLineItemViewModel? line)
    {
        var target = line ?? SelectedCartItem;
        if (target != null)
        {
            target.OnItemChanged -= RecalculateTotals;
            CartItems.Remove(target);
            RenumberLines();
            RecalculateTotals();
            SelectedCartItem = CartItems.LastOrDefault();
            OnPropertyChanged(nameof(HasCartItems));
        }
    }

    [RelayCommand]
    public void ClearCart()
    {
        if (CartItems.Count > 0)
        {
            var action = _dialogService.ShowCartExitPrompt();
            if (action == CartExitAction.Hold)
            {
                _ = HoldBillAsync();
                return;
            }
            if (action == CartExitAction.Keep)
            {
                return;
            }
        }

        ResetCartState();
    }

    private void ResetCartState()
    {
        foreach (var item in CartItems)
        {
            item.OnItemChanged -= RecalculateTotals;
        }
        CartItems.Clear();
        SelectedCartItem = null;
        OriginatingHeldBillId = null;
        BillDiscountPercent = 0m;
        BillDiscountAmount = 0m;
        Notes = null;
        IsSaleSuccess = false;
        LastCompletedSale = null;
        LastChangeAmount = 0m;
        ClearScanBox();

        // Reset to Retail price level
        SelectedPriceLevel = PriceLevels.FirstOrDefault(p => p.Name.Equals("Retail", StringComparison.OrdinalIgnoreCase)) ?? PriceLevels.FirstOrDefault();

        RecalculateTotals();
        OnPropertyChanged(nameof(HasCartItems));
    }

    public void RecalculateTotals()
    {
        var lineInputs = CartItems.Select(c => new SaleLineInput
        {
            Quantity = c.Quantity,
            UnitPrice = c.UnitPrice,
            DiscountPercent = c.DiscountPercent,
            DiscountAmount = c.DiscountAmount,
            TaxPercent = c.TaxPercent
        }).ToList();

        var totals = _saleCalculator.CalculateSale(
            lineInputs,
            billDiscountPercent: BillDiscountPercent,
            billDiscountAmount: BillDiscountAmount,
            pricesIncludeTax: PricesIncludeTax,
            cashRoundingStep: CashRoundingStep,
            paidAmount: 0m);

        SubTotal = totals.SubTotal;
        LineDiscountTotal = totals.LineDiscountTotal;
        BillDiscountAmount = totals.BillDiscountAmount;
        TaxAmount = totals.TaxAmount;
        RoundOff = totals.RoundOff;
        GrandTotal = totals.GrandTotal;

        ItemsCount = CartItems.Count;
        TotalUnits = CartItems.Sum(c => c.Quantity);

        for (int i = 0; i < CartItems.Count && i < totals.Lines.Count; i++)
        {
            CartItems[i].LineTotal = totals.Lines[i].LineTotal;
            CartItems[i].TaxAmount = totals.Lines[i].TaxAmount;
        }

        OnPropertyChanged(nameof(HasCartItems));
    }

    // ---------------- ACTIONS (SHORTCUTS) ----------------

    [RelayCommand]
    public async Task OpenProductSearchAsync()
    {
        var searchVm = new ViewModels.Purchases.ProductSearchDialogViewModel(_productService);
        var selected = _dialogService.ShowMultiProductSearchDialog(searchVm);
        if (selected != null && selected.Count > 0)
        {
            foreach (var prod in selected)
            {
                await AddProductToCartAsync(prod.Id, 1m);
            }
        }
    }

    [RelayCommand]
    public async Task HoldBillAsync()
    {
        if (CartItems.Count == 0)
        {
            _dialogService.ShowMessage("Cannot park an empty cart.", "Hold Bill");
            return;
        }

        var dto = new SaveSaleDto
        {
            PriceLevelId = SelectedPriceLevel?.Id,
            Notes = Notes?.Trim(),
            BillDiscountPercent = BillDiscountPercent,
            BillDiscountAmount = BillDiscountAmount,
            Items = CartItems.Select(c => new SaleLineItemDto
            {
                ProductId = c.ProductId,
                ProductName = c.ProductName,
                Sku = c.Sku,
                Barcode = c.Barcode,
                Quantity = c.Quantity,
                UnitPrice = c.UnitPrice,
                DiscountPercent = c.DiscountPercent,
                DiscountAmount = c.DiscountAmount,
                TaxPercent = c.TaxPercent,
                Note = c.Note
            }).ToList()
        };

        var result = await _saleService.SaveHeldBillAsync(dto);
        if (result.Success && result.Data != null)
        {
            _dialogService.ShowMessage($"Cart saved to held bills as '{result.Data.HoldNumber}'.", "Bill Held");
            ResetCartState();
            await LoadSettingsAndPriceLevelsAsync();
        }
        else
        {
            _dialogService.ShowMessage(result.Error ?? "Failed to hold bill.", "Error");
        }
    }

    [RelayCommand]
    public async Task OpenHeldBillsAsync()
    {
        var heldVm = new HeldBillsDialogViewModel(_saleService, _dialogService);
        await heldVm.InitializeAsync();
        bool? resumed = _dialogService.ShowHeldBillsDialog(heldVm);

        if (resumed == true && heldVm.ResumedSale != null)
        {
            await ResumeHeldBillAsync(heldVm.ResumedSale);
        }
    }

    public async Task ResumeHeldBillAsync(SaleDetailDto heldSale)
    {
        ResetCartState();
        OriginatingHeldBillId = heldSale.Id;
        Notes = heldSale.Notes;
        BillDiscountAmount = heldSale.BillDiscountAmount;

        if (heldSale.PriceLevelId.HasValue)
        {
            SelectedPriceLevel = PriceLevels.FirstOrDefault(pl => pl.Id == heldSale.PriceLevelId.Value) ?? SelectedPriceLevel;
        }

        foreach (var item in heldSale.Items)
        {
            await AddProductToCartAsync(item.ProductId, item.Quantity);
        }

        await LoadSettingsAndPriceLevelsAsync();
    }

    [RelayCommand]
    public void OpenBillDiscount()
    {
        var vm = new BillDiscountDialogViewModel(BillDiscountPercent, BillDiscountAmount);
        bool? applied = _dialogService.ShowBillDiscountDialog(vm);
        if (applied == true && vm.Confirmed)
        {
            BillDiscountPercent = vm.DiscountPercent;
            BillDiscountAmount = vm.DiscountAmount;
            RecalculateTotals();
        }
    }

    [RelayCommand]
    public void OpenLineDiscount()
    {
        if (SelectedCartItem == null)
        {
            _dialogService.ShowMessage("Please select a line in the cart first to apply a line discount.", "Line Discount");
            return;
        }

        var vm = new LineDiscountDialogViewModel(
            SelectedCartItem.ProductName,
            SelectedCartItem.UnitPrice,
            SelectedCartItem.Quantity,
            SelectedCartItem.DiscountPercent,
            SelectedCartItem.DiscountAmount);

        bool? applied = _dialogService.ShowLineDiscountDialog(vm);
        if (applied == true && vm.Confirmed)
        {
            SelectedCartItem.DiscountPercent = vm.DiscountPercent;
            SelectedCartItem.DiscountAmount = vm.DiscountAmount;
            RecalculateTotals();
        }
    }

    [RelayCommand]
    public async Task OpenPaymentAsync()
    {
        if (CartItems.Count == 0)
        {
            _dialogService.ShowMessage("The cart is empty. Scan products before proceeding to payment.", "Payment");
            return;
        }

        // Check for minimum selling price violations
        if (!AllowSellingBelowMinPrice)
        {
            foreach (var item in CartItems)
            {
                if (item.MinSalePrice.HasValue && item.UnitPrice < item.MinSalePrice.Value)
                {
                    _dialogService.ShowMessage(
                        $"Selling price {CurrencySymbol}{item.UnitPrice:F2} for '{item.ProductName}' is below the minimum allowed price of {CurrencySymbol}{item.MinSalePrice.Value:F2}.",
                        "Price Below Minimum");
                    return;
                }
            }
        }

        // Check for negative stock violation
        if (!AllowNegativeStock)
        {
            foreach (var item in CartItems.Where(c => c.TrackStock))
            {
                if (item.Quantity > item.CurrentStock)
                {
                    _dialogService.ShowMessage(
                        $"Cannot sell {item.Quantity} of '{item.ProductName}'. Only {item.CurrentStock} in stock and negative inventory is disallowed.",
                        "Insufficient Stock");
                    return;
                }
            }
        }

        RecalculateTotals();

        var paymentVm = new SalePaymentDialogViewModel(
            GrandTotal,
            CurrencySymbol,
            _saleCalculator);

        bool? confirmed = _dialogService.ShowSalePaymentDialog(paymentVm);
        if (confirmed == true && paymentVm.Confirmed)
        {
            await CompleteSaleTransactionAsync(paymentVm);
        }
    }

    private async Task CompleteSaleTransactionAsync(SalePaymentDialogViewModel paymentVm)
    {
        var dto = new CompleteSaleDto
        {
            HeldBillId = OriginatingHeldBillId,
            PriceLevelId = SelectedPriceLevel?.Id,
            Notes = Notes?.Trim(),
            BillDiscountPercent = BillDiscountPercent,
            BillDiscountAmount = BillDiscountAmount,
            Items = CartItems.Select(c => new SaleLineItemDto
            {
                ProductId = c.ProductId,
                ProductName = c.ProductName,
                Sku = c.Sku,
                Barcode = c.Barcode,
                Quantity = c.Quantity,
                UnitPrice = c.UnitPrice,
                DiscountPercent = c.DiscountPercent,
                DiscountAmount = c.DiscountAmount,
                TaxPercent = c.TaxPercent,
                Note = c.Note
            }).ToList(),
            Payments = paymentVm.ResultPayments
        };

        var result = await _saleService.CompleteSaleAsync(dto);
        if (result.Success && result.Data != null)
        {
            LastCompletedSale = result.Data;
            LastChangeAmount = paymentVm.ChangeAmount;
            IsSaleSuccess = true;

            if (AutoPrintReceipt)
            {
                _ = PrintLastReceiptAsync();
            }

            await LoadSettingsAndPriceLevelsAsync();
        }
        else
        {
            _dialogService.ShowMessage(result.Error ?? "Failed to complete transaction.", "Sale Failed");
        }
    }

    [RelayCommand]
    public async Task PrintLastReceiptAsync()
    {
        if (LastCompletedSale == null) return;

        await Task.Run(async () =>
        {
            try
            {
                var settings = await _shopSettingService.GetSettingsAsync();
                var printResult = await _receiptPrinterService.PrintSaleReceiptAsync(LastCompletedSale, settings, isDuplicate: false);

                if (!printResult.Success)
                {
                    var err = string.IsNullOrWhiteSpace(printResult.ErrorMessage) ? "Unknown error" : printResult.ErrorMessage.TrimEnd('.');
                    _dialogService.ShowMessage(
                        $"Receipt could not be printed: {err}. You can reprint anytime from the Sales list.",
                        "Printer Notice");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(
                    $"Receipt print error: {ex.Message}. You can reprint anytime from the Sales list.",
                    "Printer Notice");
            }
        });
    }

    [RelayCommand]
    public async Task ReprintLastReceiptAsync()
    {
        if (LastCompletedSale == null) return;

        await Task.Run(async () =>
        {
            try
            {
                var settings = await _shopSettingService.GetSettingsAsync();

                if (_auditLogService != null && _currentUserProvider != null)
                {
                    await _auditLogService.LogAsync(
                        _currentUserProvider.UserId,
                        "ReprintReceipt",
                        "Sale",
                        LastCompletedSale.Id,
                        $"Reprinted receipt for {LastCompletedSale.SaleNumber} from POS success panel (marked DUPLICATE)");
                }

                var printResult = await _receiptPrinterService.PrintSaleReceiptAsync(LastCompletedSale, settings, isDuplicate: true);

                if (printResult.Success)
                {
                    _dialogService.ShowMessage(printResult.NoticeMessage ?? "Duplicate receipt printed.", "Receipt Reprint");
                }
                else
                {
                    _dialogService.ShowMessage($"Receipt could not be printed: {printResult.ErrorMessage}", "Printer Notice");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Reprint error: {ex.Message}", "Printer Notice");
            }
        });
    }

    [RelayCommand]
    public void ViewLastSale()
    {
        if (LastCompletedSale == null) return;
        var vm = new SaleDetailDialogViewModel(
            LastCompletedSale.Id,
            _saleService,
            null!,
            _shopSettingService,
            _receiptPrinterService,
            _receiptBuilder,
            _auditLogService,
            _currentUserProvider,
            _dialogService);
        _dialogService.ShowSaleDetailDialog(vm);
    }

    [RelayCommand]
    public void StartNewSaleAfterSuccess()
    {
        ResetCartState();
    }

    [RelayCommand]
    public void OpenReturnsShortcut()
    {
        _navigationService.NavigateTo("SaleReturns");
    }

    [RelayCommand]
    public void OpenHelpOverlay()
    {
        var vm = new PosHelpDialogViewModel();
        _dialogService.ShowPosHelpDialog(vm);
    }

    [RelayCommand]
    public async Task ProcessScanAsync() => await ProcessScanInputAsync();

    [RelayCommand]
    public void DismissSuccessPanel() => StartNewSaleAfterSuccess();

    [RelayCommand]
    public void ShowHelp() => OpenHelpOverlay();

    [RelayCommand]
    public async Task ShowHeldBillsAsync() => await OpenHeldBillsAsync();

    [RelayCommand]
    public void DiscardCart() => ClearCart();

    [RelayCommand]
    public async Task OpenCashDrawerAsync() => await _receiptPrinterService.OpenCashDrawerAsync();

    [RelayCommand]
    public async Task CheckoutAsync() => await OpenPaymentAsync();

    [RelayCommand]
    public void RemoveCartItem(PosCartLineItemViewModel? line) => RemoveCartLine(line);
}
