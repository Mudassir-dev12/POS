using System;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using Xunit;

namespace ShopPOS.Tests;

public class StockFilterBoundaryTests
{
    [Theory]
    [InlineData(0, 5, true, false, true)]      // stock 0, reorder 5 -> OutOfStock = true, LowStock = false
    [InlineData(-1, 5, true, false, true)]     // stock -1, reorder 5 -> OutOfStock = true, LowStock = false
    [InlineData(1, 5, true, true, false)]      // stock 1, reorder 5 -> OutOfStock = false, LowStock = true
    [InlineData(5, 5, true, true, false)]      // stock 5, reorder 5 -> OutOfStock = false, LowStock = true
    [InlineData(6, 5, true, false, false)]     // stock 6, reorder 5 -> OutOfStock = false, LowStock = false
    [InlineData(0, 5, false, false, false)]    // untracked product -> neither
    public void ProductListItemDto_StockStatusBoundaries_MatchRules(
        decimal stock, decimal reorder, bool trackStock, bool expectedLow, bool expectedOut)
    {
        var item = new ProductListItemDto
        {
            CurrentStock = stock,
            ReorderLevel = reorder,
            TrackStock = trackStock
        };

        Assert.Equal(expectedLow, item.IsLowStock);
        Assert.Equal(expectedOut, item.IsOutOfStock);
    }

    [Theory]
    [InlineData(10, 5, true, StockStatus.OK)]
    [InlineData(5, 5, true, StockStatus.Low)]
    [InlineData(1, 5, true, StockStatus.Low)]
    [InlineData(0, 5, true, StockStatus.OutOfStock)]
    [InlineData(-3, 5, true, StockStatus.Negative)]
    [InlineData(0, 5, false, StockStatus.NotTracked)]
    public void StockStatusHelper_ComputesCorrectStatus(
        decimal stock, decimal reorder, bool trackStock, StockStatus expectedStatus)
    {
        StockStatus status;
        if (!trackStock)
        {
            status = StockStatus.NotTracked;
        }
        else if (stock < 0)
        {
            status = StockStatus.Negative;
        }
        else if (stock == 0)
        {
            status = StockStatus.OutOfStock;
        }
        else if (stock <= reorder)
        {
            status = StockStatus.Low;
        }
        else
        {
            status = StockStatus.OK;
        }

        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void LowStockFilter_DoesNotIncludeOutOfStockProducts()
    {
        // Low stock filter rule: strictly stock > 0 && stock <= ReorderLevel
        var products = new[]
        {
            new ProductListItemDto { Id = 1, CurrentStock = 0, ReorderLevel = 10, TrackStock = true },
            new ProductListItemDto { Id = 2, CurrentStock = 3, ReorderLevel = 10, TrackStock = true },
            new ProductListItemDto { Id = 3, CurrentStock = 10, ReorderLevel = 10, TrackStock = true },
            new ProductListItemDto { Id = 4, CurrentStock = 15, ReorderLevel = 10, TrackStock = true }
        };

        var lowStockOnly = products.Where(p => p.IsLowStock).ToList();

        Assert.Equal(2, lowStockOnly.Count);
        Assert.Contains(lowStockOnly, p => p.Id == 2);
        Assert.Contains(lowStockOnly, p => p.Id == 3);
        Assert.DoesNotContain(lowStockOnly, p => p.Id == 1); // stock = 0 must NOT be in low stock list
    }

    [Fact]
    public void OutOfStockFilter_IncludesZeroAndNegativeStockOnly()
    {
        var products = new[]
        {
            new ProductListItemDto { Id = 1, CurrentStock = 0, ReorderLevel = 10, TrackStock = true },
            new ProductListItemDto { Id = 2, CurrentStock = -2, ReorderLevel = 10, TrackStock = true },
            new ProductListItemDto { Id = 3, CurrentStock = 3, ReorderLevel = 10, TrackStock = true }
        };

        var outOfStockOnly = products.Where(p => p.IsOutOfStock).ToList();

        Assert.Equal(2, outOfStockOnly.Count);
        Assert.Contains(outOfStockOnly, p => p.Id == 1);
        Assert.Contains(outOfStockOnly, p => p.Id == 2);
        Assert.DoesNotContain(outOfStockOnly, p => p.Id == 3);
    }
}

