using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ShopPOS.UI.Helpers;

public class StringToVisibilityConverter : IValueConverter
{
    public static readonly StringToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public static readonly BooleanToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public static readonly InverseBooleanToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is false ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not Visibility.Visible;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is false;
    }
}

public class StepVisibilityConverter : IValueConverter
{
    public static readonly StepVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string paramStr)
        {
            var allowed = paramStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (allowed.Contains(currentStep.ToString()))
            {
                return Visibility.Visible;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class StepColorConverter : IValueConverter
{
    public static readonly StepColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && int.TryParse(parameter?.ToString(), out var targetStep))
        {
            if (currentStep == targetStep)
            {
                return new SolidColorBrush(Color.FromRgb(37, 99, 235)); // #2563EB Active blue
            }
            if (currentStep > targetStep)
            {
                return new SolidColorBrush(Color.FromRgb(22, 163, 74)); // #16A34A Completed green
            }
        }
        return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94A3B8 Muted slate
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class LowStockBackgroundConverter : IValueConverter
{
    public static readonly LowStockBackgroundConverter Instance = new();
    private static readonly Brush LowStockBrush = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11)); // Amber tint

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? LowStockBrush : Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class InactiveOpacityConverter : IValueConverter
{
    public static readonly InactiveOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is false ? 0.45 : 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class PurchaseStatusBadgeColorConverter : IValueConverter
{
    public static readonly PurchaseStatusBadgeColorConverter Instance = new();
    private static readonly Brush DraftBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // #D97706
    private static readonly Brush PostedBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74)); // #16A34A
    private static readonly Brush CancelledBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
    private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string s = value?.ToString() ?? string.Empty;
        return s switch
        {
            "Draft" => DraftBrush,
            "Posted" => PostedBrush,
            "Cancelled" => CancelledBrush,
            _ => DefaultBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class PurchaseStatusBadgeBackgroundConverter : IValueConverter
{
    public static readonly PurchaseStatusBadgeBackgroundConverter Instance = new();
    private static readonly Brush DraftBg = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // #FEF3C7
    private static readonly Brush PostedBg = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // #DCFCE7
    private static readonly Brush CancelledBg = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #FEE2E2
    private static readonly Brush DefaultBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string s = value?.ToString() ?? string.Empty;
        return s switch
        {
            "Draft" => DraftBg,
            "Posted" => PostedBg,
            "Cancelled" => CancelledBg,
            _ => DefaultBg
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class PaymentStatusBadgeColorConverter : IValueConverter
{
    public static readonly PaymentStatusBadgeColorConverter Instance = new();
    private static readonly Brush PaidBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74)); // #16A34A
    private static readonly Brush PartialBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // #2563EB
    private static readonly Brush UnpaidBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // #D97706
    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
    private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string s = value?.ToString() ?? string.Empty;
        return s switch
        {
            "Paid" => PaidBrush,
            "Partial" => PartialBrush,
            "Unpaid" => UnpaidBrush,
            "Overdue" => OverdueBrush,
            _ => DefaultBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class PaymentStatusBadgeBackgroundConverter : IValueConverter
{
    public static readonly PaymentStatusBadgeBackgroundConverter Instance = new();
    private static readonly Brush PaidBg = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // #DCFCE7
    private static readonly Brush PartialBg = new SolidColorBrush(Color.FromRgb(219, 234, 254)); // #DBEAFE
    private static readonly Brush UnpaidBg = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // #FEF3C7
    private static readonly Brush OverdueBg = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #FEE2E2
    private static readonly Brush DefaultBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string s = value?.ToString() ?? string.Empty;
        return s switch
        {
            "Paid" => PaidBg,
            "Partial" => PartialBg,
            "Unpaid" => UnpaidBg,
            "Overdue" => OverdueBg,
            _ => DefaultBg
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BooleanToActiveStatusConverter : IValueConverter
{
    public static readonly BooleanToActiveStatusConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "Active" : "Inactive";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class StockStatusDisplayConverter : IValueConverter
{
    public static readonly StockStatusDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ShopPOS.Core.Enums.StockStatus status)
        {
            return status switch
            {
                ShopPOS.Core.Enums.StockStatus.OK => "OK",
                ShopPOS.Core.Enums.StockStatus.Low => "Low stock",
                ShopPOS.Core.Enums.StockStatus.OutOfStock => "Out of stock",
                ShopPOS.Core.Enums.StockStatus.Negative => "Negative",
                ShopPOS.Core.Enums.StockStatus.NotTracked => "Not tracked",
                _ => status.ToString()
            };
        }
        return "All";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
