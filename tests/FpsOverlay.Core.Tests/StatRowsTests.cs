using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public class StatRowsTests
{
    private static readonly SensorSnapshot Snap = new(66.6, 70.6, 30, 50, 9.2);

    [Fact]
    public void HasUnit_is_true_only_for_clock_power_fan() =>
        Assert.Equal(
            new[] { StatRow.GpuClock, StatRow.GpuPower, StatRow.GpuFan },
            Enum.GetValues<StatRow>().Where(StatRows.HasUnit).ToArray());

    [Fact]
    public void Order_matches_spec() => Assert.Equal(
        [StatRow.OnePercentLow, StatRow.Gpu, StatRow.GpuMemory, StatRow.GpuClock, StatRow.GpuPower, StatRow.GpuFan, StatRow.Cpu, StatRow.Ram],
        StatRows.Order);

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public void Gpu_row_visible_if_load_or_temp(bool load, bool temp, bool visible)
    {
        var s = new OverlaySettings { ShowGpuLoad = load, ShowGpuTemp = temp };
        Assert.Equal(visible, StatRows.IsVisible(StatRow.Gpu, s));
    }

    [Fact]
    public void Gpu_both_shown_gives_load_then_temp()
    {
        var s = new OverlaySettings();
        Assert.Equal(("67%", "71°"), StatRows.Values(StatRow.Gpu, null, Snap, s, OverlayLayout.Horizontal));
        Assert.Equal(2, StatRows.TempCell(StatRow.Gpu, s));
    }

    [Fact]
    public void Gpu_temp_only_goes_in_first_cell()
    {
        var s = new OverlaySettings { ShowGpuLoad = false };
        Assert.Equal(("71°", ""), StatRows.Values(StatRow.Gpu, null, Snap, s, OverlayLayout.Vertical));
        Assert.Equal(1, StatRows.TempCell(StatRow.Gpu, s));
    }

    [Fact]
    public void Missing_values_show_dashes() =>
        Assert.Equal(("--", "--"), StatRows.Values(StatRow.Gpu, null, SensorSnapshot.Empty, new OverlaySettings(), OverlayLayout.Horizontal));

    [Fact]
    public void Labels_are_fixed()
    {
        Assert.Equal("GPU", StatRows.Label(StatRow.Gpu));
        Assert.Equal("VRAM", StatRows.Label(StatRow.GpuMemory));
        Assert.Equal("CLK", StatRows.Label(StatRow.GpuClock));
        Assert.Equal("PWR", StatRows.Label(StatRow.GpuPower));
        Assert.Equal("FAN", StatRows.Label(StatRow.GpuFan));
        Assert.Equal("1%", StatRows.Label(StatRow.OnePercentLow));
    }

    [Fact]
    public void Single_cell_rows_and_vertical_units()
    {
        var s = new OverlaySettings();
        Assert.Equal(("9.2G", ""), StatRows.Values(StatRow.Ram, null, Snap, s, OverlayLayout.Horizontal));
        Assert.Equal(("--", "MHz"), StatRows.Values(StatRow.GpuClock, null, Snap, s, OverlayLayout.Vertical));
        Assert.Equal(("--", ""), StatRows.Values(StatRow.GpuClock, null, Snap, s, OverlayLayout.Horizontal));
        Assert.Equal(0, StatRows.TempCell(StatRow.Ram, s));
    }

    private static readonly SensorSnapshot Gpu4 = new(66.6, 70.6, 30, 50, 9.2,
        GpuMemoryUsedGb: 3.1, GpuClockMhz: 1589.6, GpuPowerW: 47.5, GpuFanRpm: 2400);

    [Fact]
    public void Clock_row_horizontal_and_vertical()
    {
        var s = new OverlaySettings();
        Assert.Equal(("1590MHz", ""), StatRows.Values(StatRow.GpuClock, null, Gpu4, s, OverlayLayout.Horizontal));
        Assert.Equal(("1590", "MHz"), StatRows.Values(StatRow.GpuClock, null, Gpu4, s, OverlayLayout.Vertical));
        Assert.Equal(("--", "MHz"), StatRows.Values(StatRow.GpuClock, null, SensorSnapshot.Empty, s, OverlayLayout.Vertical));
    }

    [Fact]
    public void Power_and_fan_rows()
    {
        var s = new OverlaySettings();
        Assert.Equal(("48W", ""), StatRows.Values(StatRow.GpuPower, null, Gpu4, s, OverlayLayout.Horizontal));
        Assert.Equal(("48", "W"), StatRows.Values(StatRow.GpuPower, null, Gpu4, s, OverlayLayout.Vertical));
        Assert.Equal(("2400RPM", ""), StatRows.Values(StatRow.GpuFan, null, Gpu4, s, OverlayLayout.Horizontal));
        Assert.Equal(("2400", "RPM"), StatRows.Values(StatRow.GpuFan, null, Gpu4, s, OverlayLayout.Vertical));
    }

    [Fact]
    public void Vram_has_no_unit_cell_in_either_layout()
    {
        var s = new OverlaySettings();
        Assert.Equal(("3.1G", ""), StatRows.Values(StatRow.GpuMemory, null, Gpu4, s, OverlayLayout.Horizontal));
        Assert.Equal(("3.1G", ""), StatRows.Values(StatRow.GpuMemory, null, Gpu4, s, OverlayLayout.Vertical));
    }

    [Fact]
    public void Cpu_values_and_temp_cell()
    {
        var both = new OverlaySettings();
        Assert.Equal(("30%", "50°"), StatRows.Values(StatRow.Cpu, null, Snap, both, OverlayLayout.Horizontal));
        Assert.Equal(2, StatRows.TempCell(StatRow.Cpu, both));

        var tempOnly = new OverlaySettings { ShowCpuLoad = false };
        Assert.Equal(("50°", ""), StatRows.Values(StatRow.Cpu, null, Snap, tempOnly, OverlayLayout.Vertical));
        Assert.Equal(1, StatRows.TempCell(StatRow.Cpu, tempOnly));
    }

    [Fact]
    public void Gpu_extra_rows_follow_switches()
    {
        var d = OverlaySettings.Defaults;
        Assert.False(StatRows.IsVisible(StatRow.GpuClock, d));
        Assert.True(StatRows.IsVisible(StatRow.GpuClock, d with { ShowGpuClock = true }));
        Assert.True(StatRows.IsVisible(StatRow.GpuMemory, d with { ShowGpuMemory = true }));
        Assert.True(StatRows.IsVisible(StatRow.GpuPower, d with { ShowGpuPower = true }));
        Assert.True(StatRows.IsVisible(StatRow.GpuFan, d with { ShowGpuFan = true }));
        Assert.False(StatRows.IsVisible(StatRow.GpuFan, d with { ShowGpuClock = true }));
    }
}
