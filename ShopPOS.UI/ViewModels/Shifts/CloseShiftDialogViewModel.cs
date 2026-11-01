using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Shifts;

public partial class CloseShiftDialogViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;

    [ObservableProperty]
    private int _shiftId;

    [ObservableProperty]
    private string _shiftNumber = string.Empty;

    [ObservableProperty]
    private decimal _expectedCash;

    [ObservableProperty]
    private decimal _countedCash;

    [ObservableProperty]
    private decimal _difference;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _currencySymbol = "$";

    public ShiftSummaryDto? ClosedSummary { get; private set; }
    public bool Confirmed { get; private set; }

    public event Action? RequestClose;

    public CloseShiftDialogViewModel(int shiftId, string shiftNumber, decimal expectedCash, string currencySymbol, IShiftService shiftService)
    {
        _shiftId = shiftId;
        _shiftNumber = shiftNumber;
        _expectedCash = expectedCash;
        _currencySymbol = currencySymbol;
        _shiftService = shiftService;

        _countedCash = expectedCash;
        _difference = 0m;
    }

    partial void OnCountedCashChanged(decimal value)
    {
        Difference = Math.Round(value - ExpectedCash, 2, MidpointRounding.AwayFromZero);
    }

    [RelayCommand]
    public async Task CloseShiftAsync()
    {
        ErrorMessage = null;
        if (CountedCash < 0m)
        {
            ErrorMessage = "Counted cash cannot be negative.";
            return;
        }

        IsLoading = true;
        try
        {
            var dto = new CloseShiftDto
            {
                ShiftId = ShiftId,
                CountedCash = CountedCash,
                Notes = Notes?.Trim()
            };

            var result = await _shiftService.CloseShiftAsync(dto);
            if (result.Success && result.Data != null)
            {
                ClosedSummary = result.Data;
                Confirmed = true;
                RequestClose?.Invoke();
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to close shift.";
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

