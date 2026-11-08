using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.PurchaseReturns;

public partial class PurchaseReturnLineItemViewModel : ObservableObject
{
    private readonly Action? _onChanged;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal PurchasedQuantity { get; set; }
    public decimal AlreadyReturnedQuantity { get; set; }
    public decimal ReturnableQuantity { get; set; }

    [ObservableProperty]
    private decimal _returnQuantity;

    [ObservableProperty]
    private decimal _lineTotal;

    public PurchaseReturnLineItemViewModel(Action? onChanged = null)
    {
        _onChanged = onChanged;
    }

    partial void OnReturnQuantityChanged(decimal value)
    {
        LineTotal = Math.Round(value * UnitCost, 2, MidpointRounding.AwayFromZero);
        _onChanged?.Invoke();
    }
}

public partial class PurchaseReturnDialogViewModel : ObservableObject
{
    private readonly IPurchaseReturnService _returnService;
    private readonly ISupplierService _supplierService;
    private readonly IPurchaseService _purchaseService;
    
    [ObservableProperty]
    private string _title = "Create Purchase Return (Debit Note)";

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private PurchaseListDto? _selectedInvoice;

    [ObservableProperty]
    private DateTime _returnDate = DateTime.Today;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<PurchaseListDto> Invoices { get; } = new();
    public ObservableCollection<PurchaseReturnLineItemViewModel> LineItems { get; } = new();

    public event Action<bool?>? RequestClose;

    public PurchaseReturnDialogViewModel(
        IPurchaseReturnService returnService,
        ISupplierService supplierService,
        IPurchaseService purchaseService)
    {
        _returnService = returnService;
        _supplierService = supplierService;
        _purchaseService = purchaseService;
    }

    public async Task InitializeAsync(int? preselectedSupplierId = null, int? preselectedPurchaseId = null)
    {
        IsLoading = true;
        try
        {
            var supRes = await _supplierService.GetFlatListAsync(false);
            if (supRes.Success && supRes.Data != null)
            {
                Suppliers.Clear();
                foreach (var s in supRes.Data)
                {
                    Suppliers.Add(s);
                }
            }

            if (preselectedSupplierId.HasValue)
            {
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == preselectedSupplierId.Value);
            }
            else
            {
                SelectedSupplier = Suppliers.FirstOrDefault();
            }

            if (preselectedPurchaseId.HasValue && SelectedSupplier != null)
            {
                SelectedInvoice = Invoices.FirstOrDefault(i => i.Id == preselectedPurchaseId.Value);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSupplierChanged(SupplierDto? value)
    {
        _ = LoadSupplierInvoicesAsync(value?.Id);
    }

    private async Task LoadSupplierInvoicesAsync(int? supplierId)
    {
        Invoices.Clear();
        SelectedInvoice = null;
        LineItems.Clear();
        TotalAmount = 0m;

        if (!supplierId.HasValue) return;

        var res = await _purchaseService.GetPurchasesAsync(new PurchaseSearchQuery
        {
            SupplierId = supplierId.Value,
            Status = PurchaseStatus.Posted,
            PageNumber = 1,
            PageSize = 100
        });

        if (res.Success && res.Data != null)
        {
            foreach (var p in res.Data.Items)
            {
                Invoices.Add(p);
            }
        }
    }

    partial void OnSelectedInvoiceChanged(PurchaseListDto? value)
    {
        _ = LoadInvoiceLinesAsync(value?.Id);
    }

    private async Task LoadInvoiceLinesAsync(int? purchaseId)
    {
        LineItems.Clear();
        TotalAmount = 0m;

        if (!purchaseId.HasValue) return;

        var res = await _purchaseService.GetPurchaseByIdAsync(purchaseId.Value);
        if (res.Success && res.Data != null)
        {
            foreach (var item in res.Data.Items)
            {
                decimal alreadyReturned = 0m; // If tracked per line in future, or remaining
                decimal returnable = item.Quantity; // Full quantity if first return

                var lineVm = new PurchaseReturnLineItemViewModel(RecalculateTotals)
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    UnitCost = item.UnitCost,
                    PurchasedQuantity = item.Quantity,
                    AlreadyReturnedQuantity = alreadyReturned,
                    ReturnableQuantity = returnable,
                    ReturnQuantity = 0m
                };

                LineItems.Add(lineVm);
            }
        }
    }

    public void RecalculateTotals()
    {
        TotalAmount = LineItems.Sum(l => l.LineTotal);
    }

    [RelayCommand]
    public async Task PostReturnAsync()
    {
        ErrorMessage = null;

        if (SelectedSupplier == null)
        {
            ErrorMessage = "Please select a vendor / supplier.";
            return;
        }

        var activeLines = LineItems.Where(l => l.ReturnQuantity > 0).ToList();
        if (activeLines.Count == 0)
        {
            ErrorMessage = "Please enter return quantity > 0 for at least one item.";
            return;
        }

        foreach (var l in activeLines)
        {
            if (l.ReturnQuantity > l.ReturnableQuantity)
            {
                ErrorMessage = $"Return quantity for '{l.ProductName}' ({l.ReturnQuantity}) exceeds returnable amount ({l.ReturnableQuantity}).";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(Notes))
        {
            ErrorMessage = "A reason or note is required for the return.";
            return;
        }

        IsLoading = true;
        try
        {
            var dto = new CreatePurchaseReturnDto
            {
                SupplierId = SelectedSupplier.Id,
                PurchaseId = SelectedInvoice?.Id,
                ReturnDate = ReturnDate,
                Notes = Notes,
                Items = activeLines.Select(l => new CreatePurchaseReturnItemDto
                {
                    ProductId = l.ProductId,
                    Quantity = l.ReturnQuantity,
                    UnitCost = l.UnitCost
                }).ToList()
            };

            int? userId = 1;
            var res = await _returnService.PostReturnAsync(dto, userId);
            if (res.Success)
            {
                RequestClose?.Invoke(true);
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(null);
    }
}

