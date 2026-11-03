namespace FpsOverlay.Core.Tests;

public class PresentMonHealthTests
{
    [Theory]
    [InlineData(5, null, false)]     // mới chạy 5 giây: chưa cảnh báo
    [InlineData(11, null, true)]     // 11 giây chưa có khung nào
    [InlineData(30, 2.0, false)]     // có dữ liệu gần đây
    [InlineData(30, 10.5, false)]    // có khung rồi (sau lúc chạy) thì im lặng sau đó không cảnh báo: màn hình tĩnh vẫn bình thường
    [InlineData(30, 25.0, false)]    // khung đầu tiên đến sau lúc chạy, rồi im 25 giây
    [InlineData(11, 20.0, true)]     // khung cũ từ trước lần chạy lại, từ đó chưa có khung mới
    public void IsStalled(double sinceStart, double? sinceFrame, bool expected) =>
        Assert.Equal(expected, PresentMonHealth.IsStalled(sinceStart, sinceFrame));
}
