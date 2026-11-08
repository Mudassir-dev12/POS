using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Purchases;

public partial class PurchaseEntryViewModel : ObservableObject
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;
    private readonly IPurchaseCalculator _calculator;
    private readonly IDialogService _dialogService;
        
    private int? _purchaseId;
    private bool _isInitialized;
    private bool _isDirty;

    [ObservableProperty]
    private string _windowTitle = "New Purchase Invoice";

    [ObservableProperty]
    private string _purchaseNumber = "NEW DRAFT";

    [ObservableProperty]
    private PurchaseStatus _status = PurchaseStatus.Draft;

    [ObservableProperty]
    private PurchasePaymentStatus _paymentStatus = PurchasePaymentStatus.Unpaid;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private DateTime _purchaseDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _dueDate;

    [ObservableProperty]
    private string? _supplierInvoiceNo;

    [ObservableProperty]
    private string? _notes;

    // Line items & scanner input
    [ObservableProperty]
    private string _barcodeInput = string.Empty;

    [ObservableProperty]
    private PurchaseEntryLineItemViewModel? _selectedLine;

    [ObservableProperty]
    private decimal? _selectedProductStock;

    [ObservableProperty]
    private decimal? _selectedProductLastCost;

    // Totals
    [ObservableProperty]
    private decimal _subTotal;

    [ObservableProperty]
    private decimal _invoiceDiscount;

    [ObservableProperty]
    private decimal _taxAmount;

    [ObservableProperty]
    private decimal _freightAmount;

    [ObservableProperty]
    private decimal _grandTotal;

    [ObservableProperty]
    private decimal _paidAmount;

    [ObservableProperty]
    private decimal _balance;

    // Immediate payment
    [ObservableProperty]
    private bool _payNowEnabled;

    [ObservableProperty]
    private decimal? _payNowAmount;

    [ObservableProperty]
    private PaymentMethod _payNowMethod = PaymentMethod.Cash;

    public PaymentMethod[] AvailablePaymentMethods { get; } = Enum.GetValues<PaymentMethod>();

    // Audit & status info
    [ObservableProperty]
    private DateTime? _postedAt;

    [ObservableProperty]
    private string? _postedByUserName;

    [ObservableProperty]
    private DateTime? _cancelledAt;

    [ObservableProperty]
    private string? _cancelledByUserName;

    [ObservableProperty]
    private string? _cancelReason;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<PurchaseEntryLineItemViewModel> LineItems { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();

    public bool HasUnsavedChanges => _isDirty && !IsReadOnly;

    public event Action<bool?>? RequestClose;

    public PurchaseEntryViewModel(
        IPurchaseService purchaseService,
        ISupplierService supplierService,
        IProductService productService,
        IPurchaseCalculator calculator,
        IDialogService dialogService)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _productService = productService;
        _calculator = calculator;
        _dialogService = dialogService;
    }

    public async Task InitializeAsync(int? purchaseId = null)
    {
        _purchaseId = purchaseId;
        IsLoading = true;
        _isInitialized = false;

        try
        {
            // 1. Load active suppliers
            var supRes = await _supplierService.GetFlatListAsync(false);
            if (supRes.Success && supRes.Data != null)
            {
                Suppliers.Clear();
                foreach (var s in supRes.Data)
                {
                    Suppliers.Add(s);
                }
            }

            // 2. Load existing purchase or initialize empty draft
            if (purchaseId.HasValue)
            {
                var pRes = await _purchaseService.GetPurchaseByIdAsync(purchaseId.Value);
                if (pRes.Success && pRes.Data != null)
                {
                    LoadFromDetail(pRes.Data);
                }
                else
                {
                    ErrorMessage = pRes.ErrorMessage;
                }
            }
            else
            {
                PurchaseNumber = "NEW DRAFT";
                Status = PurchaseStatus.Draft;
                PaymentStatus = PurchasePaymentStatus.Unpaid;
                PurchaseDate = DateTime.Today;
                DueDate = null;
                IsReadOnly = false;
                WindowTitle = "New Purchase Invoice (Draft)";
            }

            RecalculateTotals();
            _isDirty = false;
            _isInitialized = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadFromDetail(PurchaseDetailDto p)
    {
        PurchaseNumber = p.PurchaseNumber;
        Status = p.Status;
        PaymentStatus = p.PaymentStatus;
        PurchaseDate = p.PurchaseDate;
        DueDate = p.DueDate;
        SupplierInvoiceNo = p.SupplierInvoiceNo;
        Notes = p.Notes;
        InvoiceDiscount = p.DiscountAmount;
        FreightAmount = p.FreightAmount;
        PaidAmount = p.PaidAmount;
        PostedAt = p.PostedAt;
        PostedByUserName = p.PostedByName;
        CancelledAt = p.CancelledAt;
        CancelledByUserName = p.CancelledByName;
        CancelReason = p.CancelReason;

        IsReadOnly = p.Status == PurchaseStatus.Posted || p.Status == PurchaseStatus.Cancelled;
        WindowTitle = IsReadOnly
            ? $"Purchase Invoice: {p.PurchaseNumber} [{p.Status}]"
            : $"Edit Purchase Invoice: {p.PurchaseNumber}";

        SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == p.SupplierId);

        LineItems.Clear();
        foreach (var item in p.Items)
        {
            var lineVm = new PurchaseEntryLineItemViewModel(_calculator, () =>
            {
                if (_isInitialized) MarkDirty();
                RecalculateTotals();
            });

            lineVm.SetInitialValues(
                item.ProductId,
                item.ProductName,
                item.Sku,
                null,
                null,
                null,
                item.Quantity,
                item.UnitCost,
                item.DiscountPercent,
                item.TaxPercent);

            lineVm.Id = item.Id;
            LineItems.Add(lineVm);
        }

        Warnings.Clear();
        foreach (var w in p.Warnings)
        {
            Warnings.Add(w);
        }
    }

    public void RecalculateTotals()
    {
        var lineResults = LineItems.Select(l => l.LastCalculation ?? _calculator.CalculateLine(l.Quantity, l.UnitCost, l.DiscountPercent, l.TaxPercent));
        var totals = _calculator.CalculateTotals(lineResults, InvoiceDiscount, FreightAmount);

        SubTotal = totals.SubTotal;
        TaxAmount = totals.TaxAmount;
        GrandTotal = totals.GrandTotal;
        Balance = totals.GrandTotal - PaidAmount;

        if (Status == PurchaseStatus.Draft && PayNowEnabled && (!PayNowAmount.HasValue || PayNowAmount.Value == 0m))
        {
            PayNowAmount = GrandTotal;
        }
    }

    private void MarkDirty()
    {
        if (!IsReadOnly)
        {
            _isDirty = true;
        }
    }

    partial void OnSelectedSupplierChanged(SupplierDto? value) { if (_isInitialized) MarkDirty(); }
    partial void OnPurchaseDateChanged(DateTime value) { if (_isInitialized) MarkDirty(); }
    partial void OnDueDateChanged(DateTime? value) { if (_isInitialized) MarkDirty(); }
    partial void OnSupplierInvoiceNoChanged(string? value) { if (_isInitialized) MarkDirty(); }
    partial void OnNotesChanged(string? value) { if (_isInitialized) MarkDirty(); }
    partial void OnInvoiceDiscountChanged(decimal value) { if (_isInitialized) { MarkDirty(); RecalculateTotals(); } }
    partial void OnFreightAmountChanged(decimal value) { if (_isInitialized) { MarkDirty(); RecalculateTotals(); } }

    partial void OnPayNowEnabledChanged(bool value)
    {
        if (value && (!PayNowAmount.HasValue || PayNowAmount.Value == 0m))
        {
            PayNowAmount = GrandTotal;
        }
    }

    partial void OnSelectedLineChanged(PurchaseEntryLineItemViewModel? value)
    {
        if (value == null)
        {
            SelectedProductStock = null;
            SelectedProductLastCost = null;
            return;
        }

        _ = LoadSelectedProductStatsAsync(value.ProductId);
    }

    private async Task LoadSelectedProductStatsAsync(int productId)
    {
        var stockRes = await _purchaseService.GetProductCurrentStockAsync(productId);
        SelectedProductStock = stockRes.Success ? stockRes.Data : null;

        var costRes = await _purchaseService.GetProductLastCostAsync(productId);
        SelectedProductLastCost = costRes.Success ? costRes.Data : null;
    }

    [RelayCommand]
    public async Task AddScannedProductAsync()
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(BarcodeInput)) return;

        string code = BarcodeInput.Trim();
        BarcodeInput = string.Empty;

        // Find product by exact barcode, SKU, or name
        var searchRes = await _productService.GetPagedProductsAsync(new ProductSearchQuery
        {
            SearchTerm = code,
            IsActive = true,
            PageNumber = 1,
            PageSize = 10
        });

        if (!searchRes.Success || searchRes.Data == null || searchRes.Data.Items.Count == 0)
        {
            ErrorMessage = $"Product not found for scan: '{code}'. Press F2 to search catalog.";
            return;
        }

        var match = searchRes.Data.Items.FirstOrDefault(p =>
            string.Equals(p.Barcode, code, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Sku, code, StringComparison.OrdinalIgnoreCase))
            ?? searchRes.Data.Items.First();

        AddOrIncrementProduct(match);
    }

    public void AddOrIncrementProduct(ProductListItemDto product)
    {
        ErrorMessage = null;
        var existingLine = LineItems.FirstOrDefault(l => l.ProductId == product.Id);
        if (existingLine != null)
        {
            existingLine.Quantity += 1m;
            SelectedLine = existingLine;
        }
        else
        {
            var lineVm = new PurchaseEntryLineItemViewModel(_calculator, () =>
            {
                if (_isInitialized) MarkDirty();
                RecalculateTotals();
            });

            decimal unitCost = product.CostPrice;
            decimal taxPercent = product.TaxPercent;

            lineVm.SetInitialValues(
                product.Id,
                product.Name,
                product.Sku,
                product.Barcode,
                product.CategoryName,
                product.UnitName,
                1m,
                unitCost,
                0m,
                taxPercent);

            LineItems.Add(lineVm);
            SelectedLine = lineVm;
        }

        MarkDirty();
        RecalculateTotals();
    }

    [RelayCommand]
    public void OpenProductSearch()
    {
        if (IsReadOnly) return;

        var searchVm = new ProductSearchDialogViewModel(_productService);
        var selectedProduct = _dialogService.ShowProductSearchDialog(searchVm);
        if (selectedProduct != null)
        {
            AddOrIncrementProduct(selectedProduct);
        }
    }

    [RelayCommand]
    public void DeleteSelectedLine()
    {
        if (IsReadOnly || SelectedLine == null) return;

        LineItems.Remove(SelectedLine);
        SelectedLine = LineItems.LastOrDefault();
        MarkDirty();
        RecalculateTotals();
    }

    [RelayCommand]
    public async Task<bool> SaveDraftAsync()
    {
        if (IsReadOnly) return false;

        ErrorMessage = null;

        if (SelectedSupplier == null)
        {
            ErrorMessage = "Please select a vendor / supplier before saving.";
            return false;
        }

        if (LineItems.Count == 0)
        {
            ErrorMessage = "Please add at least one line item.";
            return false;
        }

        // Check if anything changed on an existing draft
        if (_purchaseId.HasValue && !_isDirty)
        {
            // Unchanged: save is clean no-op
            return true;
        }

        int? userId = 1;
        IsLoading = true;
        try
        {
            if (_purchaseId.HasValue)
            {
                var updateDto = new UpdatePurchaseDto
                {
                    Id = _purchaseId.Value,
                    SupplierId = SelectedSupplier.Id,
                    SupplierInvoiceNo = SupplierInvoiceNo,
                    PurchaseDate = PurchaseDate,
                    DueDate = DueDate,
                    Notes = Notes,
                    DiscountAmount = InvoiceDiscount,
                    FreightAmount = FreightAmount,
                    Items = LineItems.Select(l => new CreatePurchaseItemDto
                    {
                        ProductId = l.ProductId,
                        Quantity = l.Quantity,
                        UnitCost = l.UnitCost,
                        DiscountPercent = l.DiscountPercent,
                        TaxPercent = l.TaxPercent
                    }).ToList()
                };

                var updateRes = await _purchaseService.UpdatePurchaseDraftAsync(updateDto, userId);
                if (!updateRes.Success)
                {
                    ErrorMessage = updateRes.ErrorMessage;
                    return false;
                }

                LoadFromDetail(updateRes.Data!);
                _isDirty = false;
                return true;
            }
            else
            {
                var createDto = new CreatePurchaseDto
                {
                    SupplierId = SelectedSupplier.Id,
                    SupplierInvoiceNo = SupplierInvoiceNo,
                    PurchaseDate = PurchaseDate,
                    DueDate = DueDate,
                    Notes = Notes,
                    DiscountAmount = InvoiceDiscount,
                    FreightAmount = FreightAmount,
                    Items = LineItems.Select(l => new CreatePurchaseItemDto
                    {
                        ProductId = l.ProductId,
                        Quantity = l.Quantity,
                        UnitCost = l.UnitCost,
                        DiscountPercent = l.DiscountPercent,
                        TaxPercent = l.TaxPercent
                    }).ToList()
                };

                var createRes = await _purchaseService.CreatePurchaseDraftAsync(createDto, userId);
                if (!createRes.Success)
                {
                    ErrorMessage = createRes.ErrorMessage;
                    return false;
                }

                _purchaseId = createRes.Data!.Id;
                LoadFromDetail(createRes.Data);
                _isDirty = false;
                return true;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PostPurchaseAsync()
    {
        if (IsReadOnly) return;

        // Save current changes first
        bool saved = await SaveDraftAsync();
        if (!saved || !_purchaseId.HasValue) return;

        string promptMsg = $"Post purchase invoice '{PurchaseNumber}' for grand total {ShopPOS.Core.Services.MoneyFormatter.Instance.Format(GrandTotal)}?\n\nThis action will immediately update inventory stock levels and recalculate weighted average costs in the stock ledger.";
        var confirm = _dialogService.ShowConfirmActionDialog("Confirm Post Purchase", promptMsg, isReferenced: false);
        if (confirm != ConfirmActionResult.Delete) // Delete represents the primary confirmed affirmative action
        {
            return;
        }

        IsLoading = true;
        try
        {
            int? userId = 1;
            decimal? payAmount = PayNowEnabled ? PayNowAmount : null;
            PaymentMethod? payMethod = PayNowEnabled ? PayNowMethod : null;

            var postRes = await _purchaseService.PostPurchaseAsync(_purchaseId.Value, payAmount, payMethod, userId);
            if (!postRes.Success)
            {
                ErrorMessage = postRes.ErrorMessage;
                return;
            }

            LoadFromDetail(postRes.Data!);
            _isDirty = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Close()
    {
        if (HasUnsavedChanges)
        {
            var confirm = _dialogService.ShowConfirmActionDialog(
                "Unsaved Changes",
                "You have unsaved changes on this purchase invoice. Do you want to discard your changes and close?",
                isReferenced: false);

            if (confirm != ConfirmActionResult.Delete)
            {
                return;
            }
        }

        RequestClose?.Invoke(true);
    }
}
