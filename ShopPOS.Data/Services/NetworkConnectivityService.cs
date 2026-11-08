using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Monitors network status, schedules 15-minute background queue checks,
/// runs startup checks, and triggers backup upload processing 10 seconds after network reconnects.
/// </summary>
public class NetworkConnectivityService : IDisposable
{
    private readonly IBackupUploader _backupUploader;
    private readonly ILogger<NetworkConnectivityService> _logger;
    private Timer? _periodicTimer;
    private CancellationTokenSource? _debounceCts;
    private bool _isStarted;
    private readonly object _lock = new();

    public NetworkConnectivityService(
        IBackupUploader backupUploader,
        ILogger<NetworkConnectivityService>? logger = null)
    {
        _backupUploader = backupUploader;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NetworkConnectivityService>.Instance;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isStarted) return;
            _isStarted = true;

            // Subscribe to network availability changes
            try
            {
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register NetworkAvailabilityChanged event handler.");
            }

            // Periodic queue check every 15 minutes (with first run after 15 min)
            _periodicTimer = new Timer(async _ =>
            {
                try
                {
                    await _backupUploader.ProcessQueueAsync(force: false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Periodic Drive queue check error.");
                }
            }, null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

            // Startup upload check after 2 minutes so application startup stays fast
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    _logger.LogInformation("Running startup Google Drive backup queue check.");
                    await _backupUploader.ProcessQueueAsync(force: false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Startup Drive queue check error.");
                }
            });
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            _logger.LogInformation("Network connection restored. Scheduling backup queue trigger in 10 seconds.");

            lock (_lock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), token);
                        if (!token.IsCancellationRequested)
                        {
                            _logger.LogInformation("Triggering Google Drive backup queue following network availability return.");
                            await _backupUploader.ProcessQueueAsync(force: false, token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Debounce cancelled by newer event
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing Drive queue on network reconnect.");
                    }
                }, token);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _isStarted = false;
            try
            {
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            }
            catch { }

            _periodicTimer?.Dispose();
            _periodicTimer = null;

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }
}

