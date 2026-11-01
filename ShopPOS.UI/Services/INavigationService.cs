using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopPOS.UI.Services;

/// <summary>
/// Service coordinating content view navigation in the main shell.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the current active view model displayed in the main content host.
    /// </summary>
    ObservableObject? CurrentView { get; }

    /// <summary>
    /// Navigates to the view model corresponding to the menu key.
    /// </summary>
    /// <param name="menuKey">Identifier of the target menu item.</param>
    void NavigateTo(string menuKey);

    /// <summary>
    /// Navigates to the view model corresponding to the menu key with an optional parameter.
    /// </summary>
    /// <param name="menuKey">Identifier of the target menu item.</param>
    /// <param name="parameter">Optional contextual payload for the target view model.</param>
    void NavigateTo(string menuKey, object? parameter);

    /// <summary>
    /// Occurs when navigation transitions to a new view model.
    /// </summary>
    event Action<ObservableObject>? CurrentViewChanged;
}

