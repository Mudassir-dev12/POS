using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Inventory;

public partial class StockAdjustmentsViewModel : ObservableObject
{
    private readonly IStockAdjustmentService _adjustmentService;
    private readonly IProductService _productService;
    private readonly IStockLedgerQuery _stockLedgerQuery;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private StockAdjustmentStatus? _selectedStatus;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private StockAdjustmentListDto? _selectedAdjustment;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string _rowCountText = "Showing 0 adjustments";

    public ObservableCollection<StockAdjustmentListDto> Items { get; } = new();

    public StockAdjustmentsViewModel(
        IStockAdjustmentService adjustmentService,
        IProductService productService,
        IStockLedgerQuery stockLedgerQuery,
        IAuthService authService,
        IDialogService dialogService)
    {
        _adjustmentService = adjustmentService;
        _productService = productService;
        _stockLedgerQuery = stockLedgerQuery;
        _authService = authService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var userRole = _authService.CurrentUser?.Role?.Name;
            var result = await _adjustmentService.GetAdjustmentsAsync(FromDate, ToDate, SelectedStatus, CurrentPage, PageSize, userRole);
            if (result.Success && result.Data != null)
            {
                Items.Clear();
                foreach (var adj in result.Data.Items)
                {
                    Items.Add(adj);
                }

                TotalCount = result.Data.TotalCount;
                TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
                int start = TotalCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
                int end = Math.Min(CurrentPage * PageSize, TotalCount);
                RowCountText = $"Showing {start}-{end} of {TotalCount} adjustments";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to load stock adjustments.";
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
    public async Task NewAdjustmentAsync()
    {
        var vm = new StockAdjustmentDialogViewModel(null, _adjustmentService, _productService, _stockLedgerQuery, _authService, _dialogService);
        await vm.InitializeAsync();
        bool? res = _dialogService.ShowStockAdjustmentDialog(vm);
        if (res == true)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public async Task OpenAdjustmentAsync()
    {
        if (SelectedAdjustment == null) return;

        var vm = new StockAdjustmentDialogViewModel(SelectedAdjustment.Id, _adjustmentService, _productService, _stockLedgerQuery, _authService, _dialogService);
        await vm.InitializeAsync();
        bool? res = _dialogService.ShowStockAdjustmentDialog(vm);
        if (res == true)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public async Task PostAdjustmentAsync()
    {
        if (SelectedAdjustment == null) return;
        if (SelectedAdjustment.Status != StockAdjustmentStatus.Draft)
        {
            _dialogService.ShowMessage("Only Draft adjustments can be posted.", "Notice");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation($"Post stock adjustment '{SelectedAdjustment.AdjustmentNumber}'? Inventory will be updated.", "Confirm Post");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            var userId = _authService.CurrentUser?.Id;
            var userRole = _authService.CurrentUser?.Role?.Name;

            var result = await _adjustmentService.PostAdjustmentAsync(SelectedAdjustment.Id, userId, userRole);
            if (result.Success)
            {
                _dialogService.ShowMessage($"Adjustment '{SelectedAdjustment.AdjustmentNumber}' has been posted.", "Success");
                await LoadDataAsync();
            }
            else
            {
                _dialogService.ShowMessage(result.ErrorMessage ?? "Failed to post adjustment.", "Post Failed");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CancelAdjustmentAsync()
    {
        if (SelectedAdjustment == null) return;
        if (SelectedAdjustment.Status != StockAdjustmentStatus.Posted)
        {
            _dialogService.ShowMessage("Only Posted adjustments can be cancelled.", "Notice");
            return;
        }

        var reason = _dialogService.ShowReasonPromptDialog("Cancel Stock Adjustment", $"Enter cancellation reason for adjustment {SelectedAdjustment.AdjustmentNumber}:");
        if (string.IsNullOrWhiteSpace(reason)) return;

        IsLoading = true;
        try
        {
            var userId = _authService.CurrentUser?.Id;
            var userRole = _authService.CurrentUser?.Role?.Name;

            var result = await _adjustmentService.CancelAdjustmentAsync(SelectedAdjustment.Id, reason, userId, userRole);
            if (result.Success)
            {
                _dialogService.ShowMessage($"Adjustment '{SelectedAdjustment.AdjustmentNumber}' has been cancelled.", "Success");
                await LoadDataAsync();
            }
            else
            {
                _dialogService.ShowMessage(result.ErrorMessage ?? "Failed to cancel adjustment.", "Cancel Failed");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteAdjustmentAsync()
    {
        if (SelectedAdjustment == null) return;
        if (SelectedAdjustment.Status != StockAdjustmentStatus.Draft)
        {
            _dialogService.ShowMessage("Only Draft adjustments can be deleted.", "Notice");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation($"Delete draft adjustment '{SelectedAdjustment.AdjustmentNumber}'?", "Confirm Delete");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            var userId = _authService.CurrentUser?.Id;
            var userRole = _authService.CurrentUser?.Role?.Name;

            var result = await _adjustmentService.DeleteAdjustmentDraftAsync(SelectedAdjustment.Id, userId, userRole);
            if (result.Success)
            {
                await LoadDataAsync();
            }
            else
            {
                _dialogService.ShowMessage(result.ErrorMessage ?? "Failed to delete adjustment.", "Delete Failed");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadDataAsync();
        }
    }
}
