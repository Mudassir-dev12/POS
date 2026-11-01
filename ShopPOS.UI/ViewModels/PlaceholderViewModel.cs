using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopPOS.UI.ViewModels;

/// <summary>
/// View model for screens planned for later implementation phases.
/// </summary>
public partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = "Coming Soon";

    [ObservableProperty]
    private string _description = "This module is scheduled for implementation in a future phase.";

    [ObservableProperty]
    private string _icon = "Apps24";

    public PlaceholderViewModel() : this("Module")
    {
    }

    public PlaceholderViewModel(string title, string icon = "Apps24", string? description = null)
    {
        Title = title;
        Icon = icon;
        if (!string.IsNullOrWhiteSpace(description))
        {
            Description = description;
        }
    }
}

