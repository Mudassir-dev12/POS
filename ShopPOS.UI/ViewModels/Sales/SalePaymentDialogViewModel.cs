using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class PaymentLineViewModel : ObservableObject
{
    [ObservableProperty]
    private PaymentMethod _method = PaymentMethod.Cash;

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    private string? _reference;

    public bool IsNonCash => Method != PaymentMethod.Cash;

    public event Action? OnChanged;

    partial void OnMethodChanged(PaymentMethod value)
    {
        OnPropertyChanged(nameof(IsNonCash));
        OnChanged?.Invoke();
    }

    partial void OnAmountChanged(decimal value)
    {
        OnChanged?.Invoke();
    }
}

public partial class SalePaymentDialogViewModel : ObservableObject
{
    private readonly ISaleCalculator _saleCalculator;

    [ObservableProperty]
    private decimal _grandTotal;

    [ObservableProperty]
    private decimal _totalPaid;

    [ObservableProperty]
    private decimal _changeAmount;

    [ObservableProperty]
    private decimal _dueAmount;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _currencySymbol = "Rs";

    [ObservableProperty]
    private string? _customerName;

    [ObservableProperty]
    private string? _customerPhone;

    public ObservableCollection<PaymentLineViewModel> Payments { get; } = new();

    public ObservableCollection<decimal> QuickCashNotes { get; } = new();

    public List<SalePaymentDto> ResultPayments { get; } = new();

    public bool Confirmed { get; set; }

    public event Action? RequestClose;

    public SalePaymentDialogViewModel(
        decimal grandTotal,
        string currencySymbol,
        ISaleCalculator saleCalculator,
        string? customerName = null,
        string? customerPhone = null)
    {
        _saleCalculator = saleCalculator;
        GrandTotal = grandTotal;
        CurrencySymbol = currencySymbol;
        CustomerName = customerName;
        CustomerPhone = customerPhone;

        // Populate initial payment line with full cash amount
        var initialLine = new PaymentLineViewModel
        {
            Method = PaymentMethod.Cash,
            Amount = grandTotal
        };
        initialLine.OnChanged += Recalculate;
        Payments.Add(initialLine);

        BuildQuickCashNotes(grandTotal);
        Recalculate();
    }

    public SalePaymentDialogViewModel(
        decimal grandTotal,
        int? customerId,
        string? customerName,
        decimal customerCurrentBalance,
        decimal customerCreditLimit,
        bool allowCreditSales,
        string currencySymbol,
        ISaleCalculator saleCalculator)
        : this(grandTotal, currencySymbol, saleCalculator, customerName, null)
    {
    }

    private void BuildQuickCashNotes(decimal total)
    {
        QuickCashNotes.Clear();
        QuickCashNotes.Add(total); // Exact

        // Generate standard cash bills rounding up from total: 5, 10, 20, 50, 100, 500, etc.
        decimal[] standardSteps = [5m, 10m, 20m, 50m, 100m, 200m, 500m];
        foreach (var step in standardSteps)
        {
            if (step > total)
            {
                decimal nextNote = Math.Ceiling(total / step) * step;
                if (!QuickCashNotes.Contains(nextNote))
                {
                    QuickCashNotes.Add(nextNote);
                }
            }
        }

        while (QuickCashNotes.Count > 5)
        {
            QuickCashNotes.RemoveAt(QuickCashNotes.Count - 1);
        }
    }

    [RelayCommand]
    public void SetCashAmount(decimal amount)
    {
        var cashLine = Payments.FirstOrDefault(p => p.Method == PaymentMethod.Cash);
        if (cashLine == null)
        {
            cashLine = new PaymentLineViewModel
            {
                Method = PaymentMethod.Cash,
                Amount = amount
            };
            cashLine.OnChanged += Recalculate;
            Payments.Insert(0, cashLine);
        }
        else
        {
            cashLine.Amount = amount;
        }

        Recalculate();
    }

    [RelayCommand]
    public void AddPaymentLine()
    {
        decimal remaining = Math.Max(0m, GrandTotal - Payments.Sum(p => p.Amount));
        var line = new PaymentLineViewModel
        {
            Method = PaymentMethod.Card,
            Amount = remaining
        };
        line.OnChanged += Recalculate;
        Payments.Add(line);
        Recalculate();
    }

    [RelayCommand]
    public void RemovePaymentLine(PaymentLineViewModel line)
    {
        if (Payments.Count > 1)
        {
            line.OnChanged -= Recalculate;
            Payments.Remove(line);
            Recalculate();
        }
    }

    public void Recalculate()
    {
        ErrorMessage = null;
        decimal totalPaid = 0m;
        decimal cashTendered = 0m;
        decimal nonCashPaid = 0m;

        foreach (var p in Payments)
        {
            if (p.Method == PaymentMethod.Cash)
            {
                cashTendered += p.Amount;
            }
            else
            {
                nonCashPaid += p.Amount;
            }
        }

        // Non-cash payments cannot exceed grand total
        if (nonCashPaid > GrandTotal)
        {
            ErrorMessage = "Non-cash payments (Card/Bank/Wallet) cannot exceed the total bill amount.";
            IsValid = false;
            TotalPaid = cashTendered + nonCashPaid;
            ChangeAmount = 0m;
            DueAmount = 0m;
            return;
        }

        decimal remainingAfterNonCash = Math.Max(0m, GrandTotal - nonCashPaid);

        if (cashTendered >= remainingAfterNonCash)
        {
            ChangeAmount = Math.Round(cashTendered - remainingAfterNonCash, 2, MidpointRounding.AwayFromZero);
            totalPaid = nonCashPaid + remainingAfterNonCash;
            DueAmount = 0m;
        }
        else
        {
            ChangeAmount = 0m;
            totalPaid = nonCashPaid + cashTendered;
            DueAmount = Math.Round(GrandTotal - totalPaid, 2, MidpointRounding.AwayFromZero);
        }

        TotalPaid = totalPaid;

        // Enforce full payment rule for counter sales:
        if (totalPaid < GrandTotal)
        {
            ErrorMessage = $"Full payment required. Short by {CurrencySymbol}{DueAmount:F2}. Underpayment is not permitted.";
            IsValid = false;
            return;
        }

        DueAmount = 0m;
        IsValid = true;
    }

    [RelayCommand]
    public void Complete()
    {
        Recalculate();
        if (!IsValid) return;

        ResultPayments.Clear();
        foreach (var p in Payments)
        {
            if (p.Amount > 0m)
            {
                ResultPayments.Add(new SalePaymentDto
                {
                    Method = p.Method,
                    Amount = p.Method == PaymentMethod.Cash ? Math.Min(p.Amount, p.Amount - ChangeAmount) : p.Amount,
                    TenderedAmount = p.Amount,
                    Reference = p.Reference?.Trim()
                });
            }
        }

        Confirmed = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke();
    }
}
