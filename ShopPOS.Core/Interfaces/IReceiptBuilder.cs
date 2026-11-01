using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IReceiptBuilder
{
    ReceiptDocument BuildFromSale(
        SaleDetailDto sale,
        ShopSetting settings,
        bool isDuplicate = false,
        int reprintCount = 0,
        decimal? customerPrevBalance = null,
        string? customerPhone = null,
        string? customerTaxNumber = null);

    ReceiptDocument BuildFromSaleReturn(
        SaleReturnDetailDto returnDetail,
        ShopSetting settings,
        bool isDuplicate = false);

    ReceiptDocument BuildFromShift(
        ShiftSummaryDto shift,
        ShopSetting settings,
        bool isZReport = false);

    ReceiptDocument BuildTestReceipt(ShopSetting settings);
}

