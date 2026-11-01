namespace ShopPOS.UI.ViewModels;

public class NavigationItem
{
    public string Key { get; }
    public string Title { get; }
    public string Icon { get; }

    public NavigationItem(string key, string title, string icon)
    {
        Key = key;
        Title = title;
        Icon = icon;
    }
}

