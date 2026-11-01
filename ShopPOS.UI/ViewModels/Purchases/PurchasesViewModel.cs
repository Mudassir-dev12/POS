using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.Purchases;

public partial class PurchasesViewModel : BaseCrudViewModel<PurchaseListDto>
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private int? _selectedSupplierId;

    [ObservableProperty]
    private PurchaseStatus? _selectedStatus;

    [ObservableProperty]
    private PurchasePaymentStatus? _selectedPaymentStatus;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public PurchasesViewModel(
        IPurchaseService purchaseService,
        ISupplierService supplierService,
        IDialogService dialogService,
        IServiceProvider serviceProvider,
        IAuthService authService,
        IPermissionService permissionService,
        IShopSettingService shopSettingService)
        : base(authService, permissionService, shopSettingService)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
    }

    protected override void ApplyPermissions()
    {
        string? role = GetCurrentRole();
        bool canManage = PermissionService.CanManagePurchases(role);

        CanAdd = canManage;
        CanEdit = canManage;
        CanDelete = canManage;
        CanDeactivate = canManage;

        if (!canManage)
        {
            ErrorMessage = "Access Denied: Cashiers do not have permission to access Purchases.";
        }
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        try
        {
            var supRes = await _supplierService.GetFlatListAsync(false, GetCurrentRole());
            if (supRes.Success && supRes.Data != null)
            {
                Suppliers.Clear();
                foreach (var s in supRes.Data)
                {
                    Suppliers.Add(s);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading suppliers: {ex.Message}";
        }
    }

    public override async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!PermissionService.CanManagePurchases(GetCurrentRole()))
        {
            ErrorMessage = "Access Denied: Cashiers do not have permission to access Purchases.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var query = new PurchaseSearchQuery
            {
                SearchTerm = SearchTerm,
                SupplierId = SelectedSupplierId,
                Status = SelectedStatus,
                PaymentStatus = SelectedPaymentStatus,
                FromDate = FromDate,
                ToDate = ToDate,
                PageNumber = CurrentPage,
                PageSize = PageSize
            };

            var res = await _purchaseService.GetPurchasesAsync(query, GetCurrentRole(), cancellationToken);
            if (res.Success && res.Data != null)
            {
                Items.Clear();
                foreach (var p in res.Data.Items)
                {
                    Items.Add(p);
                }

                UpdatePagingState(res.Data.TotalCount, res.Data.PageNumber, res.Data.PageSize);
                SelectedItem = Items.FirstOrDefault();
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading purchases: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSupplierIdChanged(int? value) => _ = LoadDataAsync();
    partial void OnSelectedStatusChanged(PurchaseStatus? value) => _ = LoadDataAsync();
    partial void OnSelectedPaymentStatusChanged(PurchasePaymentStatus? value) => _ = LoadDataAsync();
    partial void OnFromDateChanged(DateTime? value) => _ = LoadDataAsync();
    partial void OnToDateChanged(DateTime? value) => _ = LoadDataAsync();

    [RelayCommand]
    public async Task NewPurchaseAsync()
    {
        if (!CanAdd) return;

        var entryVm = _serviceProvider.GetRequiredService<PurchaseEntryViewModel>();
        await entryVm.InitializeAsync(null);

        if (_dialogService.ShowPurchaseEntryDialog(entryVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override Task AddAsync() => NewPurchaseAsync();
    public override Task EditAsync() => OpenPurchaseAsync();
    public override Task DeleteOrDeactivateAsync() => CancelSelectedPurchaseAsync();

    [RelayCommand]
    public async Task OpenPurchaseAsync()
    {
        if (SelectedItem == null) return;

        var entryVm = _serviceProvider.GetRequiredService<PurchaseEntryViewModel>();
        await entryVm.InitializeAsync(SelectedItem.Id);

        if (_dialogService.ShowPurchaseEntryDialog(entryVm) == true)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public async Task PostSelectedPurchaseAsync()
    {
        if (SelectedItem == null || SelectedItem.Status != PurchaseStatus.Draft) return;

        string prompt = $"Post purchase '{SelectedItem.PurchaseNumber}' for total {SelectedItem.GrandTotal:C}?\n\nThis will immediately adjust stock inventory and update product weighted average costs.";
        var confirm = _dialogService.ShowConfirmActionDialog("Post Purchase", prompt, isReferenced: false);
        if (confirm != ConfirmActionResult.Delete)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var res = await _purchaseService.PostPurchaseAsync(SelectedItem.Id, null, null, GetCurrentUserId(), GetCurrentRole());
            if (res.Success)
            {
                await LoadDataAsync();
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
    public async Task CancelSelectedPurchaseAsync()
    {
        if (SelectedItem == null || SelectedItem.Status == PurchaseStatus.Cancelled) return;

        string prompt = $"Are you sure you want to cancel purchase '{SelectedItem.PurchaseNumber}'?\n" +
            (SelectedItem.Status == PurchaseStatus.Posted
                ? "This purchase has been POSTED. Cancelling it will write reversing movements to the stock ledger."
                : "This draft purchase will be marked as cancelled.");

        string? reason = _dialogService.ShowReasonPromptDialog("Cancel Purchase", prompt);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return; // Cancelled by user or no reason provided
        }

        IsLoading = true;
        try
        {
            var res = await _purchaseService.CancelPurchaseAsync(SelectedItem.Id, reason, GetCurrentUserId(), GetCurrentRole());
            if (res.Success)
            {
                await LoadDataAsync();
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
    public async Task ExportPdfAsync()
    {
        if (SelectedItem == null) return;

        string defaultName = $"Purchase_{SelectedItem.PurchaseNumber}.pdf";
        string? targetPath = _dialogService.ShowSaveFileDialog("PDF Files (*.pdf)|*.pdf", defaultName, "Export Purchase PDF");
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        IsLoading = true;
        try
        {
            var res = await _purchaseService.ExportPurchasePdfAsync(SelectedItem.Id, GetCurrentRole());
            if (res.Success && res.Data != null)
            {
                await File.WriteAllBytesAsync(targetPath, res.Data);
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export PDF: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
