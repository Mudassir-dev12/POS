using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.UI.ViewModels.Brands;

public partial class BrandDialogViewModel : ObservableObject
{
    private readonly IBrandService _brandService;
    private readonly int? _currentUserId;
    
    [ObservableProperty]
    private string _dialogTitle = "Add Brand";

    [ObservableProperty]
    private bool _isCreateMode = true;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _generalError;

    public event Action<bool>? RequestClose;

    public BrandDialogViewModel(IBrandService brandService, int? currentUserId)
    {
        _brandService = brandService;
        _currentUserId = currentUserId;    }

    public void Initialize(BrandDto? brand = null)
    {
        if (brand != null)
        {
            IsCreateMode = false;
            DialogTitle = "Edit Brand";
            Id = brand.Id;
            Name = brand.Name;
            Description = brand.Description ?? string.Empty;
            IsActive = brand.IsActive;
        }
        else
        {
            IsCreateMode = true;
            DialogTitle = "Add New Brand";
            IsActive = true;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        NameError = null;
        GeneralError = null;

        if (IsCreateMode)
        {
            var res = await _brandService.CreateBrandAsync(new CreateBrandDto
            {
                Name = Name,
                Description = Description
            }, _currentUserId);

            if (!res.Success)
            {
                ApplyErrors(res);
                return;
            }
        }
        else
        {
            var res = await _brandService.UpdateBrandAsync(new UpdateBrandDto
            {
                Id = Id,
                Name = Name,
                Description = Description,
                IsActive = IsActive
            }, _currentUserId);

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

