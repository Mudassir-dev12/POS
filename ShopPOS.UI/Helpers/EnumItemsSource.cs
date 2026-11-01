using ShopPOS.Core.Enums;

namespace ShopPOS.UI.Helpers;

public static class EnumItemsSource
{
    public static PaymentMethod[] PaymentMethods { get; } = Enum.GetValues<PaymentMethod>();
}

