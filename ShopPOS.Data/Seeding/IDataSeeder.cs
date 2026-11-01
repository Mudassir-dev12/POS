namespace ShopPOS.Data.Seeding;

/// <summary>
/// Seeds initial system reference data and the default administrator user.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Executes database seeding if initial records are absent.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}

