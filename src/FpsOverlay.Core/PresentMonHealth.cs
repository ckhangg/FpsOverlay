namespace FpsOverlay.Core;

/// <summary>Phát hiện PresentMon "im lặng": chạy rồi mà không gửi khung hình nào (thường do thiếu quyền Administrator).</summary>
public static class PresentMonHealth
{
    /// <summary>Quá ngần này giây không có dữ liệu thì coi là im lặng.</summary>
    public const double StallSeconds = 10;

    /// <param name="secondsSinceStart">Thời gian từ lúc PresentMon được (khởi động lại) chạy.</param>
    /// <param name="secondsSinceAnyFrame">Thời gian từ khung hình gần nhất, null nếu chưa nhận khung nào.</param>
    public static bool IsStalled(double secondsSinceStart, double? secondsSinceAnyFrame) =>
        secondsSinceAnyFrame is { } since ? since > StallSeconds : secondsSinceStart > StallSeconds;
}
