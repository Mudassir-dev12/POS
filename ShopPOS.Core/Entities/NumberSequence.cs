namespace ShopPOS.Core.Entities;

/// <summary>
/// Maintains transaction-safe, atomic auto-incrementing numbers for documents (e.g. PUR-, SPAY-, PRET-).
/// </summary>
public class NumberSequence
{
    public string Key { get; set; } = string.Empty;
    public long LastNumber { get; set; }
}

