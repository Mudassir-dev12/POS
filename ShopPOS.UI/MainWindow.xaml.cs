using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ShopPOS.UI.ViewModels;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI;

public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        Loaded += async (_, _) => await ViewModel.InitializeAsync();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. Global '/' shortcut to focus global search (unless currently typing in a text field)
        if (e.Key == Key.OemQuestion || e.Key == Key.Divide)
        {
            if (Keyboard.FocusedElement is not TextBoxBase)
            {
                GlobalSearchTextBox.Focus();
                GlobalSearchTextBox.SelectAll();
                e.Handled = true;
                return;
            }
        }

        // 2. Global Escape closes search popup if open
        if (e.Key == Key.Escape)
        {
            if (ViewModel.IsSearchOpen)
            {
                ViewModel.CloseSearch();
                e.Handled = true;
                return;
            }
        }

        // 3. Global F2 shortcut: Opens Sales (POS) from anywhere, EXCEPT inside POS where F2 opens product search
        if (e.Key == Key.F2)
        {
            if (ViewModel.CurrentView is not PosSaleViewModel)
            {
                var posNav = ViewModel.AllNavigationItems.FirstOrDefault(n => n.Key == "Sales");
                if (posNav != null)
                {
                    ViewModel.Navigate(posNav);
                    e.Handled = true;
                    return;
                }
            }
        }

        // 4. Global F5 shortcut: Refresh current view if supported
        if (e.Key == Key.F5)
        {
            // If current view model has a Refresh or Load command
            if (ViewModel.CurrentView != null)
            {
                var viewType = ViewModel.CurrentView.GetType();
                var refreshMethod = viewType.GetMethod("Refresh") ?? viewType.GetMethod("LoadDataAsync") ?? viewType.GetMethod("InitializeAsync");
                if (refreshMethod != null)
                {
                    refreshMethod.Invoke(ViewModel.CurrentView, null);
                    e.Handled = true;
                    return;
                }
            }
        }
    }
}
