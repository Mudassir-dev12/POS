using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Shifts;

public partial class ShiftsViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;
    private readonly IShopSettingService _shopSettingService;
    private readonly IPdfReportService _pdfReportService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ShiftSummaryDto? _currentShiftSummary;

    [ObservableProperty]
    private bool _hasActiveShift;

    [ObservableProperty]
    private Shift? _selectedPastShift;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _currencySymbol = "$";

    public ObservableCollection<Shift> PastShifts { get; } = new();

    public ShiftsViewModel(
        IShiftService shiftService,
        IShopSettingService shopSettingService,
        IPdfReportService pdfReportService,
        IDialogService dialogService)
    {
        _shiftService = shiftService;
        _shopSettingService = shopSettingService;
        _pdfReportService = pdfReportService;
        _dialogService = dialogService;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;
        }
        catch { }

        await LoadCurrentShiftAndHistoryAsync();
    }

    [RelayCommand]
    public async Task LoadCurrentShiftAndHistoryAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var openShiftResult = await _shiftService.GetCurrentOpenShiftAsync();
            if (openShiftResult.Success && openShiftResult.Data != null)
            {
                HasActiveShift = true;
                var summaryResult = await _shiftService.GetShiftSummaryAsync(openShiftResult.Data.Id);
                CurrentShiftSummary = summaryResult.Success ? summaryResult.Data : null;
            }
            else
            {
                HasActiveShift = false;
                CurrentShiftSummary = null;
            }

            PastShifts.Clear();
            var searchDto = new ShiftSearchDto { PageSize = 50 };
            var paged = await _shiftService.SearchShiftsAsync(searchDto);
            foreach (var s in paged.Items)
            {
                PastShifts.Add(s);
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
    public async Task OpenShiftDialogAsync()
    {
        if (HasActiveShift)
        {
            _dialogService.ShowMessage("A shift is already open. You must close the active shift first.", "Open Shift");
            return;
        }

        var vm = new OpenShiftDialogViewModel(_shiftService);
        bool? opened = _dialogService.ShowOpenShiftDialog(vm);
        if (opened == true && vm.Confirmed)
        {
            await LoadCurrentShiftAndHistoryAsync();
        }
    }

    [RelayCommand]
    public async Task OpenCashMovementDialogAsync()
    {
        if (!HasActiveShift || CurrentShiftSummary == null)
        {
            _dialogService.ShowMessage("No shift is currently open. Cash movements require an active shift.", "Cash In / Out");
            return;
        }

        var vm = new CashMovementDialogViewModel(CurrentShiftSummary.ShiftId, CurrencySymbol, _shiftService);
        bool? saved = _dialogService.ShowCashMovementDialog(vm);
        if (saved == true && vm.Confirmed)
        {
            await LoadCurrentShiftAndHistoryAsync();
        }
    }

    [RelayCommand]
    public async Task CloseShiftDialogAsync()
    {
        if (!HasActiveShift || CurrentShiftSummary == null)
        {
            _dialogService.ShowMessage("There is no active shift to close.", "Close Shift");
            return;
        }

        var vm = new CloseShiftDialogViewModel(
            CurrentShiftSummary.ShiftId,
            CurrentShiftSummary.ShiftNumber,
            CurrentShiftSummary.ExpectedCash,
            CurrencySymbol,
            _shiftService);

        bool? closed = _dialogService.ShowCloseShiftDialog(vm);
        if (closed == true && vm.Confirmed && vm.ClosedSummary != null)
        {
            _dialogService.ShowMessage(
                $"Shift '{vm.ClosedSummary.ShiftNumber}' closed successfully. Difference: {CurrencySymbol}{vm.ClosedSummary.Difference:F2}",
                "Shift Closed");

            // Prompt for Z report
            bool printZ = _dialogService.ShowConfirmation("Would you like to generate the final Z report for this shift?", "Print Z Report");
            if (printZ)
            {
                await ExportShiftReportPdfAsync(vm.ClosedSummary, isZReport: true);
            }

            await LoadCurrentShiftAndHistoryAsync();
        }
    }

    [RelayCommand]
    public async Task PrintXReportAsync()
    {
        if (!HasActiveShift || CurrentShiftSummary == null)
        {
            _dialogService.ShowMessage("No active shift available for X reading.", "X Report");
            return;
        }

        await ExportShiftReportPdfAsync(CurrentShiftSummary, isZReport: false);
    }

    [RelayCommand]
    public async Task PrintZReportAsync()
    {
        if (SelectedPastShift == null)
        {
            _dialogService.ShowMessage("Please select a closed shift from the history list to generate a Z report.", "Z Report");
            return;
        }

        var summaryResult = await _shiftService.GetShiftSummaryAsync(SelectedPastShift.Id);
        if (summaryResult.Success && summaryResult.Data != null)
        {
            await ExportShiftReportPdfAsync(summaryResult.Data, isZReport: true);
        }
    }

    private async Task ExportShiftReportPdfAsync(ShiftSummaryDto summary, bool isZReport)
    {
        string reportType = isZReport ? "Z_Report" : "X_Report";
        string? filePath = _dialogService.ShowSaveFileDialog("PDF Files (*.pdf)|*.pdf", $"{reportType}_{summary.ShiftNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            var settingDto = new ShopSettingDto
            {
                ShopName = settings.ShopName,
                Address = settings.Address,
                Phone = settings.Phone,
                TaxNumber = settings.TaxNumber,
                CurrencySymbol = settings.CurrencySymbol
            };

            byte[] bytes = await _pdfReportService.GenerateShiftReportPdfAsync(summary, settingDto, isZReport);
            await File.WriteAllBytesAsync(filePath, bytes);
            _dialogService.ShowMessage($"{reportType} exported to '{filePath}'.", "Report Generated");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Failed to generate report: {ex.Message}", "Report Error");
        }
    }
}

