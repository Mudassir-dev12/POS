using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopPOS.UI.ViewModels.Sales;

public class PosShortcutItem
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public partial class PosHelpDialogViewModel : ObservableObject
{
    public List<PosShortcutItem> Shortcuts { get; } = new()
    {
        new() { Key = "F1", Description = "Open Keyboard Shortcuts Help Overlay", Category = "General" },
        new() { Key = "F2", Description = "Open Product Search Dialog (Search name/SKU/barcode)", Category = "Products" },
        new() { Key = "F3", Description = "Select or Quick-Add Customer (View balance & credit limit)", Category = "Sales" },
        new() { Key = "F4", Description = "Hold Bill (Park cart for later resumption)", Category = "Sales" },
        new() { Key = "F5", Description = "View Held Bills List (Resume or delete parked carts)", Category = "Sales" },
        new() { Key = "F6", Description = "Apply Bill Discount (Percent or Fixed Amount)", Category = "Pricing" },
        new() { Key = "F7", Description = "Apply Line Item Discount for selected product", Category = "Pricing" },
        new() { Key = "F9", Description = "New Sale / Clear Cart (with confirmation prompt)", Category = "Sales" },
        new() { Key = "F10", Description = "Go to Customer Returns screen", Category = "Navigation" },
        new() { Key = "F11", Description = "Record Cash In / Cash Out (Shift Float adjustment)", Category = "Shifts" },
        new() { Key = "F12 / Ctrl+Enter", Description = "Open Payment Dialog (Complete Sale)", Category = "Checkout" },
        new() { Key = "Enter", Description = "Add scanned barcode / live suggestion to cart", Category = "Scanning" },
        new() { Key = "+ / -", Description = "Increase or decrease quantity of selected line", Category = "Cart" },
        new() { Key = "Delete", Description = "Remove selected line from cart", Category = "Cart" },
        new() { Key = "Escape", Description = "Exit POS or prompt to hold/discard cart", Category = "General" }
    };

    public event Action? RequestClose;

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}

