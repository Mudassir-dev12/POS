using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.UI.ViewModels.Customers;

public partial class CustomerDialogViewModel : ObservableObject
{
    private readonly ICustomerService _customerService;
    private readonly IPriceLevelService _priceLevelService;
    private readonly IPermissionService _permissionService;
    private readonly int? _currentUserId;
    private readonly string? _currentUserRole;

    [ObservableProperty]
    private string _dialogTitle = "Add Customer";

    [ObservableProperty]
    private bool _isCreateMode = true;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _taxNumber = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private decimal _creditLimit = 0m;

    [ObservableProperty]
    private decimal _openingBalance = 0m;

    [ObservableProperty]
    private bool _canEditOpeningBalance = true;

    [ObservableProperty]
    private int? _selectedPriceLevelId;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _canToggleIsActive = true;

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _phoneError;

    [ObservableProperty]
    private string? _emailError;

    [ObservableProperty]
    private string? _generalError;

    public ObservableCollection<PriceLevelDto> PriceLevels { get; } = new();

    public event Action<bool>? RequestClose;

    public CustomerDialogViewModel(
        ICustomerService customerService,
        IPriceLevelService priceLevelService,
        IPermissionService permissionService,
        int? currentUserId,
        string? currentUserRole)
    {
        _customerService = customerService;
        _priceLevelService = priceLevelService;
        _permissionService = permissionService;
        _currentUserId = currentUserId;
        _currentUserRole = currentUserRole;

        CanToggleIsActive = _permissionService.CanDeleteCustomers(_currentUserRole);
    }

    public async Task InitializeAsync(CustomerDto? customer = null)
    {
        var levelsRes = await _priceLevelService.GetAllAsync();
        PriceLevels.Clear();
        PriceLevels.Add(new PriceLevelDto { Id = 0, Name = "(Standard Retail)" });

        if (levelsRes.Success && levelsRes.Data != null)
        {
            foreach (var l in levelsRes.Data)
            {
                PriceLevels.Add(l);
            }
        }

        if (customer != null)
        {
            IsCreateMode = false;
            DialogTitle = "Edit Customer";
            Id = customer.Id;
            Name = customer.Name;
            Phone = customer.Phone ?? string.Empty;
            Email = customer.Email ?? string.Empty;
            TaxNumber = customer.TaxNumber ?? string.Empty;
            Address = customer.Address ?? string.Empty;
            Notes = customer.Notes ?? string.Empty;
            CreditLimit = customer.CreditLimit;
            OpeningBalance = customer.OpeningBalance;
            SelectedPriceLevelId = customer.DefaultPriceLevelId ?? 0;
            CanEditOpeningBalance = !customer.HasTransactions;
            IsActive = customer.IsActive;
        }
        else
        {
            IsCreateMode = true;
            DialogTitle = "Add New Customer";
            CanEditOpeningBalance = true;
            IsActive = true;
            SelectedPriceLevelId = 0;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        NameError = null;
        PhoneError = null;
        EmailError = null;
        GeneralError = null;

        int? priceLevelId = SelectedPriceLevelId > 0 ? SelectedPriceLevelId : null;

        if (IsCreateMode)
        {
            var res = await _customerService.CreateCustomerAsync(new CreateCustomerDto
            {
                Name = Name,
                Phone = Phone,
                Email = Email,
                TaxNumber = TaxNumber,
                Address = Address,
                Notes = Notes,
                CreditLimit = CreditLimit,
                OpeningBalance = OpeningBalance,
                DefaultPriceLevelId = priceLevelId
            }, _currentUserId, _currentUserRole);

            if (!res.Success)
            {
                ApplyErrors(res);
                return;
            }
        }
        else
        {
            var res = await _customerService.UpdateCustomerAsync(new UpdateCustomerDto
            {
                Id = Id,
                Name = Name,
                Phone = Phone,
                Email = Email,
                TaxNumber = TaxNumber,
                Address = Address,
                Notes = Notes,
                CreditLimit = CreditLimit,
                OpeningBalance = OpeningBalance,
                DefaultPriceLevelId = priceLevelId,
                IsActive = IsActive
            }, _currentUserId, _currentUserRole);

            if (!res.Success)
            {
                ApplyErrors(res);
                return;
            }
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void ApplyErrors(Result res)
    {
        GeneralError = res.ErrorMessage;
        if (res.ValidationErrors.TryGetValue("Name", out var nErrs)) NameError = string.Join("; ", nErrs);
        if (res.ValidationErrors.TryGetValue("Phone", out var pErrs)) PhoneError = string.Join("; ", pErrs);
        if (res.ValidationErrors.TryGetValue("Email", out var eErrs)) EmailError = string.Join("; ", eErrs);
    }
}

