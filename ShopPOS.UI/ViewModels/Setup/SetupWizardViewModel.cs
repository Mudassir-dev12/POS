using ShopPOS.Core.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Data;
using ShopPOS.Data.Seeding;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.ViewModels.Setup;

public enum SetupWelcomeChoice
{
    NewShop,
    RestoreBackup
}

public enum StartingDataChoice
{
    StartEmpty,
    LoadDemoData
}

/// <summary>
/// Orchestrates the 7-step First-Run Setup Wizard.
/// Guarantees that nothing is committed or written to the database until Finish is clicked.
/// </summary>
public partial class SetupWizardViewModel : ObservableObject
{
    private readonly IAppPaths _appPaths;
    private readonly IUserSettingsService _userSettingsService;
    private readonly IDialogService _dialogService;
    private readonly IRestoreService? _restoreService;
    private readonly IBackupUploader? _backupUploader;
    private readonly IGitHubBackupUploader? _gitHubUploader;
    private readonly IReceiptPrinterService? _printerService;
    private readonly Func<AppDbContext>? _dbContextFactory;
    private readonly IDataSeeder? _seeder;
    private readonly IDataToolsService? _dataToolsService;
    private readonly IBackupService? _backupService;
    private readonly IServiceScopeFactory? _scopeFactory;

    // Navigation state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    [NotifyPropertyChangedFor(nameof(StepDescription))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanFinish))]
    [NotifyPropertyChangedFor(nameof(CurrentStepDisplay))]
    [NotifyPropertyChangedFor(nameof(IsWelcomeStep))]
    [NotifyPropertyChangedFor(nameof(IsProfileStep))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyStep))]
    [NotifyPropertyChangedFor(nameof(IsPrinterStep))]
    [NotifyPropertyChangedFor(nameof(IsDataFolderStep))]
    [NotifyPropertyChangedFor(nameof(IsBackupStep))]
    [NotifyPropertyChangedFor(nameof(IsStartingDataStep))]
    private int _currentStepIndex;

    public int TotalSteps => 7;
    public int CurrentStepDisplay => CurrentStepIndex + 1;

    public bool IsWelcomeStep => CurrentStepIndex == 0;
    public bool IsProfileStep => CurrentStepIndex == 1;
    public bool IsCurrencyStep => CurrentStepIndex == 2;
    public bool IsPrinterStep => CurrentStepIndex == 3;
    public bool IsDataFolderStep => CurrentStepIndex == 4;
    public bool IsBackupStep => CurrentStepIndex == 5;
    public bool IsStartingDataStep => CurrentStepIndex == 6;

    public bool CanGoBack => CurrentStepIndex > 0 && !IsBusy;
    public bool CanGoNext => CurrentStepIndex < TotalSteps - 1 && !IsBusy;
    public bool CanFinish => CurrentStepIndex == TotalSteps - 1 && !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanFinish))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    private string? _errorMessage;

    // STEP 1: Welcome
    [ObservableProperty]
    private SetupWelcomeChoice _welcomeChoice = SetupWelcomeChoice.NewShop;

    [ObservableProperty]
    private string? _selectedBackupFilePath;

    [ObservableProperty]
    private string? _restorePassword;

    // STEP 2: Shop Profile
    [ObservableProperty]
    private string _shopName = "Shop POS";

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _taxNumber;

    // STEP 3: Currency & Tax
    [ObservableProperty]
    private string _currencySymbol = "Rs";

    [ObservableProperty]
    private int _currencyDecimals = 2;

    public ObservableCollection<int> AllowedDecimals { get; } = new() { 0, 2 };

    [ObservableProperty]
    private decimal _defaultTaxPercent;

    [ObservableProperty]
    private bool _pricesIncludeTax;

    [ObservableProperty]
    private decimal _cashRoundingStep;

    public ObservableCollection<decimal> AllowedRoundingSteps { get; } = new() { 0m, 0.05m, 0.10m, 0.50m, 1.0m, 5.0m };

    // STEP 4: Receipt Printer
    [ObservableProperty]
    private PrinterMode _printerMode = PrinterMode.PdfPreview;

    public ObservableCollection<PrinterMode> AvailablePrinterModes { get; } = new()
    {
        PrinterMode.PdfPreview,
        PrinterMode.WindowsDriver,
        PrinterMode.EscPosRaw
    };

    [ObservableProperty]
    private string? _selectedPrinter;

    public ObservableCollection<string> InstalledPrinters { get; } = new();

    [ObservableProperty]
    private int _receiptWidthMm = 80;

    public ObservableCollection<int> AllowedWidths { get; } = new() { 58, 80 };

    [ObservableProperty]
    private string? _printerTestMessage;

    // STEP 5: Data Folder
    [ObservableProperty]
    private string _dataDirectory;

    partial void OnDataDirectoryChanged(string value) => ValidateDataFolder();

    [ObservableProperty]
    private string? _dataFolderStatusText;

    [ObservableProperty]
    private bool _isDataFolderValid = true;

    // STEP 6: Backups
    [ObservableProperty]
    private bool _backupEnabled = true;

    [ObservableProperty]
    private string _backupTime = "22:00";

    [ObservableProperty]
    private string? _secondFolderPath;

    [ObservableProperty]
    private bool _encryptBackups;

    [ObservableProperty]
    private string? _encryptionPassword;

    [ObservableProperty]
    private string? _confirmEncryptionPassword;

    [ObservableProperty]
    private bool _isDriveConnected;

    [ObservableProperty]
    private string? _driveAccountEmail;

    // STEP 7: Starting Data
    [ObservableProperty]
    private StartingDataChoice _startingData = StartingDataChoice.StartEmpty;

    // Results
    public bool CompletedSuccessfully { get; private set; }
    public bool WasRestored { get; private set; }
    public event Action? RequestClose;

    public string StepTitle => CurrentStepIndex switch
    {
        0 => "Welcome to ShopPOS",
        1 => "Shop Profile",
        2 => "Currency & Tax",
        3 => "Receipt Printer Setup",
        4 => "Database & Storage Location",
        5 => "Automated Backups & Protection",
        6 => "Initial Catalog & Store Data",
        _ => "Setup Wizard"
    };

    public string StepDescription => CurrentStepIndex switch
    {
        0 => "Choose whether to initialize a fresh shop or restore an existing backup.",
        1 => "Enter basic business identification details printed on customer receipts.",
        2 => "Set the primary currency symbol, decimal precision, and sales tax rules.",
        3 => "Configure receipt printer hardware or select PDF print preview.",
        4 => "Choose where the offline database and product images will be stored.",
        5 => "Configure daily automated backups, secondary USB disk mirroring, and encryption.",
        6 => "Choose whether to begin with an empty store or pre-load sample demo products.",
        _ => string.Empty
    };

    public SetupWizardViewModel(
        IAppPaths appPaths,
        IUserSettingsService userSettingsService,
        IDialogService dialogService,
        IRestoreService? restoreService = null,
        IBackupUploader? backupUploader = null,
        IReceiptPrinterService? printerService = null,
        Func<AppDbContext>? dbContextFactory = null,
        IDataSeeder? seeder = null,
        IDataToolsService? dataToolsService = null,
        IBackupService? backupService = null,
        IServiceScopeFactory? scopeFactory = null,
        IGitHubBackupUploader? gitHubUploader = null)
    {
        _appPaths = appPaths;
        _userSettingsService = userSettingsService;
        _dialogService = dialogService;
        _restoreService = restoreService;
        _backupUploader = backupUploader;
        _gitHubUploader = gitHubUploader ?? (backupUploader as IGitHubBackupUploader);
        _printerService = printerService;
        _dbContextFactory = dbContextFactory;
        _seeder = seeder;
        _dataToolsService = dataToolsService;
        _backupService = backupService;
        _scopeFactory = scopeFactory;

        _dataDirectory = _appPaths.DataDirectory;
        LoadInstalledPrinters();
        ValidateDataFolder();
    }

    private void LoadInstalledPrinters()
    {
        InstalledPrinters.Clear();
        try
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                InstalledPrinters.Add(printer);
            }

            var defaultSettings = new PrinterSettings();
            SelectedPrinter = InstalledPrinters.FirstOrDefault(p => string.Equals(p, defaultSettings.PrinterName, StringComparison.OrdinalIgnoreCase))
                ?? InstalledPrinters.FirstOrDefault();
        }
        catch { }
    }

    [RelayCommand]
    public void Next()
    {
        ErrorMessage = null;
        if (!ValidateStep(CurrentStepIndex)) return;

        if (CurrentStepIndex < TotalSteps - 1)
        {
            CurrentStepIndex++;
        }
    }

    [RelayCommand]
    public void Back()
    {
        ErrorMessage = null;
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
        }
    }

    private bool ValidateStep(int stepIndex)
    {
        switch (stepIndex)
        {
            case 1: // Profile
                if (string.IsNullOrWhiteSpace(ShopName))
                {
                    ErrorMessage = "Store name is required.";
                    return false;
                }
                break;

            case 2: // Currency
                if (string.IsNullOrWhiteSpace(CurrencySymbol))
                {
                    ErrorMessage = "Currency symbol is required (e.g. Rs).";
                    return false;
                }
                break;

            case 4: // Data folder
                if (!ValidateDataFolder())
                {
                    ErrorMessage = DataFolderStatusText ?? "The selected data directory is not valid or not writable.";
                    return false;
                }
                break;

            case 5: // Backups
                break;
        }

        return true;
    }

    [RelayCommand]
    public void BrowseDataFolder()
    {
        string? folder = _dialogService.ShowFolderBrowserDialog(
            "Select ShopPOS Data Folder",
            DataDirectory);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            DataDirectory = Path.GetFullPath(folder);
            ValidateDataFolder();
        }
    }

    [RelayCommand]
    public void BrowseSecondFolder()
    {
        string? folder = _dialogService.ShowFolderBrowserDialog(
            "Select Secondary Backup Folder (e.g. USB Drive)",
            SecondFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyComputer));

        if (!string.IsNullOrWhiteSpace(folder))
        {
            SecondFolderPath = Path.GetFullPath(folder);
        }
    }

    private bool ValidateDataFolder()
    {
        if (string.IsNullOrWhiteSpace(DataDirectory))
        {
            IsDataFolderValid = false;
            DataFolderStatusText = "Folder path cannot be empty.";
            return false;
        }

        if (!_userSettingsService.IsDirectoryWritable(DataDirectory, out string? writeErr))
        {
            IsDataFolderValid = false;
            DataFolderStatusText = $"Folder is not writable: {writeErr}";
            return false;
        }

        long freeBytes = _userSettingsService.GetAvailableFreeSpaceBytes(DataDirectory);
        double freeGb = freeBytes / (1024.0 * 1024.0 * 1024.0);

        if (freeBytes < 500L * 1024 * 1024)
        {
            IsDataFolderValid = false;
            DataFolderStatusText = $"Low disk space: {freeGb:F2} GB free (minimum 500 MB required).";
            return false;
        }

        IsDataFolderValid = true;
        DataFolderStatusText = $"Folder is writable and ready ({freeGb:F1} GB free space available).";
        return true;
    }

    [RelayCommand]
    public async Task TestPrintAsync()
    {
        PrinterTestMessage = "Sending test receipt...";
        try
        {
            using var scope = _scopeFactory?.CreateScope();
            var printerService = scope?.ServiceProvider.GetService<IReceiptPrinterService>() ?? _printerService;

            if (printerService != null)
            {
                var testDoc = new ReceiptDocument
                {
                    DocumentType = ReceiptDocumentType.TestSlip,
                    DocumentTitle = "TEST RECEIPT",
                    ReceiptNumber = "TEST-0001",
                    IssuedAt = DateTime.Now,
                    ShopName = ShopName,
                    ShopAddress = Address,
                    ShopPhone = Phone,
                    Lines = new List<ReceiptLineItem>
                    {
                        new() { ProductName = "Test Item 1", Quantity = 1, UnitPrice = 100m, LineTotal = 100m }
                    },
                    GrandTotal = 100m,
                    PaidAmount = 100m
                };

                var tempSettings = new ShopSetting
                {
                    ShopName = ShopName,
                    PrinterMode = PrinterMode,
                    ReceiptPrinterName = SelectedPrinter,
                    ReceiptWidthMm = ReceiptWidthMm
                };

                await printerService.PrintReceiptAsync(testDoc, tempSettings);
                PrinterTestMessage = "Test receipt generated successfully!";
            }
            else
            {
                PrinterTestMessage = "Receipt printer service not available.";
            }
        }
        catch (Exception ex)
        {
            PrinterTestMessage = $"Test print failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void BrowseBackupFile()
    {
        string? file = _dialogService.ShowOpenFileDialog(
            "Select Backup File",
            "ShopPOS Backup (*.zip;*.zip.enc)|*.zip;*.zip.enc|All files (*.*)|*.*");

        if (!string.IsNullOrWhiteSpace(file))
        {
            SelectedBackupFilePath = file;
        }
    }

    [RelayCommand]
    public async Task RestoreFromFileAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBackupFilePath) || !File.Exists(SelectedBackupFilePath))
        {
            _dialogService.ShowMessage("Please select a valid backup archive file first.", "File Required");
            return;
        }

        if (_restoreService == null)
        {
            _dialogService.ShowMessage("Restore service is not initialized.", "Error");
            return;
        }

        IsBusy = true;
        BusyMessage = "Validating and restoring backup archive...";
        ErrorMessage = null;

        try
        {
            var val = await _restoreService.ValidateBackupForRestoreAsync(SelectedBackupFilePath, RestorePassword);
            if (!val.IsValid)
            {
                ErrorMessage = val.ErrorMessage ?? "The selected backup file is not valid.";
                _dialogService.ShowMessage(ErrorMessage, "Validation Failed");
                return;
            }

            var result = await _restoreService.RestoreBackupAsync(SelectedBackupFilePath, RestorePassword);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to restore backup archive.";
                _dialogService.ShowMessage(ErrorMessage, "Restore Failed");
                return;
            }

            WasRestored = true;
            CompletedSuccessfully = true;
            RequestClose?.Invoke();
            _dialogService.ShowMessage("Database restored successfully! Opening ShopPOS...", "Restore Complete");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Restore failed: {ex.Message}";
            _dialogService.ShowMessage(ErrorMessage, "Restore Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RestoreFromGitHubAsync()
    {
        if (_gitHubUploader == null || _restoreService == null)
        {
            _dialogService.ShowMessage("GitHub restore service is not available.", "Error");
            return;
        }

        var vm = new GitHubRestoreDialogViewModel(_gitHubUploader, _dialogService, _restoreService);
        await vm.InitializeAsync();

        bool? restored = _dialogService.ShowGitHubRestoreDialog(vm);
        if (restored == true)
        {
            WasRestored = true;
            CompletedSuccessfully = true;
            RequestClose?.Invoke();
        }
    }

    [RelayCommand]
    public async Task FinishAsync()
    {
        if (IsBusy) return;
        if (!ValidateStep(CurrentStepIndex)) return;

        IsBusy = true;
        BusyMessage = "Setting up your store database and default settings...";
        ErrorMessage = null;

        try
        {
            // 1. Save custom data directory if changed
            if (!string.Equals(DataDirectory, _appPaths.DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _userSettingsService.SaveDataDirectory(DataDirectory);
            }

            // Ensure directories exist
            _appPaths.EnsureDirectoriesExist();

            // 2. Initialize and migrate database using a dedicated scope
            if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;
                var dbContext = sp.GetRequiredService<AppDbContext>();

                await dbContext.Database.MigrateAsync();

                // 3. Seed defaults
                var seeder = sp.GetService<IDataSeeder>() ?? _seeder;
                if (seeder != null)
                {
                    await seeder.SeedAsync();
                }

                // 4. Update ShopSetting with wizard choices
                var setting = await dbContext.ShopSettings.FirstOrDefaultAsync();
                if (setting != null)
                {
                    setting.ShopName = ShopName.Trim();
                    setting.Address = Address?.Trim();
                    setting.Phone = Phone?.Trim();
                    setting.TaxNumber = TaxNumber?.Trim();
                    setting.CurrencySymbol = CurrencySymbol.Trim();
                    setting.CurrencyDecimals = CurrencyDecimals;
                    setting.DefaultTaxPercent = DefaultTaxPercent;
                    setting.PricesIncludeTax = PricesIncludeTax;
                    setting.CashRoundingStep = CashRoundingStep;
                    setting.PrinterMode = PrinterMode;
                    setting.ReceiptPrinterName = SelectedPrinter;
                    setting.ReceiptWidthMm = ReceiptWidthMm;
                    setting.BackupEnabled = BackupEnabled;
                    setting.BackupTime = BackupTime;
                    setting.SecondFolderPath = SecondFolderPath;

                    await dbContext.SaveChangesAsync();
                }

                // 5. Backups configured without encryption password

                // 6. Seed demo data if selected
                if (StartingData == StartingDataChoice.LoadDemoData)
                {
                    BusyMessage = "Loading demo products, inventory, sales, and purchases...";
                    var dataToolsService = sp.GetService<IDataToolsService>() ?? _dataToolsService;
                    if (dataToolsService != null)
                    { 
                        await dataToolsService.LoadDemoDataAsync();
                    }
                }
            }
            else if (_dbContextFactory != null)
            {
                var dbContext = _dbContextFactory();
                await dbContext.Database.MigrateAsync();

                // 3. Seed defaults
                if (_seeder != null)
                {
                    await _seeder.SeedAsync();
                }

                // 4. Update ShopSetting with wizard choices
                var setting = await dbContext.ShopSettings.FirstOrDefaultAsync();
                if (setting != null)
                {
                    setting.ShopName = ShopName.Trim();
                    setting.Address = Address?.Trim();
                    setting.Phone = Phone?.Trim();
                    setting.TaxNumber = TaxNumber?.Trim();
                    setting.CurrencySymbol = CurrencySymbol.Trim();
                    setting.CurrencyDecimals = CurrencyDecimals;
                    setting.DefaultTaxPercent = DefaultTaxPercent;
                    setting.PricesIncludeTax = PricesIncludeTax;
                    setting.CashRoundingStep = CashRoundingStep;
                    setting.PrinterMode = PrinterMode;
                    setting.ReceiptPrinterName = SelectedPrinter;
                    setting.ReceiptWidthMm = ReceiptWidthMm;
                    setting.BackupEnabled = BackupEnabled;
                    setting.BackupTime = BackupTime;
                    setting.SecondFolderPath = SecondFolderPath;

                    await dbContext.SaveChangesAsync();
                }

                // 5. Backups configured without encryption password

                // 6. Seed demo data if selected
                if (StartingData == StartingDataChoice.LoadDemoData && _dataToolsService != null)
                {
                    BusyMessage = "Loading demo products, inventory, sales, and purchases...";
                    await _dataToolsService.LoadDemoDataAsync();
                }
            }

            CompletedSuccessfully = true;
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Initialization failed: {ex.Message}";
            _dialogService.ShowMessage(ErrorMessage, "Setup Error");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
