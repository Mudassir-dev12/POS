using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.PurchaseReturns;

public partial class PurchaseReturnsViewModel : BaseCrudViewModel<PurchaseReturnDto>
{
    private readonly IPurchaseReturnService _returnService;
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private int? _selectedSupplierId;

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public PurchaseReturnsViewModel(
        IPurchaseReturnService returnService,
        ISupplierService supplierService,
        IDialogService dialogService,
        IServiceProvider serviceProvider,
        IShopSettingService shopSettingService)
        : base(shopSettingService, dialogService)
    {
        CanShowDeleteSelected = false;
        CanShowCancelSelected = true;
        _returnService = returnService;
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
            var res = await _returnService.GetReturnsAsync(SelectedSupplierId, CurrentPage, PageSize, GetCurrentRole(), cancellationToken);
            if (res.Success && res.Data != null)
            {
                Items.Clear();
                foreach (var r in res.Data.Items)
                {
                    Items.Add(r);
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
            ErrorMessage = $"Error loading purchase returns: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSupplierIdChanged(int? value) => _ = LoadDataAsync();

    [RelayCommand]
    public async Task NewReturnAsync()
    {
        if (!CanAdd) return;

        var dialogVm = _serviceProvider.GetRequiredService<PurchaseReturnDialogViewModel>();
        await dialogVm.InitializeAsync(SelectedSupplierId, null);

        if (_dialogService.ShowPurchaseReturnDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override Task AddAsync() => NewReturnAsync();
    public override Task EditAsync() => Task.CompletedTask;
    public override Task DeleteOrDeactivateAsync() => CancelSelectedReturnAsync();

    [RelayCommand]
    public async Task CancelSelectedReturnAsync()
    {
        if (SelectedItem == null || SelectedItem.Status == PurchaseReturnStatus.Cancelled) return;

        var selected = SelectedItem;
        string prompt = $"Cancel purchase return '{selected.ReturnNumber}' for total {ShopPOS.Core.Services.MoneyFormatter.Instance.Format(selected.TotalAmount)}?\n\nThis will reverse the inventory restoration and restore the payable balance to the vendor.";
        string? reason = _dialogService.ShowReasonPromptDialog("Cancel Purchase Return", prompt);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        IsLoading = true;
        try
        {
            var res = await _returnService.CancelPurchaseReturnAsync(selected.Id, reason, GetCurrentUserId(), GetCurrentRole());
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
        return await _returnService.GetFilteredReturnIdsAsync(SelectedSupplierId);
    }

    protected override async Task<BulkOperationResult> ExecuteBulkCancelAsync(IReadOnlyList<int> ids, string reason)
    {
        return await _returnService.BulkCancelReturnsAsync(ids, reason, GetCurrentUserId());
    }

}
