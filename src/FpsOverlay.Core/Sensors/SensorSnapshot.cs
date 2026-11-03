namespace FpsOverlay.Core;

/// <summary>Một lần đọc cảm biến; giá trị nào không đọc được thì là <c>null</c>.</summary>
public readonly record struct SensorSnapshot(
    double? GpuLoad, double? GpuTemp, double? CpuLoad, double? CpuTemp, double? RamUsedGb,
    double? GpuMemoryUsedGb = null, double? GpuClockMhz = null, double? GpuPowerW = null, double? GpuFanRpm = null)
{
    public static SensorSnapshot Empty => default;
}

/// <summary>Số đọc của một card đồ hoạ (Id ổn định giữa các lần đọc, thường là đường dẫn định danh phần cứng).</summary>
public readonly record struct GpuReading(
    string Id, double? Load, double? Temp,
    double? MemoryUsedGb = null, double? ClockMhz = null, double? PowerW = null, double? FanRpm = null);

public static class SensorMath
{
    /// <summary>
    /// Chọn card đang bận nhất (load cao nhất, null coi như thấp nhất). Load bằng nhau thì giữ
    /// <paramref name="previousId"/> nếu còn trong danh sách (tránh nhảy qua lại), không thì card đầu tiên.
    /// </summary>
    public static GpuReading? BusiestGpu(IReadOnlyList<GpuReading> gpus, string? previousId)
    {
        if (gpus.Count == 0) return null;

        GpuReading best = gpus[0];
        for (int i = 1; i < gpus.Count; i++)
        {
            var g = gpus[i];
            double gl = g.Load ?? double.NegativeInfinity;
            double bl = best.Load ?? double.NegativeInfinity;
            if (gl > bl) best = g;
        }

        // Giữ card cũ nếu nó hoà với card tốt nhất
        if (previousId is not null)
        {
            double bl = best.Load ?? double.NegativeInfinity;
            foreach (var g in gpus)
            {
                if (g.Id == previousId && (g.Load ?? double.NegativeInfinity) == bl) return g;
            }
        }
        return best;
    }
}
