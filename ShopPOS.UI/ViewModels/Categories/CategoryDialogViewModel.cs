using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.UI.ViewModels.Categories;

public partial class CategoryDialogViewModel : ObservableObject
{
    private readonly ICategoryService _categoryService;
    private readonly int? _currentUserId;
    private readonly string? _currentUserRole;

    [ObservableProperty]
    private string _dialogTitle = "Add Category";

    [ObservableProperty]
    private bool _isCreateMode = true;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int? _selectedParentId;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _generalError;

    public ObservableCollection<CategoryDto> AvailableParents { get; } = new();

    public event Action<bool>? RequestClose;

    public CategoryDialogViewModel(
        ICategoryService categoryService,
        int? currentUserId,
        string? currentUserRole)
    {
        _categoryService = categoryService;
        _currentUserId = currentUserId;
        _currentUserRole = currentUserRole;
    }

    public async Task InitializeAsync(int? categoryId = null, int? preselectedParentId = null)
    {
        var flatRes = await _categoryService.GetFlatListAsync(false);
        var flatList = flatRes.Success && flatRes.Data != null ? flatRes.Data.ToList() : new List<CategoryDto>();

        AvailableParents.Clear();
        AvailableParents.Add(new CategoryDto { Id = 0, Name = "(None - Root Level Category)" });

        foreach (var c in flatList)
        {
            if (!categoryId.HasValue || c.Id != categoryId.Value)
            {
                AvailableParents.Add(c);
            }
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            IsCreateMode = false;
            DialogTitle = "Edit Category";

            var target = flatList.FirstOrDefault(c => c.Id == categoryId.Value);
            if (target != null)
            {
                Id = target.Id;
                Name = target.Name;
                SelectedParentId = target.ParentCategoryId ?? 0;
                IsActive = target.IsActive;
            }
        }
        else
        {
            IsCreateMode = true;
            DialogTitle = "Add New Category";
            SelectedParentId = preselectedParentId ?? 0;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        NameError = null;
        GeneralError = null;

        int? parentId = SelectedParentId > 0 ? SelectedParentId : null;

        if (IsCreateMode)
        {
            var createDto = new CreateCategoryDto
            {
                Name = Name,
                ParentCategoryId = parentId
            };

            var res = await _categoryService.CreateCategoryAsync(createDto, _currentUserId, _currentUserRole);
            if (!res.Success)
            {
                ApplyErrors(res);
                return;
            }
        }
        else
        {
            var updateDto = new UpdateCategoryDto
            {
                Id = Id,
                Name = Name,
                ParentCategoryId = parentId,
                IsActive = IsActive
            };

            var res = await _categoryService.UpdateCategoryAsync(updateDto, _currentUserId, _currentUserRole);
            if (!res.Success)
            {
                ApplyErrors(res);
                return;
            }
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void ApplyErrors(Result res)
    {
        GeneralError = res.ErrorMessage;
        if (res.ValidationErrors.TryGetValue("Name", out var errs))
        {
            NameError = string.Join("; ", errs);
        }
    }
}

