using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Shifts;

public partial class OpenShiftDialogViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;

    [ObservableProperty]
    private decimal _openingCash = 0m;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public Shift? OpenedShift { get; private set; }
    public bool Confirmed { get; private set; }

    public event Action? RequestClose;

    public OpenShiftDialogViewModel(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [RelayCommand]
    public async Task OpenShiftAsync()
    {
        ErrorMessage = null;
        if (OpeningCash < 0m)
        {
            ErrorMessage = "Opening cash float cannot be negative.";
            return;
        }

        IsLoading = true;
        try
        {
            var dto = new OpenShiftDto
            {
                OpeningCash = OpeningCash,
                Notes = Notes?.Trim()
            };

            var result = await _shiftService.OpenShiftAsync(dto);
            if (result.Success && result.Data != null)
            {
                OpenedShift = result.Data;
                Confirmed = true;
                RequestClose?.Invoke();
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to open shift.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke();
    }
}

