using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Products;

public partial class CsvImportWizardViewModel : ObservableObject
{
    private readonly ICsvProductService _csvProductService;
    private readonly IDialogService _dialogService;
    private readonly int? _currentUserId;
    private readonly string? _currentUserRole;

    private CancellationTokenSource? _importCts;

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private string _selectedFilePath = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    // Step 2: Preview
    [ObservableProperty]
    private int _previewRowCount = 0;

    [ObservableProperty]
    private DataTable? _previewDataTable;

    [ObservableProperty]
    private string _detectedFileType = string.Empty;

    [ObservableProperty]
    private string? _detectedNameColumn;

    [ObservableProperty]
    private bool _isNameColumnDetected = true;

    public ObservableCollection<string> Headers { get; } = new();
    public ObservableCollection<Dictionary<string, string>> PreviewRows { get; } = new();

    // Step 3: Validation
    [ObservableProperty]
    private int _totalValidatedRows = 0;

    [ObservableProperty]
    private int _validRowCount = 0;

    [ObservableProperty]
    private int _errorRowCount = 0;

    public ObservableCollection<CsvRowError> ValidationErrors { get; } = new();

    // Step 4: Import execution
    [ObservableProperty]
    private bool _updateExisting = true;

    [ObservableProperty]
    private int _importProgress = 0;

    [ObservableProperty]
    private bool _isImporting = false;

    [ObservableProperty]
    private bool _importCompleted = false;

    [ObservableProperty]
    private string _importSummaryText = string.Empty;

    public event Action<bool>? RequestClose;

    public CsvImportWizardViewModel(
        ICsvProductService csvProductService,
        IDialogService dialogService,
        int? currentUserId,
        string? currentUserRole)
    {
        _csvProductService = csvProductService;
        _dialogService = dialogService;
        _currentUserId = currentUserId;
        _currentUserRole = currentUserRole;
    }

    [RelayCommand]
    public void BrowseFile()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "All Supported Catalogs (*.xlsx;*.csv)|*.xlsx;*.csv|Excel Workbooks (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            "Select Products File (Excel or CSV)");

        if (!string.IsNullOrEmpty(filePath))
        {
            SelectedFilePath = filePath;
            ErrorMessage = null;
        }
    }

    [RelayCommand]
    public void BrowseExcelFile()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "Excel Workbooks (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
            "Select Products Excel File (.xlsx)");

        if (!string.IsNullOrEmpty(filePath))
        {
            SelectedFilePath = filePath;
            ErrorMessage = null;
        }
    }

    [RelayCommand]
    public void BrowseCsvFile()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            "Select Products CSV File (.csv)");

        if (!string.IsNullOrEmpty(filePath))
        {
            SelectedFilePath = filePath;
            ErrorMessage = null;
        }
    }

    [RelayCommand]
    public void DownloadTemplate()
    {
        DownloadExcelTemplate();
    }

    [RelayCommand]
    public void DownloadExcelTemplate()
    {
        var filePath = _dialogService.ShowSaveFileDialog(
            "Excel Workbook (*.xlsx)|*.xlsx",
            "ShopPOS_Products_Template.xlsx",
            "Save Sample Products Excel Template");

        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                byte[] bytes = _csvProductService.GetSampleExcelTemplate();
                File.WriteAllBytes(filePath, bytes);
                StatusMessage = $"Excel template saved to: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save Excel template: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    public void DownloadCsvTemplate()
    {
        var filePath = _dialogService.ShowSaveFileDialog(
            "CSV File (*.csv)|*.csv",
            "ShopPOS_Products_Template.csv",
            "Save Sample Products CSV Template");

        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                byte[] bytes = _csvProductService.GetSampleCsvTemplate();
                File.WriteAllBytes(filePath, bytes);
                StatusMessage = $"CSV template saved to: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save CSV template: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    public async Task NextStepAsync()
    {
        ErrorMessage = null;

        if (CurrentStep == 1)
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                ErrorMessage = "Please select a valid Excel (.xlsx) or CSV (.csv) file.";
                return;
            }

            IsLoading = true;
            try
            {
                using var stream = File.OpenRead(SelectedFilePath);
                var previewRes = await _csvProductService.PreviewCsvAsync(stream);
                if (previewRes.Success && previewRes.Data != null)
                {
                    Headers.Clear();
                    foreach (var h in previewRes.Data.Headers)
                    {
                        Headers.Add(h);
                    }

                    PreviewRows.Clear();
                    foreach (var row in previewRes.Data.PreviewRows)
                    {
                        PreviewRows.Add(row);
                    }

                    // Build DataTable for dynamic DataGrid auto-generation
                    var dt = new DataTable("Preview");
                    foreach (var h in previewRes.Data.Headers)
                    {
                        // Handle potential duplicate header names safely
                        string colName = h;
                        int idx = 1;
                        while (dt.Columns.Contains(colName))
                        {
                            colName = $"{h}_{++idx}";
                        }
                        dt.Columns.Add(colName);
                    }

                    foreach (var row in previewRes.Data.PreviewRows)
                    {
                        var dr = dt.NewRow();
                        for (int i = 0; i < previewRes.Data.Headers.Count; i++)
                        {
                            string origHeader = previewRes.Data.Headers[i];
                            string colName = dt.Columns[i].ColumnName;
                            dr[colName] = row.TryGetValue(origHeader, out var val) ? val : string.Empty;
                        }
                        dt.Rows.Add(dr);
                    }
                    PreviewDataTable = dt;

                    PreviewRowCount = previewRes.Data.TotalRowsFound;
                    DetectedFileType = previewRes.Data.FileTypeInfo;
                    DetectedNameColumn = previewRes.Data.DetectedNameColumn;
                    IsNameColumnDetected = !string.IsNullOrWhiteSpace(DetectedNameColumn);

                    CurrentStep = 2;
                }
                else
                {
                    ErrorMessage = previewRes.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to read file: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        else if (CurrentStep == 2)
        {
            // Run validation
            IsLoading = true;
            try
            {
                using var stream = File.OpenRead(SelectedFilePath);
                var reportRes = await _csvProductService.ValidateCsvAsync(stream);
                if (reportRes.Success && reportRes.Data != null)
                {
                    TotalValidatedRows = reportRes.Data.TotalRows;
                    ValidRowCount = reportRes.Data.ValidRowCount;
                    ErrorRowCount = reportRes.Data.ErrorRowCount;

                    ValidationErrors.Clear();
                    foreach (var err in reportRes.Data.Errors)
                    {
                        ValidationErrors.Add(err);
                    }

                    CurrentStep = 3;
                }
                else
                {
                    ErrorMessage = reportRes.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Validation failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        else if (CurrentStep == 3)
        {
            // Move to step 4 confirmation and execution
            CurrentStep = 4;
        }
    }

    [RelayCommand]
    public void PreviousStep()
    {
        ErrorMessage = null;
        if (CurrentStep > 1 && !IsImporting)
        {
            CurrentStep--;
        }
    }

    [RelayCommand]
    public async Task StartImportAsync()
    {
        if (IsImporting) return;

        IsImporting = true;
        ImportProgress = 0;
        ErrorMessage = null;
        _importCts = new CancellationTokenSource();

        try
        {
            var options = new CsvImportOptions
            {
                UpdateExisting = UpdateExisting,
                UserId = _currentUserId,
                UserRole = _currentUserRole
            };

            var progressHandler = new Progress<int>(pct =>
            {
                ImportProgress = pct;
            });

            using var stream = File.OpenRead(SelectedFilePath);
            var result = await _csvProductService.ImportCsvAsync(stream, options, progressHandler, _importCts.Token);

            if (result.Success && result.Data != null)
            {
                ImportCompleted = true;
                ImportSummaryText = $"Import finished: {result.Data.InsertedCount} inserted, {result.Data.UpdatedCount} updated, {result.Data.ErrorCount} skipped/failed out of {result.Data.ProcessedCount} total rows.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Import was cancelled by the user.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    public void CancelImport()
    {
        _importCts?.Cancel();
    }

    [RelayCommand]
    public void CloseDialog(object? param = null)
    {
        bool success = param is bool b ? b : (param is string s && bool.TryParse(s, out var sb) && sb);
        RequestClose?.Invoke(success || ImportCompleted);
    }

    [RelayCommand]
    public void Close(bool success)
    {
        RequestClose?.Invoke(success);
    }
}
