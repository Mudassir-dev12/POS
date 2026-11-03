using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Services;
using ShopPOS.UI.Views.Dialogs;
using Xunit;

namespace ShopPOS.Tests;

public class BulkOperationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IAuditLogService> _auditLogMock;

    public BulkOperationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"shoppos_bulk_{Guid.NewGuid():N}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath};Foreign Keys=True");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();

        _auditLogMock = new Mock<IAuditLogService>();
    }

    [Fact]
    public async Task BulkDeleteAsync_UnreferencedProducts_SoftDeletesAndLogsAudit()
    {
        var unit = new Unit { Name = "Piece", ShortName = "pc", IsActive = true };
        var category = new Category { Name = "Snacks", IsActive = true };
        _context.Units.Add(unit);
        _context.Categories.Add(category);
        _context.SaveChanges();

        var p1 = new Product { Sku = "SKU-DEL-1", Name = "Unused 1", UnitId = unit.Id, CategoryId = category.Id, IsActive = true };
        var p2 = new Product { Sku = "SKU-DEL-2", Name = "Unused 2", UnitId = unit.Id, CategoryId = category.Id, IsActive = true };
        _context.Products.AddRange(p1, p2);
        _context.SaveChanges();

        var productRepo = new ProductRepository(_context);
        var productService = new ProductService(
            productRepo,
            new Mock<IShopSettingRepository>().Object,
            _auditLogMock.Object);

        var result = await productService.BulkDeleteAsync(new[] { p1.Id, p2.Id }, userId: 1);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.SkippedCount);

        // Verify soft deleted in DB (global filter hides soft-deleted)
        var remaining = await _context.Products.Where(p => p.Id == p1.Id || p.Id == p2.Id).ToListAsync();
        Assert.Empty(remaining);

        // Verify audit log was recorded
        _auditLogMock.Verify(a => a.LogAsync(1, "DeleteProduct", "Product", p1.Id, It.IsAny<string>()), Times.Once);
        _auditLogMock.Verify(a => a.LogAsync(1, "DeleteProduct", "Product", p2.Id, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task FilteredUniqueIndex_AllowsReusingSkuAndBarcodeAfterSoftDelete()
    {
        var unit = new Unit { Name = "Piece", ShortName = "pc", IsActive = true };
        var category = new Category { Name = "Snacks", IsActive = true };
        _context.Units.Add(unit);
        _context.Categories.Add(category);
        _context.SaveChanges();

        // 1. Create product with SKU-REUSE and Barcode 9988776655
        var p1 = new Product
        {
            Sku = "SKU-REUSE",
            Barcode = "9988776655",
            Name = "Original Product",
            UnitId = unit.Id,
            CategoryId = category.Id,
            IsActive = true
        };
        _context.Products.Add(p1);
        _context.SaveChanges();

        // 2. Soft-delete p1
        p1.IsDeleted = true;
        _context.SaveChanges();

        // 3. Create a NEW product reusing the exact same SKU and Barcode
        var p2 = new Product
        {
            Sku = "SKU-REUSE",
            Barcode = "9988776655",
            Name = "Reused Product",
            UnitId = unit.Id,
            CategoryId = category.Id,
            IsActive = true
        };
        _context.Products.Add(p2);

        // This must succeed without SQLite unique constraint violation because WHERE IsDeleted = 0!
        var ex = Record.Exception(() => _context.SaveChanges());
        Assert.Null(ex);
        Assert.True(p2.Id > 0);
    }

    [Fact]
    public async Task BulkDeleteAsync_ReferencedProducts_SkipsAndOffersDeactivate()
    {
        var unit = new Unit { Name = "Piece", ShortName = "pc", IsActive = true };
        var category = new Category { Name = "Snacks", IsActive = true };
        _context.Units.Add(unit);
        _context.Categories.Add(category);
        _context.SaveChanges();

        var p = new Product { Sku = "SKU-REF", Name = "Referenced Item", UnitId = unit.Id, CategoryId = category.Id, IsActive = true };
        _context.Products.Add(p);
        _context.SaveChanges();

        // Add a stock movement referencing the product
        var sm = new StockMovement
        {
            ProductId = p.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 10m,
            UnitCost = 50m,
            CreatedAt = DateTime.UtcNow
        };
        _context.StockMovements.Add(sm);
        _context.SaveChanges();

        var productRepo = new ProductRepository(_context);
        var productService = new ProductService(
            productRepo,
            new Mock<IShopSettingRepository>().Object,
            _auditLogMock.Object);

        var result = await productService.BulkDeleteAsync(new[] { p.Id }, userId: 1);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.False(result.Items[0].Success);
        Assert.True(result.Items[0].CanDeactivateInstead);
        Assert.Contains("in use", result.Items[0].Message, StringComparison.OrdinalIgnoreCase);

        // Deactivate instead
        var deactResult = await productService.BulkDeactivateAsync(new[] { p.Id }, userId: 1);
        Assert.Equal(1, deactResult.SuccessCount);

        var updated = await _context.Products.FindAsync(p.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task BulkConfirmDialog_OverFiftyItems_RequiresTypedConfirmation()
    {
        // STA test for UI dialog
        var tcs = new TaskCompletionSource<bool>();
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var dialog = new BulkConfirmDialog("Delete", count: 75, requireTypedConfirmation: true);
                // Confirm button must be disabled until "DELETE" is typed
                Assert.False(dialog.IsConfirmEnabled);

                dialog.SetConfirmationInput("delete");
                Assert.True(dialog.IsConfirmEnabled);

                dialog.SetConfirmationInput("wrong");
                Assert.False(dialog.IsConfirmEnabled);

                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        await tcs.Task;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { }
    }
}
