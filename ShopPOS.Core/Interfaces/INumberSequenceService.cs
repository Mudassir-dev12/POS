namespace ShopPOS.Core.Interfaces;

public interface INumberSequenceService
{
    /// <summary>
    /// Generates the next sequential number (e.g. PUR-000001, SPAY-000001, PRET-000001) atomically in a transaction.
    /// </summary>
    Task<string> GetNextNumberAsync(string prefix, CancellationToken cancellationToken = default);
}

