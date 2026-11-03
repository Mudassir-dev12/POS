using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.SupplierPayments;

public partial class SupplierPaymentsViewModel : BaseCrudViewModel<SupplierPaymentDto>
{
    private readonly ISupplierPaymentService _paymentService;
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private int? _selectedSupplierId;

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public SupplierPaymentsViewModel(
        ISupplierPaymentService paymentService,
        ISupplierService supplierService,
        IDialogService dialogService,
        IServiceProvider serviceProvider,
        IShopSettingService shopSettingService)
        : base(shopSettingService, dialogService)
    {
        CanShowDeleteSelected = false;
        CanShowCancelSelected = true;
        _paymentService = paymentService;
        _supplierService = supplierService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
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
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var res = await _paymentService.GetPaymentsAsync(SelectedSupplierId, CurrentPage, PageSize, GetCurrentRole(), cancellationToken);
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
            ErrorMessage = $"Error loading supplier payments: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSupplierIdChanged(int? value) => _ = LoadDataAsync();

    [RelayCommand]
    public async Task RecordPaymentAsync()
    {
        if (!CanAdd) return;

        var dialogVm = _serviceProvider.GetRequiredService<SupplierPaymentDialogViewModel>();
        await dialogVm.InitializeAsync(SelectedSupplierId, null);

        if (_dialogService.ShowSupplierPaymentDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override Task AddAsync() => RecordPaymentAsync();
    public override Task EditAsync() => Task.CompletedTask;
    public override Task DeleteOrDeactivateAsync() => CancelSelectedPaymentAsync();

    [RelayCommand]
    public async Task CancelSelectedPaymentAsync()
    {
        if (SelectedItem == null || SelectedItem.IsCancelled) return;

        string prompt = $"Cancel payment '{SelectedItem.PaymentNumber}' for amount {ShopPOS.Core.Services.MoneyFormatter.Instance.Format(SelectedItem.Amount)}?\n\nThis will reverse the payment credit on the supplier balance and invoice.";
        string? reason = _dialogService.ShowReasonPromptDialog("Cancel Vendor Payment", prompt);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        IsLoading = true;
        try
        {
            var res = await _paymentService.CancelPaymentAsync(SelectedItem.Id, reason, GetCurrentUserId(), GetCurrentRole());
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

    protected override async Task<IReadOnlyList<int>> FetchAllFilteredIdsAsync()
    {
        return await _paymentService.GetFilteredPaymentIdsAsync(SelectedSupplierId);
    }

    protected override async Task<BulkOperationResult> ExecuteBulkCancelAsync(IReadOnlyList<int> ids, string reason)
    {
        return await _paymentService.BulkCancelPaymentsAsync(ids, reason, GetCurrentUserId());
    }

}
