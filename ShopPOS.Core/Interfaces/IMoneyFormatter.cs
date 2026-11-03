using System;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Provides unified formatting for monetary amounts, dates, and times across the application.
/// </summary>
public interface IMoneyFormatter
{
    /// <summary>
    /// Gets the current currency symbol (e.g. "Rs").
    /// </summary>
    string CurrencySymbol { get; }

    /// <summary>
    /// Gets the decimal places for monetary amounts (0 to 3).
    /// </summary>
    int CurrencyDecimals { get; }

    /// <summary>
    /// Updates formatter configuration from shop settings.
    /// </summary>
    void UpdateSettings(string? symbol, int decimals);

    /// <summary>
    /// Formats a monetary amount with the currency symbol, explicit thousands separators, and space (e.g. "Rs 8,296.10" or "Rs -1,234.50").
    /// </summary>
    string Format(decimal amount);

    /// <summary>
    /// Formats a nullable monetary amount, displaying a fallback string if null.
    /// </summary>
    string Format(decimal? amount, string fallback = "—");

    /// <summary>
    /// Formats a monetary amount without the currency symbol (e.g. "8,296.10").
    /// </summary>
    string FormatNoSymbol(decimal amount);

    /// <summary>
    /// Formats a nullable monetary amount without the currency symbol.
    /// </summary>
    string FormatNoSymbol(decimal? amount, string fallback = "—");

    /// <summary>
    /// Formats a date using the standard format ("dd MMM yyyy").
    /// </summary>
    string FormatDate(DateTime date);

    /// <summary>
    /// Formats a nullable date using the standard format ("dd MMM yyyy").
    /// </summary>
    string FormatDate(DateTime? date, string fallback = "—");

    /// <summary>
    /// Formats a time using the standard 12-hour format ("hh:mm tt").
    /// </summary>
    string FormatTime(DateTime time);

    /// <summary>
    /// Formats a nullable time using the standard 12-hour format ("hh:mm tt").
    /// </summary>
    string FormatTime(DateTime? time, string fallback = "—");

    /// <summary>
    /// Formats date and time combined ("dd MMM yyyy, hh:mm tt").
    /// </summary>
    string FormatDateTime(DateTime dateTime);

    /// <summary>
    /// Formats nullable date and time combined ("dd MMM yyyy, hh:mm tt").
    /// </summary>
    string FormatDateTime(DateTime? dateTime, string fallback = "—");
}

