using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Shifts;

public partial class CashMovementDialogViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;

    [ObservableProperty]
    private int _shiftId;

    [ObservableProperty]
    private CashMovementType _type = CashMovementType.CashIn;

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _currencySymbol = "$";

    public bool Confirmed { get; private set; }

    public event Action? RequestClose;

    public CashMovementDialogViewModel(int shiftId, string currencySymbol, IShiftService shiftService)
    {
        _shiftId = shiftId;
        _currencySymbol = currencySymbol;
        _shiftService = shiftService;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;
        if (Amount <= 0m)
        {
            ErrorMessage = "Amount must be greater than zero.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "Reason is required for cash movements.";
            return;
        }

        IsLoading = true;
        try
        {
            var dto = new CashMovementDto
            {
                ShiftId = ShiftId,
                Type = Type,
                Amount = Amount,
                Reason = Reason.Trim()
            };

            var result = await _shiftService.AddCashMovementAsync(dto);
            if (result.Success)
            {
                Confirmed = true;
                RequestClose?.Invoke();
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to save cash movement.";
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

