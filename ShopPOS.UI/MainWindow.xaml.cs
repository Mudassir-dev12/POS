using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ShopPOS.UI.ViewModels;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI;

public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly ShopPOS.Core.Interfaces.IUserSettingsService? _userSettingsService;
    private readonly System.Text.StringBuilder _barcodeBuffer = new();
    private DateTime _lastBarcodeTime = DateTime.MinValue;

    public MainWindow(MainWindowViewModel viewModel, ShopPOS.Core.Interfaces.IUserSettingsService? userSettingsService = null)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _userSettingsService = userSettingsService;
        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            RestoreWindowSettings();
            await ViewModel.InitializeAsync();
        };

        Closing += (_, _) => SaveWindowSettings();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewTextInput += MainWindow_PreviewTextInput;
    }

    private void RestoreWindowSettings()
    {
        if (_userSettingsService == null) return;
        try
        {
            var ws = _userSettingsService.Load().Window;
            if (ws != null)
            {
                if (ws.Width >= MinWidth) Width = ws.Width;
                if (ws.Height >= MinHeight) Height = ws.Height;

                if (ws.Left >= SystemParameters.VirtualScreenLeft &&
                    ws.Left + ws.Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
                {
                    Left = ws.Left;
                }

                if (ws.Top >= SystemParameters.VirtualScreenTop &&
                    ws.Top + ws.Height <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
                {
                    Top = ws.Top;
                }

                if (ws.WindowState == (int)WindowState.Maximized)
                {
                    WindowState = WindowState.Maximized;
                }

                ViewModel.IsSidebarCollapsed = ws.IsSidebarCollapsed;
            }
        }
        catch { }
    }

    private void SaveWindowSettings()
    {
        if (_userSettingsService == null) return;
        try
        {
            var settings = _userSettingsService.Load();
            settings.Window = new ShopPOS.Core.Models.WindowSettings
            {
                Width = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width,
                Height = RestoreBounds.Height > 0 ? RestoreBounds.Height : Height,
                Left = RestoreBounds.Left > 0 ? RestoreBounds.Left : Left,
                Top = RestoreBounds.Top > 0 ? RestoreBounds.Top : Top,
                WindowState = (int)WindowState,
                IsSidebarCollapsed = ViewModel.IsSidebarCollapsed
            };
            _userSettingsService.Save(settings);
        }
        catch { }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Barcode Scanner detection (rapid characters ending in Enter outside POS)
        if (e.Key == Key.Enter)
        {
            if (ViewModel.CurrentView is not PosSaleViewModel && _barcodeBuffer.Length >= 2)
            {
                string barcode = _barcodeBuffer.ToString().Trim();
                _barcodeBuffer.Clear();
                if (!string.IsNullOrEmpty(barcode))
                {
                    e.Handled = true;
                    _ = ViewModel.HandleGlobalBarcodeScanAsync(barcode);
                    return;
                }
            }
            _barcodeBuffer.Clear();
        }

        // 1. Global F2 shortcut: Opens Sales (POS) from anywhere, EXCEPT inside POS where F2 opens product search
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

        // 2. Global F5 shortcut: Refresh current view if supported
        if (e.Key == Key.F5)
        {
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
    private void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // When in POS, POS has its own ScanBox and scanner handling
        if (ViewModel.CurrentView is PosSaleViewModel) return;

        // If focus is inside a TextBox and user is typing slowly (> 120ms between keys), don't buffer as barcode
        if (Keyboard.FocusedElement is TextBoxBase)
        {
            var interval = (DateTime.UtcNow - _lastBarcodeTime).TotalMilliseconds;
            if (interval > 120 && _barcodeBuffer.Length == 0)
            {
                return;
            }
        }

        var now = DateTime.UtcNow;
        if ((now - _lastBarcodeTime).TotalMilliseconds > 250)
        {
            _barcodeBuffer.Clear();
        }
        _lastBarcodeTime = now;
        _barcodeBuffer.Append(e.Text);
    }

}
