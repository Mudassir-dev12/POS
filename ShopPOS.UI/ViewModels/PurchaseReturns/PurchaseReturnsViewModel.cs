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
        IAuthService authService,
        IPermissionService permissionService,
        IShopSettingService shopSettingService)
        : base(authService, permissionService, shopSettingService)
    {
        _returnService = returnService;
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
            ErrorMessage = "Access Denied: Cashiers do not have permission to access Purchase Returns.";
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
            ErrorMessage = "Access Denied: Cashiers do not have permission to access Purchase Returns.";
            return;
        }

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
        string prompt = $"Cancel purchase return '{selected.ReturnNumber}' for total {selected.TotalAmount:C}?\n\nThis will reverse the inventory restoration and restore the payable balance to the vendor.";
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
}
