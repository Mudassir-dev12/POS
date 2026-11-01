using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.UI.ViewModels.Suppliers;

public partial class SupplierDialogViewModel : ObservableObject
{
    private readonly ISupplierService _supplierService;
    private readonly int? _currentUserId;
    private readonly string? _currentUserRole;

    [ObservableProperty]
    private string _dialogTitle = "Add Supplier";

    [ObservableProperty]
    private bool _isCreateMode = true;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _contactPerson = string.Empty;

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
    private decimal _openingBalance = 0m;

    [ObservableProperty]
    private bool _canEditOpeningBalance = true;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _phoneError;

    [ObservableProperty]
    private string? _emailError;

    [ObservableProperty]
    private string? _generalError;

    public event Action<bool>? RequestClose;

    public SupplierDialogViewModel(ISupplierService supplierService, int? currentUserId, string? currentUserRole)
    {
        _supplierService = supplierService;
        _currentUserId = currentUserId;
        _currentUserRole = currentUserRole;
    }

    public void Initialize(SupplierDto? supplier = null)
    {
        if (supplier != null)
        {
            IsCreateMode = false;
            DialogTitle = "Edit Supplier";
            Id = supplier.Id;
            Name = supplier.Name;
            ContactPerson = supplier.ContactPerson ?? string.Empty;
            Phone = supplier.Phone ?? string.Empty;
            Email = supplier.Email ?? string.Empty;
            TaxNumber = supplier.TaxNumber ?? string.Empty;
            Address = supplier.Address ?? string.Empty;
            Notes = supplier.Notes ?? string.Empty;
            OpeningBalance = supplier.OpeningBalance;
            CanEditOpeningBalance = !supplier.HasTransactions;
            IsActive = supplier.IsActive;
        }
        else
        {
            IsCreateMode = true;
            DialogTitle = "Add New Supplier";
            CanEditOpeningBalance = true;
            IsActive = true;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        NameError = null;
        PhoneError = null;
        EmailError = null;
        GeneralError = null;

        if (IsCreateMode)
        {
            var res = await _supplierService.CreateSupplierAsync(new CreateSupplierDto
            {
                Name = Name,
                ContactPerson = ContactPerson,
                Phone = Phone,
                Email = Email,
                TaxNumber = TaxNumber,
                Address = Address,
                Notes = Notes,
                OpeningBalance = OpeningBalance
            }, _currentUserId, _currentUserRole);

            if (!res.Success)
            {
                ApplyErrors(res);
                return;
            }
        }
        else
        {
            var res = await _supplierService.UpdateSupplierAsync(new UpdateSupplierDto
            {
                Id = Id,
                Name = Name,
                ContactPerson = ContactPerson,
                Phone = Phone,
                Email = Email,
                TaxNumber = TaxNumber,
                Address = Address,
                Notes = Notes,
                OpeningBalance = OpeningBalance,
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

