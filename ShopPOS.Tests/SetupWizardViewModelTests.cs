using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Seeding;
using ShopPOS.Data.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Setup;
using ShopPOS.UI.Views.Setup;
using Xunit;

namespace ShopPOS.Tests;

public class SetupWizardViewModelTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly AppPaths _appPaths;
    private readonly UserSettingsService _userSettingsService;
    private readonly IDialogService _dialogService;

    public SetupWizardViewModelTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "ShopPOS_WizardTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);

        _appPaths = new AppPaths(_tempFolder);
        _userSettingsService = new UserSettingsService(Path.Combine(_tempFolder, "appsettings.user.json"));
        _dialogService = new Mock<IDialogService>().Object;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void InitialState_StartsAtStepZero_WelcomeStep()
    {
        var vm = new SetupWizardViewModel(_appPaths, _userSettingsService, _dialogService);

        Assert.Equal(0, vm.CurrentStepIndex);
        Assert.True(vm.IsWelcomeStep);
        Assert.False(vm.CanGoBack);
        Assert.True(vm.CanGoNext);
        Assert.False(vm.CanFinish);
        Assert.Equal(7, vm.TotalSteps);
        Assert.Equal("Welcome to ShopPOS", vm.StepTitle);
    }

    [Fact]
    public void Navigation_StepValidation_RequiresShopNameAtProfileStep()
    {
        var vm = new SetupWizardViewModel(_appPaths, _userSettingsService, _dialogService);

        // Step 0 -> Step 1
        vm.NextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentStepIndex);
        Assert.True(vm.IsProfileStep);

        // Blank out shop name and attempt to proceed
        vm.ShopName = "   ";
        vm.NextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentStepIndex); // Blocked
        Assert.NotNull(vm.ErrorMessage);

        // Provide valid shop name and proceed
        vm.ShopName = "Super Mart";
        vm.NextCommand.Execute(null);
        Assert.Equal(2, vm.CurrentStepIndex); // Allowed to Step 2 (Currency & Tax)
    }

    [Fact]
    public void StepTransitions_BackAndNext_TraversesProperly()
    {
        var vm = new SetupWizardViewModel(_appPaths, _userSettingsService, _dialogService);

        // Step 0 -> Step 1 -> Step 2
        vm.NextCommand.Execute(null);
        vm.ShopName = "Quick Shop";
        vm.NextCommand.Execute(null);
        Assert.Equal(2, vm.CurrentStepIndex);

        // Back -> Step 1 -> Step 0
        vm.BackCommand.Execute(null);
        Assert.Equal(1, vm.CurrentStepIndex);
        vm.BackCommand.Execute(null);
        Assert.Equal(0, vm.CurrentStepIndex);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void DataFolderStep_ValidatesWritability()
    {
        var vm = new SetupWizardViewModel(_appPaths, _userSettingsService, _dialogService);

        // Navigate to Step 4 (Data folder)
        vm.CurrentStepIndex = 4;
        Assert.True(vm.IsDataFolderStep);

        // Point to our valid temp folder
        vm.DataDirectory = _tempFolder;
        Assert.True(vm.IsDataFolderValid);

        // Empty directory is invalid
        vm.DataDirectory = "";
        Assert.False(vm.IsDataFolderValid);
    }

    [Fact]
    public async Task FinishCommand_SeedsStoreSettingsAndInitializesDatabase()
    {
        var dbPath = Path.Combine(_tempFolder, "shop.db");
        var connectionString = $"Data Source={dbPath};Foreign Keys=True;";

        AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new AppDbContext(options);
        }

        // Apply migrations first to create schema and default settings
        using (var context = CreateDbContext())
        {
            await context.Database.MigrateAsync();
            var seeder = new DataSeeder(context);
            await seeder.SeedAsync();
        }

        var vm = new SetupWizardViewModel(
            _appPaths,
            _userSettingsService,
            _dialogService,
            dbContextFactory: CreateDbContext);

        vm.ShopName = "Apex Retail";
        vm.CurrencySymbol = "Rs";
        vm.CurrencyDecimals = 2;
        vm.Address = "123 Main Street";
        vm.StartingData = StartingDataChoice.StartEmpty;

        await vm.FinishAsync();

        Assert.True(vm.CompletedSuccessfully);

        // Verify shop settings were persisted
        using (var verifyContext = CreateDbContext())
        {
            var settings = await verifyContext.ShopSettings.FirstOrDefaultAsync();
            Assert.NotNull(settings);
            Assert.Equal("Apex Retail", settings.ShopName);
            Assert.Equal("123 Main Street", settings.Address);
            Assert.Equal("Rs", settings.CurrencySymbol);
            Assert.Equal(2, settings.CurrencyDecimals);
        }
    }

    [Fact]
    public void SetupWizardWindow_InstantiatesAndRenders()
    {
        StaTestHelper.RunOnSta(() =>
        {
            var vm = new SetupWizardViewModel(_appPaths, _userSettingsService, _dialogService);
            var window = new SetupWizardWindow(vm);
            window.Show();
            window.Close();
        });
    }
    [Fact]
    public void FinalStep_FinishButton_IsEnabledAndExecutable()
    {
        var vm = new SetupWizardViewModel(_appPaths, _userSettingsService, _dialogService);

        // Initially on Step 0: CanFinish is false
        Assert.Equal(0, vm.CurrentStepIndex);
        Assert.False(vm.CanFinish);

        // Move to Step 6 (Final Step 7 in UI)
        vm.CurrentStepIndex = 6;
        Assert.True(vm.IsStartingDataStep);
        Assert.True(vm.CanFinish);
        Assert.True(vm.FinishCommand.CanExecute(null));

        // When IsBusy is true, CanFinish becomes false to guard against double click
        vm.IsBusy = true;
        Assert.False(vm.CanFinish);

        vm.IsBusy = false;
        Assert.True(vm.CanFinish);
    }
}
