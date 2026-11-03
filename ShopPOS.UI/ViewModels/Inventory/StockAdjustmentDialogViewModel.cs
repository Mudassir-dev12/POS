using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Inventory;

public partial class StockAdjustmentItemLineViewModel : ObservableObject
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private decimal _systemQuantity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Difference))]
    [NotifyPropertyChangedFor(nameof(LineValueDifference))]
    private decimal _newQuantity;

    public decimal Difference => NewQuantity - SystemQuantity;
    public decimal LineValueDifference => Math.Round(Difference * UnitCost, 2, MidpointRounding.AwayFromZero);

    [ObservableProperty]
    private string? _note;
}

public partial class StockAdjustmentDialogViewModel : ObservableObject
{
    private readonly IStockAdjustmentService _adjustmentService;
    private readonly IProductService _productService;
    private readonly IStockLedgerQuery _stockLedgerQuery;
        private readonly IDialogService _dialogService;

    public int? AdjustmentId { get; }

    [ObservableProperty]
    private string _adjustmentNumber = "(Auto)";

    [ObservableProperty]
    private DateTime _adjustmentDate = DateTime.Today;

    [ObservableProperty]
    private StockAdjustmentReason _selectedReason = StockAdjustmentReason.CountCorrection;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private StockAdjustmentStatus _status = StockAdjustmentStatus.Draft;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _scanInput;

    [ObservableProperty]
    private StockAdjustmentItemLineViewModel? _selectedLine;

    public ObservableCollection<StockAdjustmentItemLineViewModel> Items { get; } = new();

    public decimal TotalValueDifference => Items.Sum(i => i.LineValueDifference);

    public event Action<bool>? RequestClose;

    public StockAdjustmentDialogViewModel(
        int? adjustmentId,
        IStockAdjustmentService adjustmentService,
        IProductService productService,
        IStockLedgerQuery stockLedgerQuery,
        IDialogService dialogService)
    {
        AdjustmentId = adjustmentId;
        _adjustmentService = adjustmentService;
        _productService = productService;
        _stockLedgerQuery = stockLedgerQuery;
        _dialogService = dialogService;
    }

    public async Task InitializeAsync()
    {
        if (AdjustmentId.HasValue && AdjustmentId.Value > 0)
        {
            var userRole = "Owner";
            var result = await _adjustmentService.GetAdjustmentByIdAsync(AdjustmentId.Value, userRole);
            if (result.Success && result.Data != null)
            {
                var d = result.Data;
                AdjustmentNumber = d.AdjustmentNumber;
                AdjustmentDate = d.AdjustmentDate;
                SelectedReason = d.Reason;
                Notes = d.Notes;
                Status = d.Status;
                IsReadOnly = d.Status != StockAdjustmentStatus.Draft;

                Items.Clear();
                foreach (var item in d.Items)
                {
                    var line = new StockAdjustmentItemLineViewModel
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Sku = item.Sku,
                        UnitCost = item.UnitCost,
                        SystemQuantity = item.SystemQuantity,
                        NewQuantity = item.NewQuantity,
                        Note = item.Note
                    };
                    line.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TotalValueDifference));
                    Items.Add(line);
                }
                SelectedLine = Items.FirstOrDefault();
            }
        }
    }

    [RelayCommand]
    public async Task ProcessScanInputAsync()
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(ScanInput)) return;

        string code = ScanInput.Trim();
        ScanInput = string.Empty;

        var searchRes = await _productService.GetPagedProductsAsync(new Core.DTOs.ProductSearchQuery
        {
            SearchTerm = code,
            IsActive = true,
            PageNumber = 1,
            PageSize = 10
        });

        if (!searchRes.Success || searchRes.Data == null || searchRes.Data.Items.Count == 0)
        {
            ErrorMessage = $"Product not found for scan '{code}'.";
            try { System.Media.SystemSounds.Beep.Play(); } catch { }
            return;
        }

        var match = searchRes.Data.Items.FirstOrDefault(p =>
            string.Equals(p.Barcode, code, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Sku, code, StringComparison.OrdinalIgnoreCase))
            ?? searchRes.Data.Items.First();

        var existing = Items.FirstOrDefault(i => i.ProductId == match.Id);
        if (existing != null)
        {
            existing.NewQuantity += 1m;
            SelectedLine = existing;
        }
        else
        {
            decimal currentStock = await _stockLedgerQuery.GetCurrentStockAsync(match.Id);
            var line = new StockAdjustmentItemLineViewModel
            {
                ProductId = match.Id,
                ProductName = match.Name,
                Sku = match.Sku,
                UnitCost = match.CostPrice,
                SystemQuantity = currentStock,
                NewQuantity = currentStock + 1m,
                Note = string.Empty
            };
            line.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TotalValueDifference));
            Items.Add(line);
            SelectedLine = line;
        }

        ErrorMessage = null;
        OnPropertyChanged(nameof(TotalValueDifference));
    }

    [RelayCommand]
    public async Task AddProductLineAsync()
    {
        if (IsReadOnly) return;

        var vm = new Purchases.ProductSearchDialogViewModel(_productService);
        vm.SetExistingProductIds(Items.Select(i => i.ProductId));
        var selectedProducts = _dialogService.ShowMultiProductSearchDialog(vm);
        if (selectedProducts != null && selectedProducts.Count > 0)
        {
            StockAdjustmentItemLineViewModel? lastAdded = null;
            foreach (var product in selectedProducts)
            {
                if (Items.Any(i => i.ProductId == product.Id))
                    continue;

                decimal stock = await _stockLedgerQuery.GetCurrentStockAsync(product.Id);
                var line = new StockAdjustmentItemLineViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    UnitCost = product.CostPrice,
                    SystemQuantity = stock,
                    NewQuantity = stock,
                    Note = string.Empty
                };
                line.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TotalValueDifference));
                Items.Add(line);
                lastAdded = line;
            }

            if (lastAdded != null)
            {
                SelectedLine = lastAdded;
            }
            OnPropertyChanged(nameof(TotalValueDifference));
        }
    }

    [RelayCommand]
    public void RemoveLineItem(StockAdjustmentItemLineViewModel? line)
    {
        if (IsReadOnly) return;
        var target = line ?? SelectedLine;
        if (target != null)
        {
            Items.Remove(target);
            if (SelectedLine == target)
            {
                SelectedLine = Items.FirstOrDefault();
            }
            OnPropertyChanged(nameof(TotalValueDifference));
        }
    }

    [RelayCommand]
    public void RemoveSelectedLines()
    {
        if (IsReadOnly) return;
        var toRemove = Items.Where(i => i.IsSelected).ToList();
        if (toRemove.Count == 0 && SelectedLine != null)
        {
            toRemove.Add(SelectedLine);
        }
        foreach (var line in toRemove)
        {
            Items.Remove(line);
        }
        SelectedLine = Items.FirstOrDefault();
        OnPropertyChanged(nameof(TotalValueDifference));
    }

    [RelayCommand]
    public void RemoveLine()
    {
        RemoveLineItem(SelectedLine);
    }

    [RelayCommand]
    public async Task SaveDraftAsync()
    {
        if (IsReadOnly) return;
        await SaveInternalAsync(andPost: false);
    }

    [RelayCommand]
    public async Task SaveAndPostAsync()
    {
        if (IsReadOnly) return;
        await SaveInternalAsync(andPost: true);
    }

    private async Task SaveInternalAsync(bool andPost)
    {
        ErrorMessage = null;

        if (Items.Count == 0)
        {
            ErrorMessage = "At least one product line is required.";
            return;
        }

        IsSaving = true;
        try
        {
            var userId = 1;
            var userRole = "Owner";

            int savedAdjustmentId;

            if (AdjustmentId.HasValue && AdjustmentId.Value > 0)
            {
                var updateDto = new UpdateStockAdjustmentDto
                {
                    Id = AdjustmentId.Value,
                    AdjustmentDate = AdjustmentDate,
                    Reason = SelectedReason,
                    Notes = Notes?.Trim(),
                    Items = Items.Select(i => new CreateStockAdjustmentItemDto
                    {
                        ProductId = i.ProductId,
                        NewQuantity = i.NewQuantity,
                        Note = i.Note?.Trim()
                    }).ToList()
                };

                var updateResult = await _adjustmentService.UpdateAdjustmentDraftAsync(updateDto, userId, userRole);
                if (!updateResult.Success)
                {
                    ErrorMessage = updateResult.ErrorMessage ?? "Failed to update adjustment.";
                    return;
                }
                savedAdjustmentId = updateResult.Data!.Id;
            }
            else
            {
                var createDto = new CreateStockAdjustmentDto
                {
                    AdjustmentDate = AdjustmentDate,
                    Reason = SelectedReason,
                    Notes = Notes?.Trim(),
                    Items = Items.Select(i => new CreateStockAdjustmentItemDto
                    {
                        ProductId = i.ProductId,
                        NewQuantity = i.NewQuantity,
                        Note = i.Note?.Trim()
                    }).ToList()
                };

                var createResult = await _adjustmentService.CreateAdjustmentDraftAsync(createDto, userId, userRole);
                if (!createResult.Success)
                {
                    ErrorMessage = createResult.ErrorMessage ?? "Failed to create adjustment.";
                    return;
                }
                savedAdjustmentId = createResult.Data!.Id;
            }

            if (andPost)
            {
                var postResult = await _adjustmentService.PostAdjustmentAsync(savedAdjustmentId, userId, userRole);
                if (!postResult.Success)
                {
                    ErrorMessage = $"Adjustment saved as Draft, but posting failed: {postResult.ErrorMessage}";
                    return;
                }
            }

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
