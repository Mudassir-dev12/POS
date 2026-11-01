namespace ShopPOS.Core.Models;

/// <summary>
/// Configuration options for file logging.
/// </summary>
public class LoggingOptions
{
    public const string SectionName = "LoggingOptions";

    /// <summary>
    /// Directory where Serilog rolling log files are saved.
    /// Default: C:\ShopData\Logs
    /// </summary>
    public string LogDirectory { get; set; } = @"C:\ShopData\Logs";

    /// <summary>
    /// Rolling log file pattern.
    /// Default: app-.log
    /// </summary>
    public string LogFileName { get; set; } = "app-.log";

    /// <summary>
    /// Minimum logging level (e.g. Information, Debug, Warning, Error).
    /// Default: Information
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Gets the full combined path pattern for Serilog rolling file sink.
    /// </summary>
    public string CombinedLogPath => System.IO.Path.Combine(LogDirectory, LogFileName);
}

