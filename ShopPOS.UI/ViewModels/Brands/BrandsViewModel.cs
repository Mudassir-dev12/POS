using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.Brands;

public partial class BrandsViewModel : BaseCrudViewModel<BrandDto>
{
    private readonly IBrandService _brandService;
    private readonly IDialogService _dialogService;

    public BrandsViewModel(
        IBrandService brandService,
        IShopSettingService shopSettingService,
        IDialogService dialogService)
        : base(shopSettingService, dialogService)
    {
        _brandService = brandService;
        _dialogService = dialogService;
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

            var res = await _brandService.GetPagedBrandsAsync(query, cancellationToken);
            if (res.Success && res.Data != null)
            {
                Items.Clear();
                foreach (var b in res.Data.Items)
                {
                    Items.Add(b);
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
            ErrorMessage = $"Error loading brands: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task AddAsync()
    {
        if (!CanAdd) return;

        var dialogVm = new BrandDialogViewModel(_brandService, GetCurrentUserId());
        dialogVm.Initialize(null);

        if (_dialogService.ShowBrandDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task EditAsync()
    {
        if (!CanEdit || SelectedItem == null) return;

        var dialogVm = new BrandDialogViewModel(_brandService, GetCurrentUserId());
        dialogVm.Initialize(SelectedItem);

        if (_dialogService.ShowBrandDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task DeleteOrDeactivateAsync()
    {
        if (!CanDelete || SelectedItem == null) return;

        int id = SelectedItem.Id;
        string name = SelectedItem.Name;

        var delRes = await _brandService.DeleteBrandAsync(id, GetCurrentUserId());
        if (delRes.Success)
        {
            await LoadDataAsync();
            return;
        }

        if (delRes.ErrorMessage == "REFERENCED_CANNOT_DELETE")
        {
            var result = _dialogService.ShowConfirmActionDialog(
                "Brand In Use",
                $"Brand '{name}' is assigned to existing products and cannot be deleted.\n\nWould you like to deactivate it instead?",
                isReferenced: true);

            if (result == ConfirmActionResult.Deactivate)
            {
                await _brandService.DeactivateBrandAsync(id, GetCurrentUserId());
                await LoadDataAsync();
            }
        }
        else
        {
            ErrorMessage = delRes.ErrorMessage;
        }
    }

    protected override async Task<IReadOnlyList<int>> FetchAllFilteredIdsAsync()
    {
        return await _brandService.GetFilteredBrandIdsAsync(SearchTerm, ShowInactive);
    }

    protected override async Task<BulkOperationResult> ExecuteBulkDeleteAsync(IReadOnlyList<int> ids)
    {
        return await _brandService.BulkDeleteAsync(ids, GetCurrentUserId());
    }

    protected override async Task<BulkOperationResult> ExecuteBulkDeactivateAsync(IReadOnlyList<int> ids)
    {
        return await _brandService.BulkDeactivateAsync(ids, GetCurrentUserId());
    }

}
