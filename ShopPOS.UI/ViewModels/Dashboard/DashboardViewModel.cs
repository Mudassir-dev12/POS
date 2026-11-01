using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService _authService;
    private readonly Services.INavigationService _navigationService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DashboardStatsDto? _stats;

    [ObservableProperty]
    private bool _isCashier;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to ShopPOS";

    public DashboardViewModel(
        IInventoryService inventoryService,
        IAuthService authService,
        Services.INavigationService navigationService)
    {
        _inventoryService = inventoryService;
        _authService = authService;
        _navigationService = navigationService;

        IsCashier = false;
        if (_authService.CurrentUser != null)
        {
            WelcomeMessage = $"Welcome back, {_authService.CurrentUser.FullName}";
        }
    }

    [RelayCommand]
    public void OpenLowStock()
    {
        _navigationService.NavigateTo("StockOverview", StockStatus.Low);
    }

    [RelayCommand]
    public void OpenOutOfStock()
    {
        _navigationService.NavigateTo("StockOverview", StockStatus.OutOfStock);
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadStatsAsync();
    }

    [RelayCommand]
    public async Task LoadStatsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var userRole = _authService.CurrentUser?.Role?.Name;
            var result = await _inventoryService.GetDashboardStatsAsync(userRole);
            if (result.Success)
            {
                Stats = result.Data;
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to load dashboard metrics.";
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
}
