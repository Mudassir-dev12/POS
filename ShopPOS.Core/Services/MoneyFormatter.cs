using System;
using System.Globalization;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

/// <summary>
/// Thread-safe implementation of IMoneyFormatter for standardized monetary and temporal formatting.
/// </summary>
public class MoneyFormatter : IMoneyFormatter
{
    private readonly object _lock = new();
    private string _currencySymbol = "Rs";
    private int _currencyDecimals = 2;
    private string _formatPattern = "#,##0.00";

    /// <summary>
    /// Static singleton instance accessible from XAML converters, markup helpers, and non-DI scenarios.
    /// </summary>
    public static MoneyFormatter Instance { get; } = new();

    public MoneyFormatter()
    {
        UpdatePattern();
    }

    public MoneyFormatter(string? currencySymbol, int currencyDecimals = 2)
    {
        UpdateSettings(currencySymbol, currencyDecimals);
    }

    public string CurrencySymbol
    {
        get
        {
            lock (_lock) return _currencySymbol;
        }
    }

    public int CurrencyDecimals
    {
        get
        {
            lock (_lock) return _currencyDecimals;
        }
    }

    public void UpdateSettings(string? symbol, int decimals)
    {
        lock (_lock)
        {
            _currencySymbol = string.IsNullOrWhiteSpace(symbol) ? "Rs" : symbol.Trim();
            _currencyDecimals = Math.Clamp(decimals, 0, 3);
            UpdatePattern();
        }
    }

    private void UpdatePattern()
    {
        _formatPattern = _currencyDecimals switch
        {
            0 => "#,##0",
            1 => "#,##0.0",
            3 => "#,##0.000",
            _ => "#,##0.00"
        };
    }

    public string Format(decimal amount)
    {
        lock (_lock)
        {
            decimal rounded = Math.Round(amount, _currencyDecimals, MidpointRounding.AwayFromZero);
            string formattedNumber = rounded.ToString(_formatPattern, CultureInfo.InvariantCulture);
            return $"{_currencySymbol} {formattedNumber}";
        }
    }

    public string Format(decimal? amount, string fallback = "—")
    {
        return amount.HasValue ? Format(amount.Value) : fallback;
    }

    public string FormatNoSymbol(decimal amount)
    {
        lock (_lock)
        {
            decimal rounded = Math.Round(amount, _currencyDecimals, MidpointRounding.AwayFromZero);
            return rounded.ToString(_formatPattern, CultureInfo.InvariantCulture);
        }
    }

    public string FormatNoSymbol(decimal? amount, string fallback = "—")
    {
        return amount.HasValue ? FormatNoSymbol(amount.Value) : fallback;
    }

    public string FormatDate(DateTime date)
    {
        return date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }

    public string FormatDate(DateTime? date, string fallback = "—")
    {
        return date.HasValue ? FormatDate(date.Value) : fallback;
    }

    public string FormatTime(DateTime time)
    {
        return time.ToString("hh:mm tt", CultureInfo.InvariantCulture);
    }

    public string FormatTime(DateTime? time, string fallback = "—")
    {
        return time.HasValue ? FormatTime(time.Value) : fallback;
    }

    public string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture);
    }

    public string FormatDateTime(DateTime? dateTime, string fallback = "—")
    {
        return dateTime.HasValue ? FormatDateTime(dateTime.Value) : fallback;
    }
}

