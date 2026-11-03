namespace ShopPOS.UI.ViewModels;

public class GlobalSearchResultItem
{
    public string Group { get; set; } = string.Empty; // "Products", "Sales", "Suppliers", "Purchases"
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string NavigationTarget { get; set; } = string.Empty;
    public int EntityId { get; set; }
}
