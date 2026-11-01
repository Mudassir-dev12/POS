using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Settings;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IShopSettingService _shopSettingService;
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;
    private ShopSetting _currentSetting = null!;

    [ObservableProperty]
    private string _shopName = "Shop POS";

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _taxNumber;

    [ObservableProperty]
    private string _currencySymbol = "$";

    [ObservableProperty]
    private string? _receiptFooter;

    [ObservableProperty]
    private int _receiptWidthMm = 80;

    [ObservableProperty]
    private decimal _defaultTaxPercent;

    [ObservableProperty]
    private CostingMethod _costingMethod = CostingMethod.WeightedAverage;

    [ObservableProperty]
    private bool _allowNegativeStock;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _isLoading;

    public CostingMethod[] AvailableCostingMethods { get; } = (CostingMethod[])Enum.GetValues(typeof(CostingMethod));

    public SettingsViewModel(
        IShopSettingService shopSettingService,
        IAuthService authService,
        IPermissionService permissionService)
    {
        _shopSettingService = shopSettingService;
        _authService = authService;
        _permissionService = permissionService;
    }

    public async Task InitializeAsync()
    {
        IsAdmin = true;

        IsLoading = true;
        try
        {
            _currentSetting = await _shopSettingService.GetSettingsAsync();

            ShopName = _currentSetting.ShopName;
            Address = _currentSetting.Address;
            Phone = _currentSetting.Phone;
            TaxNumber = _currentSetting.TaxNumber;
            CurrencySymbol = _currentSetting.CurrencySymbol;
            ReceiptFooter = _currentSetting.ReceiptFooter;
            ReceiptWidthMm = _currentSetting.ReceiptWidthMm;
            DefaultTaxPercent = _currentSetting.DefaultTaxPercent;
            CostingMethod = _currentSetting.CostingMethod;
            AllowNegativeStock = _currentSetting.AllowNegativeStock;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(ShopName))
        {
            ErrorMessage = "Store name is required.";
            return;
        }

        IsLoading = true;
        try
        {
            _currentSetting.ShopName = ShopName.Trim();
            _currentSetting.Address = Address?.Trim();
            _currentSetting.Phone = Phone?.Trim();
            _currentSetting.TaxNumber = TaxNumber?.Trim();
            _currentSetting.CurrencySymbol = string.IsNullOrWhiteSpace(CurrencySymbol) ? "$" : CurrencySymbol.Trim();
            ReceiptFooter = ReceiptFooter?.Trim();
            _currentSetting.ReceiptFooter = ReceiptFooter;
            _currentSetting.ReceiptWidthMm = ReceiptWidthMm;
            _currentSetting.DefaultTaxPercent = DefaultTaxPercent;
            _currentSetting.CostingMethod = CostingMethod;
            _currentSetting.AllowNegativeStock = AllowNegativeStock;

            await _shopSettingService.UpdateSettingsAsync(_currentSetting);
            SuccessMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

