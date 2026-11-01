using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.Customers;

public partial class CustomersViewModel : BaseCrudViewModel<CustomerDto>
{
    private readonly ICustomerService _customerService;
    private readonly IPriceLevelService _priceLevelService;
    private readonly IDialogService _dialogService;

    public CustomersViewModel(
        ICustomerService customerService,
        IPriceLevelService priceLevelService,
        IAuthService authService,
        IPermissionService permissionService,
        IShopSettingService shopSettingService,
        IDialogService dialogService)
        : base(authService, permissionService, shopSettingService)
    {
        _customerService = customerService;
        _priceLevelService = priceLevelService;
        _dialogService = dialogService;
    }

    protected override void ApplyPermissions()
    {
        string? role = GetCurrentRole();

        // Cashiers can add and edit, but CANNOT delete or deactivate
        CanAdd = PermissionService.CanCreateEditCustomers(role);
        CanEdit = PermissionService.CanCreateEditCustomers(role);
        CanDelete = PermissionService.CanDeleteCustomers(role);
        CanDeactivate = PermissionService.CanDeleteCustomers(role);
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

            var res = await _customerService.GetPagedCustomersAsync(query, cancellationToken);
            if (res.Success && res.Data != null)
            {
                Items.Clear();
                foreach (var c in res.Data.Items)
                {
                    Items.Add(c);
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
            ErrorMessage = $"Error loading customers: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task AddAsync()
    {
        if (!CanAdd) return;

        var dialogVm = new CustomerDialogViewModel(
            _customerService,
            _priceLevelService,
            PermissionService,
            GetCurrentUserId(),
            GetCurrentRole());

        await dialogVm.InitializeAsync(null);

        if (_dialogService.ShowCustomerDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task EditAsync()
    {
        if (!CanEdit || SelectedItem == null) return;

        var dialogVm = new CustomerDialogViewModel(
            _customerService,
            _priceLevelService,
            PermissionService,
            GetCurrentUserId(),
            GetCurrentRole());

        await dialogVm.InitializeAsync(SelectedItem);

        if (_dialogService.ShowCustomerDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task DeleteOrDeactivateAsync()
    {
        if (!CanDelete || SelectedItem == null) return;

        int id = SelectedItem.Id;
        string name = SelectedItem.Name;

        var delRes = await _customerService.DeleteCustomerAsync(id, GetCurrentUserId(), GetCurrentRole());
        if (delRes.Success)
        {
            await LoadDataAsync();
            return;
        }

        if (delRes.ErrorMessage == "REFERENCED_CANNOT_DELETE")
        {
            var result = _dialogService.ShowConfirmActionDialog(
                "Customer In Use",
                $"Customer '{name}' has recorded sales invoices and cannot be deleted.\n\nWould you like to deactivate this account instead?",
                isReferenced: true);

            if (result == ConfirmActionResult.Deactivate)
            {
                await _customerService.DeactivateCustomerAsync(id, GetCurrentUserId(), GetCurrentRole());
                await LoadDataAsync();
            }
        }
        else
        {
            ErrorMessage = delRes.ErrorMessage;
        }
    }
}
