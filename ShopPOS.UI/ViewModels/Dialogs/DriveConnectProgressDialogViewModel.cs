using System;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopPOS.UI.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the Google Drive authorization dialog showing a 3-minute countdown,
/// connection status, and cancellation support.
/// </summary>
public partial class DriveConnectProgressDialogViewModel : ObservableObject
{
    private DispatcherTimer? _timer;
    private int _remainingSeconds = 180;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _title = "Connecting to Google Drive";

    [ObservableProperty]
    private string _statusMessage = "Opening default browser for Google sign-in...";

    [ObservableProperty]
    private string _remainingTimeFormatted = "03:00";

    [ObservableProperty]
    private bool _isBusy = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool? _dialogResult;

    public void AttachCancellationTokenSource(CancellationTokenSource cts)
    {
        _cts = cts;
        _remainingSeconds = 180;
        UpdateFormattedTime();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        UpdateFormattedTime();

        if (_remainingSeconds <= 0)
        {
            _timer?.Stop();
            IsBusy = false;
            ErrorMessage = "Google Drive authorization timed out after 3 minutes. Please try again.";
            _cts?.Cancel();
        }
    }

    private void UpdateFormattedTime()
    {
        int min = Math.Max(0, _remainingSeconds / 60);
        int sec = Math.Max(0, _remainingSeconds % 60);
        RemainingTimeFormatted = $"{min:D2}:{sec:D2}";
    }

    [RelayCommand]
    public void Cancel()
    {
        _timer?.Stop();
        _cts?.Cancel();
        DialogResult = false;
    }

    public void StopTimer()
    {
        _timer?.Stop();
    }
}
