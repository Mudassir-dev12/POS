using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.Units;

public partial class UnitsViewModel : BaseCrudViewModel<UnitDto>
{
    private readonly IUnitService _unitService;
    private readonly IDialogService _dialogService;

    public UnitsViewModel(
        IUnitService unitService,
        IAuthService authService,
        IPermissionService permissionService,
        IShopSettingService shopSettingService,
        IDialogService dialogService)
        : base(authService, permissionService, shopSettingService)
    {
        _unitService = unitService;
        _dialogService = dialogService;
    }

    protected override void ApplyPermissions()
    {
        string? role = GetCurrentRole();
        bool canManage = PermissionService.CanManageCatalog(role);

        CanAdd = canManage;
        CanEdit = canManage;
        CanDelete = canManage;
        CanDeactivate = canManage;
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

            var res = await _unitService.GetPagedUnitsAsync(query, cancellationToken);
            if (res.Success && res.Data != null)
            {
                Items.Clear();
                foreach (var u in res.Data.Items)
                {
                    Items.Add(u);
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
            ErrorMessage = $"Error loading units: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task AddAsync()
    {
        if (!CanAdd) return;

        var dialogVm = new UnitDialogViewModel(_unitService, GetCurrentUserId(), GetCurrentRole());
        dialogVm.Initialize(null);

        if (_dialogService.ShowUnitDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task EditAsync()
    {
        if (!CanEdit || SelectedItem == null) return;

        var dialogVm = new UnitDialogViewModel(_unitService, GetCurrentUserId(), GetCurrentRole());
        dialogVm.Initialize(SelectedItem);

        if (_dialogService.ShowUnitDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task DeleteOrDeactivateAsync()
    {
        if (!CanDelete || SelectedItem == null) return;

        int id = SelectedItem.Id;
        string name = SelectedItem.Name;

        var delRes = await _unitService.DeleteUnitAsync(id, GetCurrentUserId(), GetCurrentRole());
        if (delRes.Success)
        {
            await LoadDataAsync();
            return;
        }

        if (delRes.ErrorMessage == "REFERENCED_CANNOT_DELETE")
        {
            var result = _dialogService.ShowConfirmActionDialog(
                "Unit In Use",
                $"Unit '{name}' is currently used by catalog products and cannot be deleted.\n\nWould you like to deactivate it instead?",
                isReferenced: true);

            if (result == ConfirmActionResult.Deactivate)
            {
                await _unitService.DeactivateUnitAsync(id, GetCurrentUserId(), GetCurrentRole());
                await LoadDataAsync();
            }
        }
        else
        {
            ErrorMessage = delRes.ErrorMessage;
        }
    }
}
