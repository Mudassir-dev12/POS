using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs.Diagnostics;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Generates support zip archives and system diagnostic reports.
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>
    /// Builds a sanitized diagnostic report with schema versions and row counts.
    /// </summary>
    Task<DiagnosticsReportDto> GenerateReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a zip archive containing sanitized report and the last 7 days of log files.
    /// Never includes shop.db, credentials, or tokens.
    /// </summary>
    Task<string> CreateSupportBundleAsync(string? destinationZipPath = null, CancellationToken cancellationToken = default);
}

