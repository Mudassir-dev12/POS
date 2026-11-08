using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class AuditLogRowDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public string? Username { get; set; }
}

public class AuditLogFilterDto
{
    public DateRangeDto DateRange { get; set; } = new();
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public int PageIndex { get; set; } = 0;
    public int PageSize { get; set; } = 50;
}

public class AuditLogReportDto
{
    public List<AuditLogRowDto> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}

