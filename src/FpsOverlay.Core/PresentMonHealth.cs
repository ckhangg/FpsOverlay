namespace FpsOverlay.Core;

/// <summary>Phát hiện PresentMon "im lặng": chạy rồi mà không gửi khung hình nào (thường do thiếu quyền Administrator).</summary>
public static class PresentMonHealth
{
    /// <summary>Quá ngần này giây từ lúc chạy mà chưa nhận khung nào thì coi là im lặng.</summary>
    public const double StallSeconds = 10;

    /// <param name="secondsSinceStart">Thời gian từ lúc PresentMon được (khởi động lại) chạy.</param>
    /// <param name="secondsSinceAnyFrame">Thời gian từ khung hình gần nhất, null nếu chưa nhận khung nào.</param>
    /// <remarks>
    /// Chỉ cảnh báo khi chưa có khung nào kể từ lúc chạy. Đã có khung rồi thì không cảnh báo nữa:
    /// màn hình tĩnh có thể không present gì hơn 10 giây mà PresentMon vẫn bình thường.
    /// </remarks>
    public static bool IsStalled(double secondsSinceStart, double? secondsSinceAnyFrame)
    {
        bool anyFrameSinceStart = secondsSinceAnyFrame is { } since && since <= secondsSinceStart;
        return !anyFrameSinceStart && secondsSinceStart > StallSeconds;
    }
}
