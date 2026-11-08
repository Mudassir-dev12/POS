using System;
using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs.DataTools;

namespace ShopPOS.Core.Interfaces;

public interface IDataToolsService
{
    Task<DataCountsDto> GetCountsAsync(CancellationToken cancellationToken = default);
    Task<DataToolResultDto> ClearTransactionsAsync(string? backupDirectory = null, CancellationToken cancellationToken = default);
    Task<DataToolResultDto> ClearEverythingAsync(string? backupDirectory = null, CancellationToken cancellationToken = default);
    Task<DataToolResultDto> ClearAuditLogOnlyAsync(string? backupDirectory = null, CancellationToken cancellationToken = default);
    Task<DemoDataResultDto> LoadDemoDataAsync(string? backupDirectory = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

