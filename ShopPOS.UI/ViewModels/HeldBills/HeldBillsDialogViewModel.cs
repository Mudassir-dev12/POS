using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.HeldBills;

public partial class HeldBillItemViewModel : ObservableObject
{
    public SaleDetailDto Sale { get; }

    public int Id => Sale.Id;
    public string? HoldNumber => Sale.HoldNumber;
    public DateTime Date => Sale.SaleDate;
    public string CustomerName => Sale.CustomerName ?? "Walk-in";
    public int ItemCount => Sale.Items.Count;
    public decimal TotalAmount => Sale.GrandTotal;
    public string? Notes => Sale.Notes;

    public string AgeText
    {
        get
        {
            var diff = DateTime.Now - Sale.SaleDate;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }
    }

    public HeldBillItemViewModel(SaleDetailDto sale)
    {
        Sale = sale;
    }
}

public partial class HeldBillsDialogViewModel : ObservableObject
{
    private readonly ISaleService _saleService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private HeldBillItemViewModel? _selectedItem;

    public ObservableCollection<HeldBillItemViewModel> Bills { get; } = new();

    public SaleDetailDto? ResumedSale { get; private set; }
    public bool Resumed { get; private set; }

    public event Action? RequestClose;

    public HeldBillsDialogViewModel(ISaleService saleService, IDialogService dialogService)
    {
        _saleService = saleService;
        _dialogService = dialogService;
    }

    public async Task InitializeAsync()
    {
        await LoadBillsAsync();
    }

    [RelayCommand]
    public async Task LoadBillsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Bills.Clear();
            var result = await _saleService.ListHeldBillsAsync();
            if (result.Success && result.Data != null)
            {
                foreach (var b in result.Data.OrderByDescending(x => x.SaleDate))
                {
                    Bills.Add(new HeldBillItemViewModel(b));
                }
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to load held bills.";
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
    public void Resume()
    {
        if (SelectedItem == null)
        {
            _dialogService.ShowMessage("Please select a held bill to resume.", "Resume Bill");
            return;
        }

        ResumedSale = SelectedItem.Sale;
        Resumed = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedItem == null) return;

        bool confirm = _dialogService.ShowConfirmation($"Are you sure you want to delete held bill '{SelectedItem.HoldNumber}'?", "Delete Held Bill");
        if (!confirm) return;

        var result = await _saleService.DeleteHeldBillAsync(SelectedItem.Id);
        if (result.Success)
        {
            Bills.Remove(SelectedItem);
            SelectedItem = null;
        }
        else
        {
            _dialogService.ShowMessage(result.Error ?? "Failed to delete held bill.", "Error");
        }
    }

    [RelayCommand]
    public void Close()
    {
        Resumed = false;
        RequestClose?.Invoke();
    }
}

