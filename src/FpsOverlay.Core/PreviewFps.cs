namespace FpsOverlay.Core;

/// <summary>Số FPS mẫu cho cửa sổ cài đặt: sóng tam giác 20 → 144 → 20.</summary>
public static class PreviewFps
{
    public const double PeriodSeconds = 6;
    private const double Low = 20;
    private const double High = 144;

    public static double At(double seconds)
    {
        var phase = seconds % PeriodSeconds / PeriodSeconds;
        if (phase < 0) phase += 1;
        var tri = phase < 0.5 ? phase * 2 : (1 - phase) * 2;
        return Low + (High - Low) * tri;
    }
}
