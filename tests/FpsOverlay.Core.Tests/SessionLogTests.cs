using System.Globalization;
using FpsOverlay.Core.Logging;

namespace FpsOverlay.Core.Tests;

public class SessionLogTests
{
    [Fact]
    public void Header_lists_all_columns()
    {
        Assert.Equal("Time,Fps,OnePercentLow,GpuLoad,GpuTemp,CpuLoad,CpuTemp,RamUsedGb", SessionLog.Header);
    }

    [Fact]
    public void Row_formats_values()
    {
        var time = new DateTimeOffset(2026, 11, 3, 20, 15, 7, TimeSpan.FromHours(7));
        var s = new SensorSnapshot(GpuLoad: 67.4, GpuTemp: 71.6, CpuLoad: 34, CpuTemp: 65, RamUsedGb: 9.234);
        Assert.Equal("2026-11-03T20:15:07+07:00,87.3,61.0,67,72,34,65,9.23",
            SessionLog.FormatRow(time, 87.349, 61.04, s));
    }

    [Fact]
    public void Row_with_nulls_has_empty_cells()
    {
        var time = new DateTimeOffset(2026, 11, 3, 20, 15, 7, TimeSpan.Zero);
        Assert.Equal("2026-11-03T20:15:07+00:00,,,,,,,", SessionLog.FormatRow(time, null, null, SensorSnapshot.Empty));
    }

    [Fact]
    public void Row_uses_dot_decimal_under_vi_VN()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
            var row = SessionLog.FormatRow(DateTimeOffset.UnixEpoch, 60.5, 50.3, new SensorSnapshot(null, null, null, null, 8.5));
            Assert.EndsWith(",60.5,50.3,,,,,8.50", row);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void File_stem_is_sanitized()
    {
        Assert.Equal("Lies_of_P__2026-11-03_20-15", SessionLog.FileStem("Lies of P:", new DateTime(2026, 11, 3, 20, 15, 59)));
        Assert.Equal("Game_2026-11-03_20-15", SessionLog.FileStem("", new DateTime(2026, 11, 3, 20, 15, 0)));
    }

    [Fact]
    public void Unique_path_adds_suffix()
    {
        var dir = Path.Combine("logs");
        var taken = new HashSet<string> { Path.Combine(dir, "a.csv"), Path.Combine(dir, "a_2.csv") };
        Assert.Equal(Path.Combine(dir, "b.csv"), SessionLog.UniquePath(dir, "b", ".csv", taken.Contains));
        Assert.Equal(Path.Combine(dir, "a_3.csv"), SessionLog.UniquePath(dir, "a", ".csv", taken.Contains));
    }

    [Fact]
    public void Summary_path_sits_next_to_csv()
    {
        Assert.Equal(Path.Combine("logs", "Lies_of_P__2026-11-03_20-15_2_summary.txt"),
            SessionLog.SummaryPath(Path.Combine("logs", "Lies_of_P__2026-11-03_20-15_2.csv")));
    }

    [Fact]
    public void Unique_path_can_also_avoid_existing_summary()
    {
        var dir = "logs";
        var taken = new HashSet<string> { SessionLog.SummaryPath(Path.Combine(dir, "a.csv")) };
        Assert.Equal(Path.Combine(dir, "a_2.csv"),
            SessionLog.UniquePath(dir, "a", ".csv", p => taken.Contains(p) || taken.Contains(SessionLog.SummaryPath(p))));
    }}