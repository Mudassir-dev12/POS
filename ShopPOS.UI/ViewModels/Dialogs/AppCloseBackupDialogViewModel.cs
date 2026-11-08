using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopPOS.UI.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the non-blocking application close backup dialog with 20s timeout and Skip button.
/// </summary>
public partial class AppCloseBackupDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = "Backing up database before closing...";

    [ObservableProperty]
    private int _secondsRemaining = 20;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isSkipped;

    [ObservableProperty]
    private bool? _dialogResult;

    [RelayCommand]
    public void Skip()
    {
        IsSkipped = true;
        DialogResult = false;
    }

    public void Complete(bool success)
    {
        IsCompleted = true;
        DialogResult = success;
    }
}

