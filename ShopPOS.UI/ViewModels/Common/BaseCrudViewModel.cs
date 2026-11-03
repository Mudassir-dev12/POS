using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Common;

public abstract partial class BaseCrudViewModel<TItemDto> : ObservableObject
    where TItemDto : class
{
    protected readonly IShopSettingService ShopSettingService;
    protected readonly IDialogService? DialogService;

    private CancellationTokenSource? _searchDebounceCts;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _showInactive = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private TItemDto? _selectedItem;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalCount = 0;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string _rowCountText = "Showing 0 items";

    [ObservableProperty]
    private string _currencySymbol = "Rs";

    [ObservableProperty]
    private bool _canAdd = true;

    [ObservableProperty]
    private bool _canEdit = true;

    [ObservableProperty]
    private bool _canDelete = true;

    [ObservableProperty]
    private bool _canDeactivate = true;

    // Bulk selection state
    [ObservableProperty]
    private int _selectedCount = 0;

    [ObservableProperty]
    private bool? _isAllOnPageSelected = false;

    [ObservableProperty]
    private bool _isAllResultsSelected = false;

    [ObservableProperty]
    private bool _canShowDeleteSelected = true;

    [ObservableProperty]
    private bool _canShowCancelSelected = false;

    public HashSet<int> SelectedIds { get; } = new();

    public bool HasSelection => SelectedCount > 0;

    public bool ShowSelectAllResultsBanner => TotalCount > Items.Count && IsAllOnPageSelected == true && !IsAllResultsSelected;

    public string SelectAllResultsBannerText => $"All {Items.Count} items on this page are selected. Select all {TotalCount:N0} items matching this filter?";

    public string SelectionStatusText => IsAllResultsSelected ? $"All {TotalCount:N0} items selected across all pages" : $"{SelectedCount:N0} selected";

    public ObservableCollection<TItemDto> Items { get; } = new();

    public BaseCrudViewModel(IShopSettingService shopSettingService, IDialogService? dialogService = null)
    {
        ShopSettingService = shopSettingService;
        DialogService = dialogService;
    }

    public virtual async Task InitializeAsync()
    {
        try
        {
            var settings = await ShopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;
        }
        catch
        {
            CurrencySymbol = "Rs";
        }

        ApplyPermissions();
        await LoadDataAsync();
    }

    protected virtual void ApplyPermissions()
    {
        CanAdd = true;
        CanEdit = true;
        CanDelete = true;
        CanDeactivate = true;
    }

    public abstract Task LoadDataAsync(CancellationToken cancellationToken = default);

    partial void OnSearchTermChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                {
                    CurrentPage = 1;
                    await App.Current.Dispatcher.InvokeAsync(async () => await LoadDataAsync(token));
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    partial void OnShowInactiveChanged(bool value)
    {
        CurrentPage = 1;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadDataAsync();
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

    protected void UpdatePagingState(int totalCount, int pageNumber, int pageSize)
    {
        TotalCount = totalCount;
        CurrentPage = pageNumber;
        PageSize = pageSize;
        TotalPages = (int)Math.Max(1, Math.Ceiling((double)totalCount / pageSize));

        int start = totalCount == 0 ? 0 : (pageNumber - 1) * pageSize + 1;
        int end = Math.Min(totalCount, pageNumber * pageSize);
        RowCountText = $"Showing {start}-{end} of {totalCount:N0} records";

        SyncSelectionState();
    }

    protected void SyncSelectionState()
    {
        foreach (var item in Items)
        {
            if (item is ISelectable sel)
            {
                sel.IsSelected = SelectedIds.Contains(sel.Id);
            }
        }
        UpdateSelectionState();
    }

    [RelayCommand]
    public void ToggleSelectAllOnPage(bool? isChecked)
    {
        bool select = isChecked == true;
        foreach (var item in Items)
        {
            if (item is ISelectable sel)
            {
                sel.IsSelected = select;
                if (select) SelectedIds.Add(sel.Id);
                else SelectedIds.Remove(sel.Id);
            }
        }
        if (!select) IsAllResultsSelected = false;
        UpdateSelectionState();
    }

    [RelayCommand]
    public void SelectAllOnPage()
    {
        ToggleSelectAllOnPage(IsAllOnPageSelected != true);
    }

    [RelayCommand]
    public void ItemSelectionChanged(TItemDto item)
    {
        if (item is ISelectable sel)
        {
            if (sel.IsSelected) SelectedIds.Add(sel.Id);
            else
            {
                SelectedIds.Remove(sel.Id);
                IsAllResultsSelected = false;
            }
        }
        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task SelectAllFilteredResultsAsync()
    {
        var allIds = await FetchAllFilteredIdsAsync();
        foreach (var id in allIds)
        {
            SelectedIds.Add(id);
        }
        IsAllResultsSelected = true;
        foreach (var item in Items)
        {
            if (item is ISelectable sel)
            {
                sel.IsSelected = true;
            }
        }
        UpdateSelectionState();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        SelectedIds.Clear();
        IsAllResultsSelected = false;
        IsAllOnPageSelected = false;
        foreach (var item in Items)
        {
            if (item is ISelectable sel)
            {
                sel.IsSelected = false;
            }
        }
        UpdateSelectionState();
    }

    public void UpdateSelectionState()
    {
        SelectedCount = SelectedIds.Count;
        int onPageCount = Items.Count;
        int onPageSelected = 0;
        foreach (var item in Items)
        {
            if (item is ISelectable sel && sel.IsSelected)
                onPageSelected++;
        }

        if (onPageCount == 0 || onPageSelected == 0)
            IsAllOnPageSelected = false;
        else if (onPageSelected == onPageCount)
            IsAllOnPageSelected = true;
        else
            IsAllOnPageSelected = null;

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowSelectAllResultsBanner));
        OnPropertyChanged(nameof(SelectAllResultsBannerText));
        OnPropertyChanged(nameof(SelectionStatusText));
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedCount == 0 || DialogService == null) return;

        bool requireTyped = SelectedCount > 50 || IsAllResultsSelected;
        if (!DialogService.ShowBulkConfirmDialog("Delete", SelectedCount, requireTyped)) return;

        var ids = SelectedIds.ToList();
        var result = await ExecuteBulkDeleteAsync(ids);
        bool deactivateRequested = DialogService.ShowBulkSummaryDialog("Bulk Delete Summary", result);
        if (deactivateRequested)
        {
            var deactCandidates = result.Items.Where(i => !i.Success && i.CanDeactivateInstead).Select(i => i.Id).ToList();
            if (deactCandidates.Count > 0)
            {
                var deactResult = await ExecuteBulkDeactivateAsync(deactCandidates);
                DialogService.ShowBulkSummaryDialog("Bulk Deactivate Summary", deactResult);
            }
        }

        ClearSelection();
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task CancelSelectedAsync()
    {
        if (SelectedCount == 0 || DialogService == null) return;

        string? reason = DialogService.ShowReasonPromptDialog("Cancel Selected", "Enter a shared cancellation reason for all selected items:");
        if (string.IsNullOrWhiteSpace(reason)) return;

        bool requireTyped = SelectedCount > 50 || IsAllResultsSelected;
        if (!DialogService.ShowBulkConfirmDialog("Cancel", SelectedCount, requireTyped, $"Are you sure you want to cancel {SelectedCount:N0} selected items with reason: '{reason.Trim()}'?")) return;

        var ids = SelectedIds.ToList();
        var result = await ExecuteBulkCancelAsync(ids, reason.Trim());
        DialogService.ShowBulkSummaryDialog("Bulk Cancel Summary", result);

        ClearSelection();
        await LoadDataAsync();
    }

    protected virtual Task<IReadOnlyList<int>> FetchAllFilteredIdsAsync() => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
    protected virtual Task<BulkOperationResult> ExecuteBulkDeleteAsync(IReadOnlyList<int> ids) => Task.FromResult(new BulkOperationResult());
    protected virtual Task<BulkOperationResult> ExecuteBulkDeactivateAsync(IReadOnlyList<int> ids) => Task.FromResult(new BulkOperationResult());
    protected virtual Task<BulkOperationResult> ExecuteBulkCancelAsync(IReadOnlyList<int> ids, string reason) => Task.FromResult(new BulkOperationResult());

    [RelayCommand]
    public abstract Task AddAsync();

    [RelayCommand]
    public abstract Task EditAsync();

    [RelayCommand]
    public abstract Task DeleteOrDeactivateAsync();

    protected string? GetCurrentRole() => ShopPOS.Core.Enums.RoleDefaults.Admin;
    protected int? GetCurrentUserId() => 1;
}
