using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core;

/// <summary>Các dòng của overlay, theo thứ tự hiển thị.</summary>
public enum StatRow { OnePercentLow, Gpu, GpuMemory, GpuClock, GpuPower, GpuFan, Cpu, Ram }

/// <summary>Mô hình dòng dùng chung cho cả hai bố cục: nhãn, điều kiện hiện, hai ô giá trị và ô mang nhiệt độ.</summary>
public static class StatRows
{
    private const string None = "--";

    public static IReadOnlyList<StatRow> Order { get; } =
    [
        StatRow.OnePercentLow, StatRow.Gpu, StatRow.GpuMemory, StatRow.GpuClock,
        StatRow.GpuPower, StatRow.GpuFan, StatRow.Cpu, StatRow.Ram,
    ];

    public static string Label(StatRow row) => row switch
    {
        StatRow.OnePercentLow => "1%",
        StatRow.Gpu => "GPU",
        StatRow.GpuMemory => "VRAM",
        StatRow.GpuClock => "CLK",
        StatRow.GpuPower => "PWR",
        StatRow.GpuFan => "FAN",
        StatRow.Cpu => "CPU",
        StatRow.Ram => "RAM",
        _ => "",
    };

    public static bool IsVisible(StatRow row, OverlaySettings s) => row switch
    {
        StatRow.OnePercentLow => s.ShowOnePercentLow,
        StatRow.Gpu => s.ShowGpuLoad || s.ShowGpuTemp,
        StatRow.GpuMemory => s.ShowGpuMemory,
        StatRow.GpuClock => s.ShowGpuClock,
        StatRow.GpuPower => s.ShowGpuPower,
        StatRow.GpuFan => s.ShowGpuFan,
        StatRow.Cpu => s.ShowCpuLoad || s.ShowCpuTemp,
        StatRow.Ram => s.ShowRam,
        _ => false,
    };

    public static (string First, string Second) Values(
        StatRow row, double? onePercentLow, SensorSnapshot s, OverlaySettings settings, OverlayLayout layout)
    {
        switch (row)
        {
            case StatRow.OnePercentLow: return (FpsCalculator.Format(onePercentLow), "");
            case StatRow.Gpu: return LoadTemp(settings.ShowGpuLoad, settings.ShowGpuTemp, s.GpuLoad, s.GpuTemp);
            case StatRow.Cpu: return LoadTemp(settings.ShowCpuLoad, settings.ShowCpuTemp, s.CpuLoad, s.CpuTemp);
            case StatRow.Ram: return (StatFormat.Gigabytes(s.RamUsedGb), "");
            case StatRow.GpuMemory: return (StatFormat.Gigabytes(s.GpuMemoryUsedGb), "");
            case StatRow.GpuClock: return WithUnit(StatFormat.Megahertz(s.GpuClockMhz, layout != OverlayLayout.Vertical), row, layout);
            case StatRow.GpuPower: return WithUnit(StatFormat.Watts(s.GpuPowerW, layout != OverlayLayout.Vertical), row, layout);
            case StatRow.GpuFan: return WithUnit(StatFormat.Rpm(s.GpuFanRpm, layout != OverlayLayout.Vertical), row, layout);
            default: return (None, "");
        }
    }

    /// <summary>0 = dòng không có nhiệt, 1 = nhiệt ở ô First, 2 = ở ô Second.</summary>
    public static int TempCell(StatRow row, OverlaySettings s) => row switch
    {
        StatRow.Gpu => s.ShowGpuTemp ? (s.ShowGpuLoad ? 2 : 1) : 0,
        StatRow.Cpu => s.ShowCpuTemp ? (s.ShowCpuLoad ? 2 : 1) : 0,
        _ => 0,
    };

    private static (string, string) LoadTemp(bool showLoad, bool showTemp, double? load, double? temp)
    {
        if (showLoad && showTemp) return (StatFormat.Percent(load), StatFormat.Temperature(temp));
        if (showTemp) return (StatFormat.Temperature(temp), "");
        return (StatFormat.Percent(load), "");
    }

    // Ngang: đơn vị dính vào số; dọc: đơn vị nằm ở ô thứ hai.
    private static (string, string) WithUnit(string first, StatRow row, OverlayLayout layout) =>
        (first, layout == OverlayLayout.Vertical ? Unit(row) : "");

    /// <summary>Hàng có đơn vị ở ô thứ hai trong bố cục dọc.</summary>
    public static bool HasUnit(StatRow row) => Unit(row).Length > 0;

    private static string Unit(StatRow row) => row switch
    {
        StatRow.GpuClock => "MHz",
        StatRow.GpuPower => "W",
        StatRow.GpuFan => "RPM",
        _ => "",
    };
}
