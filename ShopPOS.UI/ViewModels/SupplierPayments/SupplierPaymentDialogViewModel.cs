using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.SupplierPayments;

public partial class SupplierPaymentDialogViewModel : ObservableObject
{
    private readonly ISupplierPaymentService _paymentService;
    private readonly ISupplierService _supplierService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _title = "Record Vendor Payment";

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private PurchaseListDto? _selectedInvoice;

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    private DateTime _paymentDate = DateTime.Today;

    [ObservableProperty]
    private PaymentMethod _method = PaymentMethod.Cash;

    public PaymentMethod[] AvailablePaymentMethods { get; } = Enum.GetValues<PaymentMethod>();

    [ObservableProperty]
    private string? _reference;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<PurchaseListDto> Invoices { get; } = new();

    public event Action<bool?>? RequestClose;

    public SupplierPaymentDialogViewModel(
        ISupplierPaymentService paymentService,
        ISupplierService supplierService,
        IPurchaseService purchaseService,
        IAuthService authService)
    {
        _paymentService = paymentService;
        _supplierService = supplierService;
        _purchaseService = purchaseService;
        _authService = authService;
    }

    public async Task InitializeAsync(int? preselectedSupplierId = null, int? preselectedPurchaseId = null)
    {
        IsLoading = true;
        try
        {
            var supRes = await _supplierService.GetFlatListAsync(false, _authService.CurrentUser?.Role?.Name);
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

        if (!supplierId.HasValue) return;

        var res = await _purchaseService.GetPurchasesAsync(new PurchaseSearchQuery
        {
            SupplierId = supplierId.Value,
            Status = PurchaseStatus.Posted,
            PageNumber = 1,
            PageSize = 100
        }, _authService.CurrentUser?.Role?.Name);

        if (res.Success && res.Data != null)
        {
            foreach (var p in res.Data.Items.Where(p => p.Balance > 0))
            {
                Invoices.Add(p);
            }
        }
    }

    partial void OnSelectedInvoiceChanged(PurchaseListDto? value)
    {
        if (value != null)
        {
            Amount = value.Balance;
            Reference = $"Payment on {value.PurchaseNumber}";
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedSupplier == null)
        {
            ErrorMessage = "Please select a supplier.";
            return;
        }

        if (Amount <= 0)
        {
            ErrorMessage = "Payment amount must be greater than zero.";
            return;
        }

        if (SelectedInvoice != null && Amount > SelectedInvoice.Balance)
        {
            ErrorMessage = $"Payment amount ({Amount:C}) cannot exceed the invoice outstanding balance ({SelectedInvoice.Balance:C}).";
            return;
        }

        IsLoading = true;
        try
        {
            var dto = new CreateSupplierPaymentDto
            {
                SupplierId = SelectedSupplier.Id,
                PurchaseId = SelectedInvoice?.Id,
                PaymentDate = PaymentDate,
                Amount = Amount,
                Method = Method,
                Reference = Reference,
                Notes = Notes
            };

            int? userId = _authService.CurrentUser?.Id;
            string? userRole = _authService.CurrentUser?.Role?.Name;

            var res = await _paymentService.RecordPaymentAsync(dto, userId, userRole);
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

