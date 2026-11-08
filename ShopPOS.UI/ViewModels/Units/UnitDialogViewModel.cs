using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.UI.ViewModels.Units;

public partial class UnitDialogViewModel : ObservableObject
{
    private readonly IUnitService _unitService;
    private readonly int? _currentUserId;
    
    [ObservableProperty]
    private string _dialogTitle = "Add Unit of Measure";

    [ObservableProperty]
    private bool _isCreateMode = true;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _shortName = string.Empty;

    [ObservableProperty]
    private bool _allowDecimal = false;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _shortNameError;

    [ObservableProperty]
    private string? _generalError;

    public event Action<bool>? RequestClose;

    public UnitDialogViewModel(IUnitService unitService, int? currentUserId)
    {
        _unitService = unitService;
        _currentUserId = currentUserId;    }

    public void Initialize(UnitDto? unit = null)
    {
        if (unit != null)
        {
            IsCreateMode = false;
            DialogTitle = "Edit Unit";
            Id = unit.Id;
            Name = unit.Name;
            ShortName = unit.ShortName;
            AllowDecimal = unit.AllowDecimal;
            IsActive = unit.IsActive;
        }
        else
        {
            IsCreateMode = true;
            DialogTitle = "Add New Unit";
            IsActive = true;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        NameError = null;
        ShortNameError = null;
        GeneralError = null;

        if (IsCreateMode)
        {
            var res = await _unitService.CreateUnitAsync(new CreateUnitDto
            {
                Name = Name,
                ShortName = ShortName,
                AllowDecimal = AllowDecimal
            }, _currentUserId);

            if (!res.Success)
            {
                ApplyErrors(res);
                return;
            }
        }
        else
        {
            var res = await _unitService.UpdateUnitAsync(new UpdateUnitDto
            {
                Id = Id,
                Name = Name,
                ShortName = ShortName,
                AllowDecimal = AllowDecimal,
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
        if (res.ValidationErrors.TryGetValue("Name", out var nErrs))
        {
            NameError = string.Join("; ", nErrs);
        }
        if (res.ValidationErrors.TryGetValue("ShortName", out var sErrs))
        {
            ShortNameError = string.Join("; ", sErrs);
        }
    }
}

