using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShopPOS.Core.Interfaces;

public record SelfCheckItemResult(
    string Name,
    bool Passed,
    string Details,
    string? FailureReason = null
);

public class SelfCheckReport
{
    public bool AllPassed => Items.Count > 0 && Items.All(i => i.Passed);
    public List<SelfCheckItemResult> Items { get; set; } = new();
    public DateTime RunAt { get; set; } = DateTime.Now;
    public string FormattedReport { get; set; } = string.Empty;
}

public interface ISelfCheckService
{
    Task<SelfCheckReport> RunAllChecksAsync(CancellationToken cancellationToken = default);
}

