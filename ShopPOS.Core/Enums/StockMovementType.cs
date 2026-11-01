namespace ShopPOS.Core.Enums;

/// <summary>
/// Specifies the type of stock movement in the inventory ledger.
/// </summary>
public enum StockMovementType
{
    Purchase = 1,
    Sale = 2,
    SaleReturn = 3,
    PurchaseReturn = 4,
    Adjustment = 5,
    Opening = 6,
    PurchaseCancel = 7,
    PurchaseReturnCancel = 8,
    AdjustmentCancel = 9,
    SaleCancel = 10,
    SaleReturnCancel = 11
}
