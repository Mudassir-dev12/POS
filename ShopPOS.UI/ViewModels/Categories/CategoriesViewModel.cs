using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Common;

namespace ShopPOS.UI.ViewModels.Categories;

public partial class CategoriesViewModel : BaseCrudViewModel<CategoryDto>
{
    private readonly ICategoryService _categoryService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<CategoryDto> CategoryTree { get; } = new();

    public CategoriesViewModel(
        ICategoryService categoryService,
        IAuthService authService,
        IPermissionService permissionService,
        IShopSettingService shopSettingService,
        IDialogService dialogService)
        : base(authService, permissionService, shopSettingService)
    {
        _categoryService = categoryService;
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
            var res = await _categoryService.GetCategoryTreeAsync(ShowInactive, cancellationToken);
            if (res.Success && res.Data != null)
            {
                CategoryTree.Clear();
                foreach (var c in res.Data)
                {
                    CategoryTree.Add(c);
                }

                RowCountText = $"Loaded {CategoryTree.Count} top-level categories";
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading categories: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task AddAsync()
    {
        await AddChildAsync(null);
    }

    [RelayCommand]
    public async Task AddChildCategoryAsync()
    {
        await AddChildAsync(SelectedItem?.Id);
    }

    private async Task AddChildAsync(int? parentId)
    {
        if (!CanAdd) return;

        var dialogVm = new CategoryDialogViewModel(_categoryService, GetCurrentUserId(), GetCurrentRole());
        await dialogVm.InitializeAsync(null, parentId);

        if (_dialogService.ShowCategoryDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task EditAsync()
    {
        if (!CanEdit || SelectedItem == null) return;

        var dialogVm = new CategoryDialogViewModel(_categoryService, GetCurrentUserId(), GetCurrentRole());
        await dialogVm.InitializeAsync(SelectedItem.Id);

        if (_dialogService.ShowCategoryDialog(dialogVm) == true)
        {
            await LoadDataAsync();
        }
    }

    public override async Task DeleteOrDeactivateAsync()
    {
        if (!CanDelete || SelectedItem == null) return;

        int id = SelectedItem.Id;
        string name = SelectedItem.Name;

        var delRes = await _categoryService.DeleteCategoryAsync(id, GetCurrentUserId(), GetCurrentRole());
        if (delRes.Success)
        {
            await LoadDataAsync();
            return;
        }

        if (delRes.ErrorMessage == "REFERENCED_CANNOT_DELETE")
        {
            var result = _dialogService.ShowConfirmActionDialog(
                "Category In Use",
                $"Category '{name}' has existing products or subcategories and cannot be deleted.\n\nWould you like to deactivate it instead?",
                isReferenced: true);

            if (result == ConfirmActionResult.Deactivate)
            {
                await _categoryService.DeactivateCategoryAsync(id, GetCurrentUserId(), GetCurrentRole());
                await LoadDataAsync();
            }
        }
        else
        {
            ErrorMessage = delRes.ErrorMessage;
        }
    }
}
