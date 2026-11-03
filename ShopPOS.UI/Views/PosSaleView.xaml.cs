using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views;

public partial class PosSaleView : UserControl
{
    public PosSaleView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ScanBox.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PosSaleViewModel vm) return;

        if (vm.IsSaleSuccess)
        {
            if (e.Key == Key.Space || e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape)
            {
                vm.DismissSuccessPanelCommand.Execute(null);
                ScanBox.Focus();
                e.Handled = true;
                return;
            }
        }

        // Suggestions navigation
        if (vm.ShowSuggestions && vm.Suggestions.Count > 0)
        {
            if (e.Key == Key.Down)
            {
                int nextIndex = vm.SelectedSuggestion != null ? vm.Suggestions.IndexOf(vm.SelectedSuggestion) + 1 : 0;
                if (nextIndex < vm.Suggestions.Count)
                {
                    vm.SelectedSuggestion = vm.Suggestions[nextIndex];
                    SuggestionsList.ScrollIntoView(vm.SelectedSuggestion);
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                int prevIndex = vm.SelectedSuggestion != null ? vm.Suggestions.IndexOf(vm.SelectedSuggestion) - 1 : 0;
                if (prevIndex >= 0 && prevIndex < vm.Suggestions.Count)
                {
                    vm.SelectedSuggestion = vm.Suggestions[prevIndex];
                    SuggestionsList.ScrollIntoView(vm.SelectedSuggestion);
                }
                e.Handled = true;
                return;
            }
        }

        if (vm.ShowSuggestions && e.Key == Key.Escape)
        {
            vm.ShowSuggestions = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && vm.ShowSuggestions && vm.SelectedSuggestion != null)
        {
            vm.SelectSuggestionCommand.Execute(vm.SelectedSuggestion);
            ScanBox.Focus();
            e.Handled = true;
            return;
        }

        // USB Barcode Scanner Wedge Auto-Focus: If user scans while focus is elsewhere, redirect to ScanBox
        if (!ScanBox.IsFocused && !(Keyboard.FocusedElement is TextBox) && e.Key != Key.Tab && e.Key != Key.Escape && !e.Key.ToString().StartsWith("F") && e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && e.Key != Key.LeftAlt && e.Key != Key.RightAlt)
        {
            ScanBox.Focus();
        }

        switch (e.Key)
        {
            case Key.F1:
                vm.ShowHelpCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F2:
                vm.OpenProductSearchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F3:
                ScanBox.Focus();
                ScanBox.SelectAll();
                e.Handled = true;
                break;
            case Key.F4:
                vm.HoldBillCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F5:
                vm.ShowHeldBillsCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F6:
                vm.OpenBillDiscountCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F7:
                vm.OpenLineDiscountCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F8:
                vm.DiscardCartCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F9:
                vm.OpenCashDrawerCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F12:
                vm.CheckoutCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
