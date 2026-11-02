namespace FpsOverlay.Core.Settings;

public enum OverlayStyle { Classic, Cyberpunk }

public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public enum AppLanguage { Vietnamese, English }

/// <summary>Cài đặt của overlay. Bất biến; dùng <c>with</c> hoặc các hàm <c>With*</c> để đổi.</summary>
public sealed record OverlaySettings
{
    public const int MinThreshold = 10;
    public const int MaxThreshold = 240;
    public const double MinScale = 0.5;
    public const double MaxScale = 3.0;

    public OverlayStyle Style { get; init; } = OverlayStyle.Cyberpunk;
    public OverlayCorner Corner { get; init; } = OverlayCorner.TopLeft;
    public double Scale { get; init; } = 1.0;
    public double BackgroundOpacity { get; init; } = 0.72;
    public bool ColorByFps { get; init; } = true;
    public int GoodThreshold { get; init; } = 60;
    public int BadThreshold { get; init; } = 30;
    public AppLanguage Language { get; init; } = AppLanguage.Vietnamese;
    public StyleColors ClassicColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.Classic);
    public StyleColors CyberpunkColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.Cyberpunk);

    public static OverlaySettings Defaults { get; } = new();

    /// <summary>Kẹp mọi giá trị vào khoảng cho phép và thay màu hỏng bằng màu mặc định.</summary>
    public OverlaySettings Normalize()
    {
        var good = Math.Clamp(GoodThreshold, MinThreshold + 1, MaxThreshold);
        var bad = Math.Clamp(BadThreshold, MinThreshold, MaxThreshold);
        if (bad >= good) bad = good - 1;

        return this with
        {
            Scale = Clamp(Scale, MinScale, MaxScale, 1.0),
            BackgroundOpacity = Clamp(BackgroundOpacity, 0, 1, 0.72),
            GoodThreshold = good,
            BadThreshold = bad,
            ClassicColors = (ClassicColors ?? StyleColors.DefaultFor(OverlayStyle.Classic)).Normalize(OverlayStyle.Classic),
            CyberpunkColors = (CyberpunkColors ?? StyleColors.DefaultFor(OverlayStyle.Cyberpunk)).Normalize(OverlayStyle.Cyberpunk),
        };
    }

    public StyleColors ColorsFor(OverlayStyle style) =>
        style == OverlayStyle.Classic ? ClassicColors : CyberpunkColors;

    public OverlaySettings WithColors(OverlayStyle style, StyleColors colors) =>
        style == OverlayStyle.Classic ? this with { ClassicColors = colors } : this with { CyberpunkColors = colors };

    /// <summary>Đặt mốc tốt; nếu mốc kém không còn thấp hơn thì đẩy mốc kém xuống.</summary>
    public OverlaySettings WithGoodThreshold(int value)
    {
        var good = Math.Clamp(value, MinThreshold + 1, MaxThreshold);
        return this with { GoodThreshold = good, BadThreshold = Math.Min(BadThreshold, good - 1) };
    }

    /// <summary>Đặt mốc kém; nếu mốc tốt không còn cao hơn thì đẩy mốc tốt lên.</summary>
    public OverlaySettings WithBadThreshold(int value)
    {
        var bad = Math.Clamp(value, MinThreshold, MaxThreshold - 1);
        return this with { BadThreshold = bad, GoodThreshold = Math.Max(GoodThreshold, bad + 1) };
    }

    /// <summary>Về mặc định nhưng giữ ngôn ngữ, phong cách đang chọn và bộ màu của phong cách kia.</summary>
    public OverlaySettings ResetToDefaults() =>
        Defaults.WithColors(OverlayStyle.Classic, Style == OverlayStyle.Classic ? Defaults.ClassicColors : ClassicColors)
                .WithColors(OverlayStyle.Cyberpunk, Style == OverlayStyle.Cyberpunk ? Defaults.CyberpunkColors : CyberpunkColors)
        with { Style = Style, Language = Language };

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
}
