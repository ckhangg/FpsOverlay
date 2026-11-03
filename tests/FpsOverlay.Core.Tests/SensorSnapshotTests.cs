using FpsOverlay.Core;

namespace FpsOverlay.Core.Tests;

public class SensorSnapshotTests
{
    static GpuReading G(string id, double? load, double? temp = null) => new(id, load, temp);

    [Fact]
    public void Chon_card_load_cao_nhat()
    {
        var r = SensorMath.BusiestGpu([G("a", 10), G("b", 80), G("c", 30)], "a");
        Assert.Equal("b", r!.Value.Id);
    }

    [Fact]
    public void Hai_card_cung_null_giu_previousId()
    {
        var r = SensorMath.BusiestGpu([G("a", null), G("b", null)], "b");
        Assert.Equal("b", r!.Value.Id);
    }

    [Fact]
    public void Load_bang_nhau_giu_previousId()
    {
        var r = SensorMath.BusiestGpu([G("a", 50), G("b", 50)], "b");
        Assert.Equal("b", r!.Value.Id);
    }

    [Fact]
    public void Previous_khong_con_thi_lay_card_dau()
    {
        var r = SensorMath.BusiestGpu([G("a", null), G("b", null)], "x");
        Assert.Equal("a", r!.Value.Id);
        r = SensorMath.BusiestGpu([G("a", null), G("b", null)], null);
        Assert.Equal("a", r!.Value.Id);
    }

    [Fact]
    public void Danh_sach_rong_tra_null()
    {
        Assert.Null(SensorMath.BusiestGpu([], "a"));
    }

    [Fact]
    public void Null_coi_la_thap_nhat_so_voi_0()
    {
        var r = SensorMath.BusiestGpu([G("a", null), G("b", 0)], "a");
        Assert.Equal("b", r!.Value.Id);
    }

    [Fact]
    public void Empty_toan_null()
    {
        Assert.Equal(new SensorSnapshot(null, null, null, null, null), SensorSnapshot.Empty);
    }
}
