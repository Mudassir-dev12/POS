using System;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs.Reports;

public class DateRangeDto
{
    public ReportDatePreset Preset { get; set; } = ReportDatePreset.Today;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public static DateRangeDto FromPreset(ReportDatePreset preset, DateTime? referenceTime = null, DateTime? customStart = null, DateTime? customEnd = null)
    {
        var now = referenceTime ?? DateTime.Now;
        var today = now.Date;

        switch (preset)
        {
            case ReportDatePreset.Today:
                return new DateRangeDto
                {
                    Preset = preset,
                    StartDate = today,
                    EndDate = today.AddDays(1).AddTicks(-1)
                };

            case ReportDatePreset.Yesterday:
                var yesterday = today.AddDays(-1);
                return new DateRangeDto
                {
                    Preset = preset,
                    StartDate = yesterday,
                    EndDate = today.AddTicks(-1)
                };

            case ReportDatePreset.ThisWeek:
                int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                var monday = today.AddDays(-diff);
                return new DateRangeDto
                {
                    Preset = preset,
                    StartDate = monday,
                    EndDate = monday.AddDays(7).AddTicks(-1)
                };

            case ReportDatePreset.ThisMonth:
                var firstDayThisMonth = new DateTime(today.Year, today.Month, 1);
                return new DateRangeDto
                {
                    Preset = preset,
                    StartDate = firstDayThisMonth,
                    EndDate = firstDayThisMonth.AddMonths(1).AddTicks(-1)
                };

            case ReportDatePreset.LastMonth:
                var firstDayLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                return new DateRangeDto
                {
                    Preset = preset,
                    StartDate = firstDayLastMonth,
                    EndDate = firstDayLastMonth.AddMonths(1).AddTicks(-1)
                };

            case ReportDatePreset.ThisYear:
                var firstDayThisYear = new DateTime(today.Year, 1, 1);
                return new DateRangeDto
                {
                    Preset = preset,
                    StartDate = firstDayThisYear,
                    EndDate = new DateTime(today.Year, 12, 31, 23, 59, 59, 999)
                };

            case ReportDatePreset.Custom:
            default:
                var start = (customStart ?? today).Date;
                var end = (customEnd ?? today).Date.AddDays(1).AddTicks(-1);
                if (start > end)
                {
                    var temp = start;
                    start = end.Date;
                    end = temp.AddDays(1).AddTicks(-1);
                }
                return new DateRangeDto
                {
                    Preset = ReportDatePreset.Custom,
                    StartDate = start,
                    EndDate = end
                };
        }
    }

    /// <summary>
    /// Gets the immediately preceding comparison period of the exact same length.
    /// </summary>
    public DateRangeDto GetPreviousPeriod()
    {
        var duration = EndDate - StartDate;
        var prevEnd = StartDate.AddTicks(-1);
        var prevStart = prevEnd - duration;
        return new DateRangeDto
        {
            Preset = Preset,
            StartDate = prevStart,
            EndDate = prevEnd
        };
    }
}

