namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Interface implemented by list DTOs to support multi-select and bulk operations.
/// </summary>
public interface ISelectable
{
    int Id { get; }
    bool IsSelected { get; set; }
}

