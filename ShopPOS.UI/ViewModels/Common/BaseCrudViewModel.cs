using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Common;

public abstract partial class BaseCrudViewModel<TItemDto> : ObservableObject
    where TItemDto : class
{
    protected readonly IAuthService AuthService;
    protected readonly IPermissionService PermissionService;
    protected readonly IShopSettingService ShopSettingService;

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
    private string _currencySymbol = "$";

    [ObservableProperty]
    private bool _canAdd = true;

    [ObservableProperty]
    private bool _canEdit = true;

    [ObservableProperty]
    private bool _canDelete = true;

    [ObservableProperty]
    private bool _canDeactivate = true;

    public ObservableCollection<TItemDto> Items { get; } = new();

    public BaseCrudViewModel(
        IAuthService authService,
        IPermissionService permissionService,
        IShopSettingService shopSettingService)
    {
        AuthService = authService;
        PermissionService = permissionService;
        ShopSettingService = shopSettingService;
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
            CurrencySymbol = "$";
        }

        ApplyPermissions();
        await LoadDataAsync();
    }

    protected abstract void ApplyPermissions();

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
    }

    [RelayCommand]
    public abstract Task AddAsync();

    [RelayCommand]
    public abstract Task EditAsync();

    [RelayCommand]
    public abstract Task DeleteOrDeactivateAsync();

    protected string? GetCurrentRole() => AuthService.CurrentUser?.Role?.Name;
    protected int? GetCurrentUserId() => AuthService.CurrentUser?.Id;
}

