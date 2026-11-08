using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.SupplierLedger;

public partial class SupplierLedgerViewModel : ObservableObject
{
    private readonly ISupplierLedgerService _ledgerService;
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogService;
            private readonly IShopSettingService _shopSettingService;

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private DateTime _toDate = DateTime.Today;

    [ObservableProperty]
    private decimal _openingBalance;

    [ObservableProperty]
    private decimal _totalDebits;

    [ObservableProperty]
    private decimal _totalCredits;

    [ObservableProperty]
    private decimal _closingBalance;

    [ObservableProperty]
    private string _currencySymbol = "Rs";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<SupplierLedgerEntryDto> Entries { get; } = new();

    public SupplierLedgerViewModel(
        ISupplierLedgerService ledgerService,
        ISupplierService supplierService,
        IDialogService dialogService,
        IShopSettingService shopSettingService)
    {
        _ledgerService = ledgerService;
        _supplierService = supplierService;
        _dialogService = dialogService;
        _shopSettingService = shopSettingService;
    }

    public async Task InitializeAsync(int? preselectedSupplierId = null)
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;
        }
        catch
        {
            CurrencySymbol = "Rs";
        }

        IsLoading = true;
        try
        {
            var supRes = await _supplierService.GetFlatListAsync(false);
            if (supRes.Success && supRes.Data != null)
            {
                Suppliers.Clear();
                foreach (var s in supRes.Data)
                {
                    Suppliers.Add(s);
                }
            }

            if (preselectedSupplierId.HasValue)
            {
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == preselectedSupplierId.Value);
            }
            else
            {
                SelectedSupplier = Suppliers.FirstOrDefault();
            }

            if (SelectedSupplier != null)
            {
                await LoadLedgerAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSupplierChanged(SupplierDto? value)
    {
        if (value != null)
        {
            _ = LoadLedgerAsync();
        }
        else
        {
            Entries.Clear();
            OpeningBalance = 0m;
            TotalDebits = 0m;
            TotalCredits = 0m;
            ClosingBalance = 0m;
        }
    }

    [RelayCommand]
    public async Task LoadLedgerAsync()
    {
        if (SelectedSupplier == null) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var res = await _ledgerService.GetSupplierLedgerReportAsync(
                SelectedSupplier.Id,
                FromDate,
                ToDate);

            if (res.Success && res.Data != null)
            {
                OpeningBalance = res.Data.OpeningBalance;
                TotalDebits = res.Data.TotalDebits;
                TotalCredits = res.Data.TotalCredits;
                ClosingBalance = res.Data.ClosingBalance;

                Entries.Clear();
                foreach (var e in res.Data.Entries)
                {
                    Entries.Add(e);
                }
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading supplier ledger: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (SelectedSupplier == null) return;

        string safeName = string.Join("_", SelectedSupplier.Name.Split(Path.GetInvalidFileNameChars()));
        string defaultName = $"Ledger_{safeName}_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.pdf";
        string? targetPath = _dialogService.ShowSaveFileDialog("PDF Files (*.pdf)|*.pdf", defaultName, "Export Supplier Ledger PDF");
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        IsLoading = true;
        try
        {
            var res = await _ledgerService.ExportLedgerPdfAsync(SelectedSupplier.Id, FromDate, ToDate);
            if (res.Success && res.Data != null)
            {
                await File.WriteAllBytesAsync(targetPath, res.Data);
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error exporting PDF: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        if (SelectedSupplier == null) return;

        string safeName = string.Join("_", SelectedSupplier.Name.Split(Path.GetInvalidFileNameChars()));
        string defaultName = $"Ledger_{safeName}_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.csv";
        string? targetPath = _dialogService.ShowSaveFileDialog("CSV Files (*.csv)|*.csv", defaultName, "Export Supplier Ledger CSV");
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        IsLoading = true;
        try
        {
            var res = await _ledgerService.ExportLedgerCsvAsync(SelectedSupplier.Id, FromDate, ToDate);
            if (res.Success && res.Data != null)
            {
                await File.WriteAllBytesAsync(targetPath, res.Data);
            }
            else
            {
                ErrorMessage = res.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error exporting CSV: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

