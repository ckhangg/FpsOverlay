namespace FpsOverlay.Core.Tests;

public class PresentMonHealthTests
{
    [Theory]
    [InlineData(5, null, false)]     // mới chạy 5 giây: chưa cảnh báo
    [InlineData(11, null, true)]     // 11 giây chưa có khung nào
    [InlineData(30, 2.0, false)]     // có dữ liệu gần đây
    [InlineData(30, 10.5, true)]     // im lặng 10,5 giây
    public void IsStalled(double sinceStart, double? sinceFrame, bool expected) =>
        Assert.Equal(expected, PresentMonHealth.IsStalled(sinceStart, sinceFrame));
}
