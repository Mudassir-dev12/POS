using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.Suppliers;

public partial class SuppliersViewModel : BaseCrudViewModel<SupplierDto>
{
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogService;
    private readonly ISupplierLedgerService? _ledgerService;
    private readonly INavigationService? _navigationService;

    public SuppliersViewModel(
        ISupplierService supplierService,
        IShopSettingService shopSettingService,
        IDialogService dialogService,
        ISupplierLedgerService? ledgerService = null,
        INavigationService? navigationService = null)
        : base(shopSettingService, dialogService)
    {
        _supplierService = supplierService;
        _dialogService = dialogService;
        _ledgerService = ledgerService;
        _navigationService = navigationService;
    }

    protected override void ApplyPermissions()
    {
        CanAdd = true;
        CanEdit = true;
        CanDelete = true;
        CanDeactivate = true;
    }

    public override async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var query = new MasterDataSearchQuery
            {
                SearchTerm = SearchTerm,
                IsActive = ShowInactive ? null : true,
                PageNumber = CurrentPage,
                PageSize = PageSize
            };

            var res = await _supplierService.GetPagedSuppliersAsync(query, GetCurrentRole(), cancellationToken);
            if (res.Success && res.Data != null)
            {
                Items.Clear();
                foreach (var s in res.Data.Items)
                {
                    Items.Add(s);
                }

                if (_ledgerService != null && Items.Count > 0)
                {
                    var balRes = await _ledgerService.GetSupplierBalancesAsync(Items.Select(s => s.Id), GetCurrentRole(), cancellationToken);
                    if (balRes.Success && balRes.Data != null)
                    {
                        foreach (var s in Items)
                        {
                            if (balRes.Data.TryGetValue(s.Id, out var bal))
                            {
                                s.CurrentBalance = bal;
                            }
                        }
                    }
                }

                UpdatePagingState(res.Data.TotalCount, res.Data.PageNumber, res.Data.PageSize);
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading suppliers: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task AddAsync()
    {
        if (!CanAdd) return;

        var dialogVm = new SupplierDialogViewModel(_supplierService, GetCurrentUserId(), GetCurrentRole());
        dialogVm.Initialize(null);

        if (_dialogService.ShowSupplierDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task EditAsync()
    {
        if (!CanEdit || SelectedItem == null) return;

        var dialogVm = new SupplierDialogViewModel(_supplierService, GetCurrentUserId(), GetCurrentRole());
        dialogVm.Initialize(SelectedItem);

        if (_dialogService.ShowSupplierDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task DeleteOrDeactivateAsync()
    {
        if (!CanDelete || SelectedItem == null) return;

        int id = SelectedItem.Id;
        string name = SelectedItem.Name;

        var delRes = await _supplierService.DeleteSupplierAsync(id, GetCurrentUserId(), GetCurrentRole());
        if (delRes.Success)
        {
            await LoadDataAsync();
            return;
        }

        if (delRes.ErrorMessage == "REFERENCED_CANNOT_DELETE")
        {
            var result = _dialogService.ShowConfirmActionDialog(
                "Supplier In Use",
                $"Supplier '{name}' has recorded purchase activity and cannot be deleted.\n\nWould you like to deactivate this vendor instead?",
                isReferenced: true);

            if (result == ConfirmActionResult.Deactivate)
            {
                await _supplierService.DeactivateSupplierAsync(id, GetCurrentUserId(), GetCurrentRole());
                await LoadDataAsync();
            }
        }
        else
        {
            ErrorMessage = delRes.ErrorMessage;
        }
    }

    [RelayCommand]
    public void ViewLedger(SupplierDto? supplier = null)
    {
        var target = supplier ?? SelectedItem;
        if (target != null && _navigationService != null)
        {
            _navigationService.NavigateTo("SupplierLedger", target.Id);
        }
    }

    protected override async Task<IReadOnlyList<int>> FetchAllFilteredIdsAsync()
    {
        return await _supplierService.GetFilteredSupplierIdsAsync(SearchTerm, ShowInactive);
    }

    protected override async Task<BulkOperationResult> ExecuteBulkDeleteAsync(IReadOnlyList<int> ids)
    {
        return await _supplierService.BulkDeleteAsync(ids, GetCurrentUserId());
    }

    protected override async Task<BulkOperationResult> ExecuteBulkDeactivateAsync(IReadOnlyList<int> ids)
    {
        return await _supplierService.BulkDeactivateAsync(ids, GetCurrentUserId());
    }

}
