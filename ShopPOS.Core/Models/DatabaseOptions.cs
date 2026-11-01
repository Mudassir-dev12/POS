namespace ShopPOS.Core.Models;

/// <summary>
/// Configuration options for the SQLite database.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// File path to the SQLite database.
    /// Default: C:\ShopData\shop.db
    /// </summary>
    public string DbPath { get; set; } = @"C:\ShopData\shop.db";

    /// <summary>
    /// Gets the formatted SQLite connection string.
    /// </summary>
    public string ConnectionString => $"Data Source={DbPath}";
}

