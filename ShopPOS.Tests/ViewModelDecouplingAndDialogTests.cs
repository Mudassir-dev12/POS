using AwesomeAssertions;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Brands;
using ShopPOS.UI.ViewModels.Categories;
using ShopPOS.UI.ViewModels.Products;
using ShopPOS.UI.ViewModels.Suppliers;
using ShopPOS.UI.ViewModels.Units;

namespace ShopPOS.Tests;

public class ViewModelDecouplingAndDialogTests
{
    private static string FindSolutionDirectory()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "ShopPOS.sln")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir)!;
        }

        throw new DirectoryNotFoundException("Could not find directory containing ShopPOS.sln");
    }

    [Fact]
    public void ViewModels_MustNotContainOwnerAssignmentOrApplicationMainWindow()
    {
        // Arrange
        string solutionDir = FindSolutionDirectory();
        string viewModelsDir = Path.Combine(solutionDir, "ShopPOS.UI", "ViewModels");
        viewModelsDir.Should().NotBeNullOrEmpty();
        Directory.Exists(viewModelsDir).Should().BeTrue();

        var csFiles = Directory.GetFiles(viewModelsDir, "*.cs", SearchOption.AllDirectories);
        csFiles.Should().NotBeEmpty();

        var violations = new List<string>();

        // Act & Assert
        foreach (var file in csFiles)
        {
            string relativePath = Path.GetRelativePath(solutionDir, file);
            string content = File.ReadAllText(file);

            if (content.Contains(".Owner =") || content.Contains("Owner ="))
            {
                violations.Add($"{relativePath}: contains direct Owner assignment");
            }

            if (content.Contains("Application.Current.MainWindow"))
            {
                violations.Add($"{relativePath}: references Application.Current.MainWindow");
            }

            if (content.Contains("using System.Windows;"))
            {
                violations.Add($"{relativePath}: imports System.Windows");
            }
        }

        violations.Should().BeEmpty("ViewModels must not directly reference Window.Owner, Application.Current.MainWindow, or System.Windows");
    }

    [Fact]
    public async Task ProductsViewModel_AddAsync_ShouldInvokeDialogServiceShowProductDialog()
    {
        // Arrange
        var mockProductService = new Mock<IProductService>();
        var mockCategoryService = new Mock<ICategoryService>();
        var mockBrandService = new Mock<IBrandService>();
        var mockUnitService = new Mock<IUnitService>();
        var mockPriceLevelService = new Mock<IPriceLevelService>();
        var mockCsvService = new Mock<ICsvProductService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();

        mockCategoryService.Setup(c => c.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CategoryDto>>.Ok(new List<CategoryDto>()));
        mockBrandService.Setup(b => b.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<BrandDto>>.Ok(new List<BrandDto>()));
        mockBrandService.Setup(b => b.GetPagedBrandsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BrandDto>>.Ok(new PagedResult<BrandDto>(new List<BrandDto>(), 0, 1, 10)));
        mockUnitService.Setup(u => u.GetPagedUnitsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<UnitDto>>.Ok(new PagedResult<UnitDto>(new List<UnitDto>(), 0, 1, 10)));
        mockPriceLevelService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<PriceLevelDto>>.Ok(new List<PriceLevelDto>()));
        mockProductService.Setup(p => p.GenerateNextSkuAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok("P000001"));
        mockProductService.Setup(p => p.GetPagedProductsAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductListItemDto>>.Ok(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 50)));

        var vm = new ProductsViewModel(
            mockProductService.Object,
            mockCategoryService.Object,
            mockBrandService.Object,
            mockUnitService.Object,
            mockPriceLevelService.Object,
            mockCsvService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();

        // Act
        await vm.AddAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowProductDialog(It.IsAny<ProductDialogViewModel>()), Times.Once);
    }

    [Fact]
    public async Task ProductsViewModel_EditAsync_ShouldInvokeDialogServiceShowProductDialog()
    {
        // Arrange
        var mockProductService = new Mock<IProductService>();
        var mockCategoryService = new Mock<ICategoryService>();
        var mockBrandService = new Mock<IBrandService>();
        var mockUnitService = new Mock<IUnitService>();
        var mockPriceLevelService = new Mock<IPriceLevelService>();
        var mockCsvService = new Mock<ICsvProductService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();

        mockCategoryService.Setup(c => c.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CategoryDto>>.Ok(new List<CategoryDto>()));
        mockBrandService.Setup(b => b.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<BrandDto>>.Ok(new List<BrandDto>()));
        mockBrandService.Setup(b => b.GetPagedBrandsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BrandDto>>.Ok(new PagedResult<BrandDto>(new List<BrandDto>(), 0, 1, 10)));
        mockUnitService.Setup(u => u.GetPagedUnitsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<UnitDto>>.Ok(new PagedResult<UnitDto>(new List<UnitDto>(), 0, 1, 10)));
        mockPriceLevelService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<PriceLevelDto>>.Ok(new List<PriceLevelDto>()));
        mockProductService.Setup(p => p.GetProductByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDto>.Ok(new ProductDto { Id = 10, Name = "Test Product", Sku = "SKU-10" }));
        mockProductService.Setup(p => p.GetPagedProductsAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductListItemDto>>.Ok(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 50)));

        var vm = new ProductsViewModel(
            mockProductService.Object,
            mockCategoryService.Object,
            mockBrandService.Object,
            mockUnitService.Object,
            mockPriceLevelService.Object,
            mockCsvService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();
        vm.SelectedItem = new ProductListItemDto { Id = 10, Name = "Test Product", Sku = "SKU-10" };

        // Act
        await vm.EditAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowProductDialog(It.IsAny<ProductDialogViewModel>()), Times.Once);
    }

    [Fact]
    public async Task ProductsViewModel_DeleteReferencedProduct_ShouldPromptConfirmDialogAndDeactivate()
    {
        // Arrange
        var mockProductService = new Mock<IProductService>();
        var mockCategoryService = new Mock<ICategoryService>();
        var mockBrandService = new Mock<IBrandService>();
        var mockUnitService = new Mock<IUnitService>();
        var mockPriceLevelService = new Mock<IPriceLevelService>();
        var mockCsvService = new Mock<ICsvProductService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();

        mockCategoryService.Setup(c => c.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CategoryDto>>.Ok(new List<CategoryDto>()));
        mockBrandService.Setup(b => b.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<BrandDto>>.Ok(new List<BrandDto>()));
        mockBrandService.Setup(b => b.GetPagedBrandsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BrandDto>>.Ok(new PagedResult<BrandDto>(new List<BrandDto>(), 0, 1, 10)));

        // Delete returns REFERENCED_CANNOT_DELETE
        mockProductService.Setup(p => p.DeleteProductAsync(10, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("REFERENCED_CANNOT_DELETE"));

        // User chooses Deactivate in dialog
        mockDialogService.Setup(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true))
            .Returns(ConfirmActionResult.Deactivate);

        mockProductService.Setup(p => p.DeactivateProductAsync(10, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        mockProductService.Setup(p => p.GetPagedProductsAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductListItemDto>>.Ok(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 50)));

        var vm = new ProductsViewModel(
            mockProductService.Object,
            mockCategoryService.Object,
            mockBrandService.Object,
            mockUnitService.Object,
            mockPriceLevelService.Object,
            mockCsvService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();
        vm.SelectedItem = new ProductListItemDto { Id = 10, Name = "Referenced Product", Sku = "REF-10" };

        // Act
        await vm.DeleteOrDeactivateAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
        mockProductService.Verify(p => p.DeactivateProductAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductsViewModel_ImportCsvAsync_ShouldInvokeDialogServiceShowCsvImportWizard()
    {
        // Arrange
        var mockProductService = new Mock<IProductService>();
        var mockCategoryService = new Mock<ICategoryService>();
        var mockBrandService = new Mock<IBrandService>();
        var mockUnitService = new Mock<IUnitService>();
        var mockPriceLevelService = new Mock<IPriceLevelService>();
        var mockCsvService = new Mock<ICsvProductService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();

        mockCategoryService.Setup(c => c.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CategoryDto>>.Ok(new List<CategoryDto>()));
        mockBrandService.Setup(b => b.GetFlatListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<BrandDto>>.Ok(new List<BrandDto>()));
        mockBrandService.Setup(b => b.GetPagedBrandsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BrandDto>>.Ok(new PagedResult<BrandDto>(new List<BrandDto>(), 0, 1, 10)));

        var vm = new ProductsViewModel(
            mockProductService.Object,
            mockCategoryService.Object,
            mockBrandService.Object,
            mockUnitService.Object,
            mockPriceLevelService.Object,
            mockCsvService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();

        // Act
        await vm.ImportCsvAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowCsvImportWizardDialog(It.IsAny<CsvImportWizardViewModel>()), Times.Once);
    }

    [Fact]
    public async Task BrandsViewModel_DeleteReferencedBrand_ShouldPromptConfirmDialogAndDeactivate()
    {
        // Arrange
        var mockBrandService = new Mock<IBrandService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();


        mockBrandService.Setup(b => b.DeleteBrandAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("REFERENCED_CANNOT_DELETE"));

        mockDialogService.Setup(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true))
            .Returns(ConfirmActionResult.Deactivate);

        mockBrandService.Setup(b => b.DeactivateBrandAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        mockBrandService.Setup(b => b.GetPagedBrandsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<BrandDto>>.Ok(new PagedResult<BrandDto>(new List<BrandDto>(), 0, 1, 50)));

        var vm = new BrandsViewModel(
            mockBrandService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();
        vm.SelectedItem = new BrandDto { Id = 5, Name = "In-Use Brand" };

        // Act
        await vm.DeleteOrDeactivateAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
        mockBrandService.Verify(b => b.DeactivateBrandAsync(5, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoriesViewModel_DeleteReferencedCategory_ShouldPromptConfirmDialogAndDeactivate()
    {
        // Arrange
        var mockCategoryService = new Mock<ICategoryService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();


        mockCategoryService.Setup(c => c.DeleteCategoryAsync(3, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("REFERENCED_CANNOT_DELETE"));

        mockDialogService.Setup(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true))
            .Returns(ConfirmActionResult.Deactivate);

        mockCategoryService.Setup(c => c.DeactivateCategoryAsync(3, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        mockCategoryService.Setup(c => c.GetCategoryTreeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CategoryDto>>.Ok(new List<CategoryDto>()));

        var vm = new CategoriesViewModel(
            mockCategoryService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();
        vm.SelectedItem = new CategoryDto { Id = 3, Name = "In-Use Category" };

        // Act
        await vm.DeleteOrDeactivateAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
        mockCategoryService.Verify(c => c.DeactivateCategoryAsync(3, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnitsViewModel_DeleteReferencedUnit_ShouldPromptConfirmDialogAndDeactivate()
    {
        // Arrange
        var mockUnitService = new Mock<IUnitService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();


        mockUnitService.Setup(u => u.DeleteUnitAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("REFERENCED_CANNOT_DELETE"));

        mockDialogService.Setup(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true))
            .Returns(ConfirmActionResult.Deactivate);

        mockUnitService.Setup(u => u.DeactivateUnitAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        mockUnitService.Setup(u => u.GetPagedUnitsAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<UnitDto>>.Ok(new PagedResult<UnitDto>(new List<UnitDto>(), 0, 1, 50)));

        var vm = new UnitsViewModel(
            mockUnitService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();
        vm.SelectedItem = new UnitDto { Id = 2, Name = "Kilogram" };

        // Act
        await vm.DeleteOrDeactivateAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
        mockUnitService.Verify(u => u.DeactivateUnitAsync(2, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuppliersViewModel_DeleteReferencedSupplier_ShouldPromptConfirmDialogAndDeactivate()
    {
        // Arrange
        var mockSupplierService = new Mock<ISupplierService>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();


        mockSupplierService.Setup(s => s.DeleteSupplierAsync(4, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("REFERENCED_CANNOT_DELETE"));

        mockDialogService.Setup(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true))
            .Returns(ConfirmActionResult.Deactivate);

        mockSupplierService.Setup(s => s.DeactivateSupplierAsync(4, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        mockSupplierService.Setup(s => s.GetPagedSuppliersAsync(It.IsAny<MasterDataSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<SupplierDto>>.Ok(new PagedResult<SupplierDto>(new List<SupplierDto>(), 0, 1, 50)));

        var vm = new SuppliersViewModel(
            mockSupplierService.Object,
            mockShopSettingService.Object,
            mockDialogService.Object);

        await vm.InitializeAsync();
        vm.SelectedItem = new SupplierDto { Id = 4, Name = "Active Supplier" };

        // Act
        await vm.DeleteOrDeactivateAsync();

        // Assert
        mockDialogService.Verify(d => d.ShowConfirmActionDialog(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
        mockSupplierService.Verify(s => s.DeactivateSupplierAsync(4, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    }
