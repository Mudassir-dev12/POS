using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ShopPOS.Core;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Seeding;
using ShopPOS.Data.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels;
using ShopPOS.UI.ViewModels.Dialogs;
using ShopPOS.UI.ViewModels.Setup;
using ShopPOS.UI.Views;
using ShopPOS.UI.Views.Dialogs;
using ShopPOS.UI.Views.Setup;

namespace ShopPOS.UI;

/// <summary>
/// Interaction logic for App.xaml, host lifecycle, logging, single-instance mutex, and database safety.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\ShopPOS_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;

    private IHost? _host;
    private MainWindow? _currentMainWindow;
    private bool _isShuttingDown;
    private IAppPaths _appPaths = new AppPaths();

    /// <summary>
    /// When set to true (e.g. during unit and integration test runs), OnStartup exits immediately
    /// without initializing the production host, single-instance mutex, or C:\ShopData paths.
    /// </summary>
    public static bool IsTestEnvironment { get; set; }

    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwRestore = 9;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private const int AttachParentProcess = -1;

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (IsTestEnvironment)
        {
            return;
        }

        base.OnStartup(e);

        // 1. Parse CLI flags
        string? cliDataDir = null;
        bool isSmokeTest = false;
        bool isSelfCheck = false;
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (string.Equals(e.Args[i], "--data-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                cliDataDir = e.Args[i + 1];
                i++;
            }
            else if (string.Equals(e.Args[i], "--smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                isSmokeTest = true;
            }
            else if (string.Equals(e.Args[i], "--self-check", StringComparison.OrdinalIgnoreCase))
            {
                isSelfCheck = true;
            }
        }

        // 2. Single-instance guard (Named Mutex) - bypassed during smoke tests & self-check
        if (!isSmokeTest && !isSelfCheck)
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool isOnlyInstance);
            if (!isOnlyInstance)
            {
                BringExistingInstanceToFront();
                Shutdown(0);
                return;
            }
        }

        if (isSelfCheck)
        {
            AttachConsole(AttachParentProcess);
            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine("ShopPOS Diagnostic Self-Check Probe (--self-check)");
            Console.WriteLine("================================================================================");

            try
            {
                var userSettingsService = new UserSettingsService();
                string resolvedDataDir = userSettingsService.ResolveDataDirectory(cliDataDir);
                _appPaths = new AppPaths(resolvedDataDir);
                _appPaths.EnsureDirectoriesExist();

                AppDbContext? dbCtx = null;
                try
                {
                    var opt = new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlite($"Data Source={_appPaths.DatabasePath}")
                        .Options;
                    dbCtx = new AppDbContext(opt);
                    if (File.Exists(_appPaths.DatabasePath))
                    {
                        await dbCtx.Database.MigrateAsync();
                    }
                }
                catch { }

                var selfCheck = new SelfCheckService(_appPaths, dbCtx);
                var report = await selfCheck.RunAllChecksAsync();

                Console.WriteLine(report.FormattedReport);
                if (report.AllPassed)
                {
                    Console.WriteLine("OVERALL RESULT: ALL CHECKS PASSED (Exit Code: 0)");
                    Shutdown(0);
                }
                else
                {
                    Console.WriteLine("OVERALL RESULT: ONE OR MORE CHECKS FAILED (Exit Code: 1)");
                    Shutdown(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Self-check execution error: {ex.Message}");
                Console.WriteLine(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        // 3. Setup global unhandled exception handling
        SetupGlobalExceptionHandling();

        try
        {
            // 4. Resolve data directory via IUserSettingsService or CLI flag
            var userSettingsService = new UserSettingsService();
            string resolvedDataDir = userSettingsService.ResolveDataDirectory(cliDataDir);
            _appPaths = new AppPaths(resolvedDataDir);
            _appPaths.EnsureDirectoriesExist();

            // 5. Pre-flight Filesystem & Free Space Validation (500 MB minimum)
            var startupChecker = new StartupSafetyChecker();
            var fsCheck = startupChecker.ValidateFileSystem(_appPaths.DataDirectory);
            if (!fsCheck.CanProceed)
            {
                MessageBox.Show(fsCheck.Message, "ShopPOS Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // 6. Configure Serilog with 30-day retention and EF Core Warning filter
            string combinedLogPath = Path.Combine(_appPaths.LogsDirectory, "shoppos-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File(
                    path: combinedLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("ShopPOS application starting up. Database: {DbPath}", _appPaths.DatabasePath);

            // 7. Build DI Host
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IAppPaths>(_appPaths);
                    services.AddCoreServices();
                    services.AddDataServices(new DatabaseOptions { DbPath = _appPaths.DatabasePath });
                    services.AddUiServices();
                })
                .Build();

            await _host.StartAsync();

            bool dbExisted = File.Exists(_appPaths.DatabasePath);

            // 8. Database Pre-Flight Locks & Compatibility Checks
            if (dbExisted)
            {
                var lockCheck = startupChecker.CheckDatabaseLock(_appPaths.DatabasePath);
                if (!lockCheck.CanProceed)
                {
                    MessageBox.Show(lockCheck.Message, "ShopPOS Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }

                var dbCheck = await startupChecker.ValidateDatabaseAsync(_appPaths.DatabasePath);
                if (!dbCheck.CanProceed)
                {
                    if (dbCheck.Issue == StartupIssueType.DatabaseNewerThanApp)
                    {
                        MessageBox.Show(dbCheck.Message, "ShopPOS Update Required", MessageBoxButton.OK, MessageBoxImage.Stop);
                        Shutdown(1);
                        return;
                    }
                    if (dbCheck.Issue == StartupIssueType.DatabaseCorrupted)
                    {
                        MessageBox.Show(dbCheck.Message, "Database Integrity Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            // 9. First-Run Setup Wizard if database does not exist
            if (!dbExisted && !isSmokeTest)
            {
                var scopeFactory = _host.Services.GetRequiredService<IServiceScopeFactory>();
                using var scope = scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var wizardVm = new SetupWizardViewModel(
                    _appPaths,
                    sp.GetRequiredService<IUserSettingsService>(),
                    sp.GetRequiredService<IDialogService>(),
                    sp.GetService<IRestoreService>(),
                    sp.GetService<IBackupUploader>(),
                    sp.GetService<IReceiptPrinterService>(),
                    () => sp.GetRequiredService<AppDbContext>(),
                    sp.GetService<IDataSeeder>(),
                    sp.GetService<IDataToolsService>(),
                    sp.GetService<IBackupService>(),
                    scopeFactory: scopeFactory,
                    gitHubUploader: sp.GetService<IGitHubBackupUploader>());

                var wizardWin = new SetupWizardWindow(wizardVm);
                bool? result = wizardWin.ShowDialog();

                if (result != true || !wizardVm.CompletedSuccessfully)
                {
                    Log.Information("First-run setup wizard closed without completion. Shutting down.");
                    Shutdown(0);
                    return;
                }
            }

            // 10. Database Safety: Check startup lock file and purge logs older than 30 days
            var safetyService = _host.Services.GetRequiredService<IDatabaseSafetyService>();
            safetyService.CheckAndCreateLockFile();
            safetyService.PurgeOldLogs(30);

            // Background quick_check
            _ = Task.Run(async () =>
            {
                try
                {
                    bool healthy = await safetyService.RunQuickCheckAsync();
                    if (!healthy)
                    {
                        Log.Warning("Startup PRAGMA quick_check detected database inconsistency.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Background database quick_check encountered an exception.");
                }
            });

            // 11. Apply migrations and seed data
            using (var scope = _host.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Check pending migrations and create safe pre-migration backup
                var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
                if (pendingMigrations.Any())
                {
                    Log.Information("Pending migrations detected ({Count}): {Migrations}. Creating pre-migration database backup...",
                        pendingMigrations.Count, string.Join(", ", pendingMigrations));

                    var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                    var bResult = await backupService.CreateBackupAsync(BackupKind.PreMigration);
                    if (!bResult.Success)
                    {
                        DatabaseBackupHelper.CreatePreMigrationBackup(_appPaths.DatabasePath, pendingMigrations.First());
                    }
                }

                await dbContext.Database.MigrateAsync();

                var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                await seeder.SeedAsync();

                var settingRepo = scope.ServiceProvider.GetRequiredService<IShopSettingRepository>();
                var settings = await settingRepo.GetCurrentSettingsAsync();
                if (settings != null)
                {
                    MoneyFormatter.Instance.UpdateSettings(settings.CurrencySymbol, settings.CurrencyDecimals);
                }
            }

            // 12. Automated Smoke Test (--smoke-test)
            if (isSmokeTest)
            {
                Log.Information("Running automated smoke test (--smoke-test)...");
                try
                {
                    using (var scope = _host.Services.CreateScope())
                    {
                        var dashVm = scope.ServiceProvider.GetRequiredService<ViewModels.Dashboard.DashboardViewModel>();
                        await dashVm.InitializeAsync();

                        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                        var bResult = await backupService.CreateBackupAsync(BackupKind.Manual);
                        if (!bResult.Success)
                        {
                            Log.Error("Smoke test backup failed: {Error}", bResult.ErrorMessage);
                            Shutdown(1);
                            return;
                        }
                    }

                    Log.Information("Smoke test completed successfully with exit code 0.");
                    Shutdown(0);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Smoke test failed with exception.");
                    Shutdown(1);
                    return;
                }
            }

            // 13. Start automated backup scheduler & network connectivity monitor
            var scheduler = _host.Services.GetRequiredService<IBackupScheduler>();
            scheduler.Start();

            var networkService = _host.Services.GetService<NetworkConnectivityService>();
            networkService?.Start();

            // 14. Launch Main Window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _currentMainWindow = mainWindow;
            MainWindow = mainWindow;
            mainWindow.Closing += MainWindow_Closing;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Fatal(ex, "Fatal failure during application initialization.");
            }
            catch { }

            var errorInfo = BootstrapCrashHandler.LogAndMap(ex);
            ShowEarlyBootstrapError(errorInfo);
            Shutdown(1);
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;
        e.Cancel = true;

        try
        {
            if (_host != null)
            {
                using var scope = _host.Services.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                var settings = await backupService.GetSettingsAsync();

                if (settings.BackupEnabled && settings.BackupOnAppClose)
                {
                    var dialogVm = new AppCloseBackupDialogViewModel();
                    var dialog = new AppCloseBackupDialog { DataContext = dialogVm, Owner = _currentMainWindow };

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var res = await backupService.CreateBackupAsync(BackupKind.Scheduled, cancellationToken: cts.Token);
                            Dispatcher.Invoke(() => dialogVm.Complete(res.Success));
                        }
                        catch
                        {
                            Dispatcher.Invoke(() => dialogVm.Complete(false));
                        }
                    }, cts.Token);

                    var countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    countdownTimer.Tick += (s, args) =>
                    {
                        if (dialogVm.SecondsRemaining > 0)
                        {
                            dialogVm.SecondsRemaining--;
                        }
                        else
                        {
                            countdownTimer.Stop();
                            cts.Cancel();
                            dialogVm.Skip();
                        }
                    };
                    countdownTimer.Start();

                    dialogVm.PropertyChanged += (s, args) =>
                    {
                        if (args.PropertyName == nameof(AppCloseBackupDialogViewModel.DialogResult) && dialogVm.DialogResult.HasValue)
                        {
                            countdownTimer.Stop();
                            if (dialog.IsVisible) dialog.Close();
                        }
                    };

                    dialog.ShowDialog();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error occurred during application closing backup.");
        }
        finally
        {
            _currentMainWindow?.Close();
        }
    }

    private static void BringExistingInstanceToFront()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var other = Process.GetProcessesByName(current.ProcessName)
                .FirstOrDefault(p => p.Id != current.Id);

            if (other != null && other.MainWindowHandle != IntPtr.Zero)
            {
                ShowWindow(other.MainWindowHandle, SwRestore);
                SetForegroundWindow(other.MainWindowHandle);
            }
        }
        catch { }
    }

    private void SetupGlobalExceptionHandling()
    {
        DispatcherUnhandledException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI Dispatcher Exception.");
            ShowErrorDialog("An unexpected application error occurred.", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled AppDomain Exception. Terminating: {IsTerminating}", args.IsTerminating);
                ShowErrorDialog("A critical system error occurred.", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unobserved Task Exception.");
            args.SetObserved();
        };
    }

    private void ShowEarlyBootstrapError(BootstrapErrorInfo info)
    {
        try
        {
            var dialog = new Views.Dialogs.UnhandledExceptionDialog(
                $"{info.ErrorSummary}\n\n{info.PlainLanguageAdvice}\n\nA detailed startup error log has been written to:\n{info.LogFilePath}",
                info.TechnicalDetails,
                Path.GetDirectoryName(info.LogFilePath));
            dialog.ShowDialog();
        }
        catch
        {
            MessageBox.Show(
                $"{info.ErrorSummary}\n\n{info.PlainLanguageAdvice}\n\nLog saved to: {info.LogFilePath}\n\nTechnical details:\n{info.TechnicalDetails}",
                "ShopPOS - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ShowErrorDialog(string message, Exception ex)
    {
        try
        {
            var dialog = new Views.Dialogs.UnhandledExceptionDialog(
                $"{message}\n\n{ex.Message}",
                ex.ToString(),
                _appPaths?.LogsDirectory);
            dialog.ShowDialog();
        }
        catch
        {
            MessageBox.Show(
                $"{message}\n\n{ex.Message}\n\nCheck logs in {_appPaths?.LogsDirectory}",
                "ShopPOS - Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            Log.Information("ShopPOS shutting down.");

            try
            {
                var networkService = _host.Services.GetService<NetworkConnectivityService>();
                networkService?.Dispose();

                var scheduler = _host.Services.GetService<IBackupScheduler>();
                scheduler?.Stop();

                var safetyService = _host.Services.GetService<IDatabaseSafetyService>();
                if (safetyService != null)
                {
                    await safetyService.RunWalCheckpointAsync();
                    safetyService.RemoveLockFile();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while performing database safety shutdown tasks.");
            }

            await _host.StopAsync();
            _host.Dispose();
        }

        if (_singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch { }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
