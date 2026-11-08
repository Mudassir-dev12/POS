using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Inventory;

public partial class ReorderListViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly ISupplierService _supplierService;
        private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int? _selectedSupplierId;

    [ObservableProperty]
    private bool _selectAll = true;

    public ObservableCollection<ReorderItemDto> Items { get; } = new();
    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public int SelectedCount => Items.Count(i => i.IsSelected);
    public decimal SelectedTotalCost => Items.Where(i => i.IsSelected).Sum(i => i.SuggestedQuantity * i.CostPrice);

    public ReorderListViewModel(
        IInventoryService inventoryService,
        ISupplierService supplierService,
        IDialogService dialogService)
    {
        _inventoryService = inventoryService;
        _supplierService = supplierService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadSuppliersAsync();
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadSuppliersAsync()
    {
        try
        {            var res = await _supplierService.GetFlatListAsync(false);
            if (res.Success && res.Data != null)
            {
                Suppliers.Clear();
                foreach (var s in res.Data.Where(s => s.IsActive))
                {
                    Suppliers.Add(s);
                }
                if (Suppliers.Count > 0)
                {
                    SelectedSupplierId = Suppliers[0].Id;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {            var result = await _inventoryService.GetReorderListAsync();
            if (result.Success && result.Data != null)
            {
                Items.Clear();
                foreach (var item in result.Data)
                {
                    item.IsSelected = SelectAll;
                    Items.Add(item);
                }
                UpdateTotals();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to load reorder list.";
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

    partial void OnSelectAllChanged(bool value)
    {
        foreach (var item in Items)
        {
            item.IsSelected = value;
        }
        UpdateTotals();
    }

    public void UpdateTotals()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedTotalCost));
    }

    [RelayCommand]
    public async Task CreatePurchaseDraftAsync()
    {
        if (!SelectedSupplierId.HasValue || SelectedSupplierId.Value <= 0)
        {
            _dialogService.ShowMessage("Please select a supplier for the purchase draft.", "Supplier Required");
            return;
        }

        var selected = Items.Where(i => i.IsSelected && i.SuggestedQuantity > 0).ToList();
        if (selected.Count == 0)
        {
            _dialogService.ShowMessage("Please select at least one item with a suggested quantity greater than 0.", "Items Required");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation(
            $"Create a draft purchase order for {selected.Count} items totaling {ShopPOS.Core.Services.MoneyFormatter.Instance.Format(SelectedTotalCost)}?",
            "Confirm Purchase Draft");

        if (!confirm) return;

        IsLoading = true;
        try
        {
            var dto = new CreatePurchaseFromReorderDto
            {
                SupplierId = SelectedSupplierId.Value,
                Items = selected.Select(s => new ReorderPurchaseLineDto
                {
                    ProductId = s.ProductId,
                    Quantity = s.SuggestedQuantity,
                    UnitCost = s.CostPrice
                }).ToList()
            };

            var userId = 1;
            var result = await _inventoryService.CreatePurchaseDraftFromReorderAsync(dto, userId);
            if (result.Success && result.Data != null)
            {
                _dialogService.ShowMessage(
                    $"Draft Purchase Order '{result.Data.PurchaseNumber}' created successfully! You can review and post it under Purchases.",
                    "Purchase Created");
                await LoadDataAsync();
            }
            else
            {
                _dialogService.ShowMessage(result.ErrorMessage ?? "Failed to create purchase order.", "Creation Failed");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
