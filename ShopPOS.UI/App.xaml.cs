using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ShopPOS.Core;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;
using ShopPOS.Data;
using ShopPOS.Data.Seeding;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels;
using ShopPOS.UI.Views;

namespace ShopPOS.UI;

/// <summary>
/// Interaction logic for App.xaml, host lifecycle, logging, and global exception handling.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private MainWindow? _currentMainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Setup global unhandled exception handling
        SetupGlobalExceptionHandling();

        try
        {
            // 2. Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
            var logOptions = configuration.GetSection(LoggingOptions.SectionName).Get<LoggingOptions>() ?? new LoggingOptions();

            // 3. Ensure log directory exists & configure Serilog
            if (!Directory.Exists(logOptions.LogDirectory))
            {
                Directory.CreateDirectory(logOptions.LogDirectory);
            }

            var minLevel = Enum.TryParse<Serilog.Events.LogEventLevel>(logOptions.MinimumLevel, true, out var parsedLevel)
                ? parsedLevel
                : Serilog.Events.LogEventLevel.Information;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File(
                    path: logOptions.CombinedLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("ShopPOS application starting up. Database: {DbPath}", dbOptions.DbPath);

            // 4. Build DI Host
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    // Core services
                    services.AddCoreServices();

                    // Data services
                    services.AddDataServices(dbOptions);

                    // UI Navigation, Dialogs, ViewModels, and Views
                    services.AddUiServices();
                })
                .Build();

            await _host.StartAsync();

            // 5. Apply migrations and seed data
            using (var scope = _host.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Check pending migrations and create safe pre-migration backup
                var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
                if (pendingMigrations.Any())
                {
                    Log.Information("Pending migrations detected ({Count}): {Migrations}. Creating pre-migration database backup...",
                        pendingMigrations.Count, string.Join(", ", pendingMigrations));

                    foreach (var migration in pendingMigrations)
                    {
                        ShopPOS.Data.Services.DatabaseBackupHelper.CreatePreMigrationBackup(dbOptions.DbPath, migration);
                    }
                }

                Log.Information("Applying EF Core migrations...");
                await dbContext.Database.MigrateAsync();

                var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                Log.Information("Executing initial database seeding if required...");
                await seeder.SeedAsync();

                var settingRepo = scope.ServiceProvider.GetRequiredService<IShopSettingRepository>();
                var settings = await settingRepo.GetCurrentSettingsAsync();
                if (settings != null)
                {
                    ShopPOS.Core.Services.MoneyFormatter.Instance.UpdateSettings(settings.CurrencySymbol, settings.CurrencyDecimals);
                }
            }

            // 6. Launch Main Window directly (single-user mode, no login required)
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _currentMainWindow = mainWindow;
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal failure during application initialization.");
            MessageBox.Show(
                $"Application failed to start: {ex.Message}\n\nCheck log files in C:\\ShopData\\Logs for full details.",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    private void SetupGlobalExceptionHandling()
    {
        // UI thread unhandled exceptions
        DispatcherUnhandledException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI Dispatcher Exception.");
            MessageBox.Show(
                "An unexpected application error occurred.\n\nThe error has been recorded in the local log file.",
                "Shop POS - Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        // Non-UI thread unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled AppDomain Exception. Terminating: {IsTerminating}", args.IsTerminating);
                MessageBox.Show(
                    "A critical system error occurred. Details have been logged to C:\\ShopData\\Logs.",
                    "Shop POS - Critical System Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        // Async Task unobserved exceptions
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unobserved Task Exception.");
            args.SetObserved();
        };
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            Log.Information("ShopPOS shutting down.");
            await _host.StopAsync();
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
