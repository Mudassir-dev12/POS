using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShopPOS.UI.ViewModels;

public partial class NavigationItem : ObservableObject
{
    public string Key { get; }
    public string Title { get; }
    public string Icon { get; }
    public string Shortcut { get; }
    public string Group { get; }

    [ObservableProperty]
    private bool _isSelected;

    public NavigationItem(string key, string title, string icon, string shortcut = "", string group = "")
    {
        Key = key;
        Title = title;
        Icon = icon;
        Shortcut = shortcut;
        Group = group;
    }
}

public class NavigationGroup
{
    public string Header { get; }
    public ObservableCollection<NavigationItem> Items { get; } = new();

    public NavigationGroup(string header, IEnumerable<NavigationItem> items)
    {
        Header = header;
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }
}
