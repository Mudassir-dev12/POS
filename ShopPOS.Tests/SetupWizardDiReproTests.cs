using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopPOS.Core;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Setup;
using Xunit;

namespace ShopPOS.Tests;

public class SetupWizardDiReproTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly AppPaths _appPaths;
    private readonly IHost _host;

    public SetupWizardDiReproTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "ShopPOS_DiRepro_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        _appPaths = new AppPaths(_tempFolder);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IAppPaths>(_appPaths);
                services.AddCoreServices();
                services.AddDataServices(new DatabaseOptions { DbPath = _appPaths.DatabasePath });
                services.AddScoped<IDialogService, DialogService>();
            })
            .Build();
    }

    public void Dispose()
    {
        _host.Dispose();
        try
        {
            if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
        }
        catch { }
    }

    [Fact]
    public async Task FinishAsync_WithScopeFactory_StartEmpty_InitializesDatabaseWithoutDisposedContextException()
    {
        var scopeFactory = _host.Services.GetRequiredService<IServiceScopeFactory>();

        var wizardVm = new SetupWizardViewModel(
            _appPaths,
            _host.Services.GetRequiredService<IUserSettingsService>(),
            _host.Services.GetRequiredService<IDialogService>(),
            scopeFactory: scopeFactory);

        wizardVm.CurrentStepIndex = 6;
        wizardVm.ShopName = "Clean Install Mart";
        wizardVm.Address = "Bara Market, Block H";
        wizardVm.Phone = "0335-5558359";
        wizardVm.StartingData = StartingDataChoice.StartEmpty;

        // Finish execution
        await wizardVm.FinishAsync();

        Assert.Null(wizardVm.ErrorMessage);
        Assert.True(wizardVm.CompletedSuccessfully);

        // Verify database and settings exist and are accessible via a new scope
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var setting = await db.ShopSettings.FirstOrDefaultAsync();
            Assert.NotNull(setting);
            Assert.Equal("Clean Install Mart", setting.ShopName);
            Assert.Equal("Bara Market, Block H", setting.Address);
            Assert.Equal("0335-5558359", setting.Phone);
        }
    }

    [Fact]
    public async Task FinishAsync_CanBeCalledRepeatedly_WithoutThrowingDisposedContextException()
    {
        var scopeFactory = _host.Services.GetRequiredService<IServiceScopeFactory>();

        var wizardVm = new SetupWizardViewModel(
            _appPaths,
            _host.Services.GetRequiredService<IUserSettingsService>(),
            _host.Services.GetRequiredService<IDialogService>(),
            scopeFactory: scopeFactory);

        wizardVm.CurrentStepIndex = 6;
        wizardVm.ShopName = "Clean Install Mart";
        wizardVm.StartingData = StartingDataChoice.StartEmpty;

        // Call once
        await wizardVm.FinishAsync();
        Assert.Null(wizardVm.ErrorMessage);
        Assert.True(wizardVm.CompletedSuccessfully);

        // Call again (simulate retry / double click)
        await wizardVm.FinishAsync();
        Assert.Null(wizardVm.ErrorMessage);
        Assert.True(wizardVm.CompletedSuccessfully);
    }
}
