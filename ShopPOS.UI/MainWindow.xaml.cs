using System.Windows;
using ShopPOS.UI.ViewModels;

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
    }
}
