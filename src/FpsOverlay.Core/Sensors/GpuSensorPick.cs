namespace FpsOverlay.Core;

/// <summary>Một cảm biến thô. <paramref name="Type"/> là tên <c>SensorType</c> của LibreHardwareMonitor dạng chuỗi (Core không phụ thuộc thư viện).</summary>
public readonly record struct SensorValue(string Type, string Name, float? Value);

/// <summary>Chọn số đọc GPU từ danh sách cảm biến của một card, theo thứ tự tên ưu tiên.</summary>
public static class GpuSensorPick
{
    public static GpuReading Read(string id, IEnumerable<SensorValue> sensors)
    {
        var list = sensors as IReadOnlyList<SensorValue> ?? sensors.ToList();

        double? memMb = Find(list, "SmallData", "GPU Memory Used") ?? Find(list, "SmallData", "D3D Dedicated Memory Used");
        double? power = Positive(Find(list, "Power", "GPU Package"))
                        ?? Positive(Find(list, "Power", "GPU Power"))
                        ?? Positive(Find(list, "Power", "GPU Board Power"));

        return new GpuReading(id,
            Load: Find(list, "Load", "GPU Core"),
            Temp: Positive(Find(list, "Temperature", "GPU Core")),
            MemoryUsedGb: memMb / 1024,
            ClockMhz: Positive(Find(list, "Clock", "GPU Core")),
            PowerW: power,
            FanRpm: FindFan(list));
    }

    // Số <= 0 là cảm biến chưa đọc được, không phải giá trị thật (riêng quạt: 0 RPM là hợp lệ).
    private static double? Positive(double? value) => value > 0 ? value : null;

    private static double? Find(IReadOnlyList<SensorValue> list, string type, string name)
    {
        foreach (var s in list)
        {
            if (s.Type == type && s.Name == name)
                return s.Value is float v && float.IsFinite(v) ? v : null;
        }
        return null;
    }

    // "GPU Fan" hoặc "GPU Fan 1"...: lấy cảm biến đầu tiên.
    private static double? FindFan(IReadOnlyList<SensorValue> list)
    {
        foreach (var s in list)
        {
            if (s.Type == "Fan" && s.Name.StartsWith("GPU Fan", StringComparison.Ordinal))
                return s.Value is float v && float.IsFinite(v) ? v : null;
        }
        return null;
    }
}
