using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs.DataTools;

namespace ShopPOS.UI.ViewModels.Dialogs;

public partial class DataToolResultDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Data Tool Result";

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string? _backupFilePath;

    [ObservableProperty]
    private bool _isDemoData;

    [ObservableProperty]
    private int _categoriesCreated;

    [ObservableProperty]
    private int _brandsCreated;

    [ObservableProperty]
    private int _suppliersCreated;

    [ObservableProperty]
    private int _productsCreated;

    [ObservableProperty]
    private int _purchasesCreated;

    [ObservableProperty]
    private int _salesCreated;

    [ObservableProperty]
    private int _returnsCreated;

    [ObservableProperty]
    private int _expensesCreated;

    public ObservableCollection<DemoBarcodeItemDto> SampleBarcodes { get; } = new();
    public ObservableCollection<TableCountDto> DeletedRows { get; } = new();

    public event Action? RequestClose;
    public event Action<string>? RequestCopyToClipboard;

    [RelayCommand]
    private void CopyBackupPath()
    {
        if (!string.IsNullOrWhiteSpace(BackupFilePath))
        {
            RequestCopyToClipboard?.Invoke(BackupFilePath);
        }
    }

    [RelayCommand]
    private void CopyAllBarcodes()
    {
        if (SampleBarcodes.Count == 0) return;
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("Barcode\tProduct\tSKU\tPrice");
        foreach (var b in SampleBarcodes)
        {
            lines.AppendLine($"{b.Barcode}\t{b.ProductName}\t{b.Sku}\tRs {b.SalePrice:N2}");
        }
        RequestCopyToClipboard?.Invoke(lines.ToString());
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
