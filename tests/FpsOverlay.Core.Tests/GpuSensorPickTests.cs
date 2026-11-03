using FpsOverlay.Core;

namespace FpsOverlay.Core.Tests;

public class GpuSensorPickTests
{
    private static SensorValue V(string type, string name, float? value) => new(type, name, value);

    [Fact]
    public void Picks_nvidia_names()
    {
        var r = GpuSensorPick.Read("gpu0",
        [
            V("Clock", "GPU Core", 1590), V("Power", "GPU Package", 48),
            V("SmallData", "GPU Memory Used", 3174.4f), V("Fan", "GPU Fan", 2400),
        ]);
        Assert.Equal(1590, r.ClockMhz);
        Assert.Equal(48, r.PowerW);
        Assert.Equal(3.1, r.MemoryUsedGb!.Value, 0.01);
        Assert.Equal(2400, r.FanRpm);
    }

    [Fact]
    public void Falls_back_to_d3d_memory_and_gpu_power()
    {
        var r = GpuSensorPick.Read("gpu0", [V("SmallData", "D3D Dedicated Memory Used", 2048), V("Power", "GPU Power", 30)]);
        Assert.Equal(2.0, r.MemoryUsedGb!.Value, 0.001);
        Assert.Equal(30, r.PowerW);
    }

    [Fact]
    public void Zero_clock_and_power_are_null_but_zero_fan_is_kept()
    {
        var r = GpuSensorPick.Read("gpu0", [V("Clock", "GPU Core", 0), V("Power", "GPU Package", 0), V("Fan", "GPU Fan", 0)]);
        Assert.Null(r.ClockMhz);
        Assert.Null(r.PowerW);
        Assert.Equal(0, r.FanRpm);
    }

    [Fact]
    public void Missing_fan_is_null() =>
        Assert.Null(GpuSensorPick.Read("gpu0", [V("Load", "GPU Core", 10)]).FanRpm);

    [Fact]
    public void Fan_name_with_index_is_accepted() =>
        Assert.Equal(1800, GpuSensorPick.Read("gpu0", [V("Fan", "GPU Fan 1", 1800)]).FanRpm);

    [Fact]
    public void Load_and_temp_unchanged_rules()
    {
        var r = GpuSensorPick.Read("gpu0", [V("Load", "GPU Core", 55), V("Temperature", "GPU Core", 0)]);
        Assert.Equal("gpu0", r.Id);
        Assert.Equal(55, r.Load);
        Assert.Null(r.Temp);
    }
}
