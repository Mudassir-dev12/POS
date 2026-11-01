using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopPOS.UI.ViewModels.Purchases;

public partial class ReasonPromptDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Reason Required";

    [ObservableProperty]
    private string _message = "Please enter a reason for this action:";

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public event Action<string?>? RequestClose;

    public ReasonPromptDialogViewModel(string title, string message, string defaultReason = "")
    {
        Title = title;
        Message = message;
        Reason = defaultReason;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "A reason is mandatory for this action.";
            return;
        }

        RequestClose?.Invoke(Reason.Trim());
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(null);
    }
}

