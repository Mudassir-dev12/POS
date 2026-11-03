using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels;
using Xunit;

namespace ShopPOS.Tests;

public class MainWindowViewModelTests
{
    private readonly Mock<ICurrentUserProvider> _userProviderMock = new();
    private readonly Mock<IShopSettingService> _settingServiceMock = new();
    private readonly Mock<INavigationService> _navServiceMock = new();
    private readonly Mock<IThemeService> _themeServiceMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<ISaleRepository> _saleRepoMock = new();
    private readonly Mock<ISupplierService> _supplierServiceMock = new();
    private readonly Mock<IPurchaseRepository> _purchaseRepoMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public MainWindowViewModelTests()
    {
        _userProviderMock.Setup(u => u.FullName).Returns("Store Owner");
        _settingServiceMock.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(new ShopSetting { ShopName = "Test Shop" });
        _themeServiceMock.Setup(t => t.CurrentTheme).Returns("Light");
    }

    [Fact]
    public void Constructor_InitializesNavigationGroups_AndTheme()
    {
        var vm = new MainWindowViewModel(
            _userProviderMock.Object,
            _settingServiceMock.Object,
            _navServiceMock.Object,
            _themeServiceMock.Object,
            _productRepoMock.Object,
            _saleRepoMock.Object,
            _supplierServiceMock.Object,
            _purchaseRepoMock.Object,
            _dialogServiceMock.Object);

        Assert.Equal("Light", vm.CurrentTheme);
        Assert.NotEmpty(vm.NavigationGroups);
        Assert.Contains(vm.NavigationGroups, g => g.Header == "SELL");
        Assert.Contains(vm.NavigationGroups, g => g.Header == "STOCK");
        Assert.Contains(vm.NavigationGroups, g => g.Header == "BUYING");
        Assert.Contains(vm.NavigationGroups, g => g.Header == "MONEY");
        Assert.Contains(vm.NavigationGroups, g => g.Header == "SYSTEM");
    }

    [Fact]
    public void ToggleTheme_InvokesThemeServiceToggleTheme()
    {
        var vm = new MainWindowViewModel(
            _userProviderMock.Object,
            _settingServiceMock.Object,
            _navServiceMock.Object,
            _themeServiceMock.Object,
            _productRepoMock.Object,
            _saleRepoMock.Object,
            _supplierServiceMock.Object,
            _purchaseRepoMock.Object,
            _dialogServiceMock.Object);

        vm.ToggleThemeCommand.Execute(null);

        _themeServiceMock.Verify(t => t.ToggleTheme(), Times.Once);
    }

    [Fact]
    public void Navigate_NavigatesToKey_AndSetsSelectedItem()
    {
        var vm = new MainWindowViewModel(
            _userProviderMock.Object,
            _settingServiceMock.Object,
            _navServiceMock.Object,
            _themeServiceMock.Object,
            _productRepoMock.Object,
            _saleRepoMock.Object,
            _supplierServiceMock.Object,
            _purchaseRepoMock.Object,
            _dialogServiceMock.Object);

        var item = new NavigationItem("Products", "Products", "Box");
        vm.NavigateCommand.Execute(item);

        Assert.Equal(item, vm.SelectedNavigationItem);
        _navServiceMock.Verify(n => n.NavigateTo("Products"), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_SetsShopNameAndUser()
    {
        var vm = new MainWindowViewModel(
            _userProviderMock.Object,
            _settingServiceMock.Object,
            _navServiceMock.Object,
            _themeServiceMock.Object,
            _productRepoMock.Object,
            _saleRepoMock.Object,
            _supplierServiceMock.Object,
            _purchaseRepoMock.Object,
            _dialogServiceMock.Object);

        await vm.InitializeAsync();

        Assert.Equal("Test Shop", vm.ShopName);
        Assert.Equal("Store Owner", vm.CurrentUserName);
    }

    [Fact]
    public void SelectSearchResult_NavigatesToTarget_AndClosesSearch()
    {
        var vm = new MainWindowViewModel(
            _userProviderMock.Object,
            _settingServiceMock.Object,
            _navServiceMock.Object,
            _themeServiceMock.Object,
            _productRepoMock.Object,
            _saleRepoMock.Object,
            _supplierServiceMock.Object,
            _purchaseRepoMock.Object,
            _dialogServiceMock.Object);

        var resultItem = new GlobalSearchResultItem
        {
            Group = "Products",
            Title = "Cola 500ml",
            NavigationTarget = "Products",
            EntityId = 1
        };

        vm.IsSearchOpen = true;
        vm.GlobalSearchText = "cola";

        vm.SelectSearchResultCommand.Execute(resultItem);

        Assert.False(vm.IsSearchOpen);
        Assert.Empty(vm.GlobalSearchText);
        _navServiceMock.Verify(n => n.NavigateTo("Products"), Times.Once);
    }
}
