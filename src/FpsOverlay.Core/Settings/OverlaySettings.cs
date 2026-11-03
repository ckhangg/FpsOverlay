namespace FpsOverlay.Core.Settings;

public enum OverlayStyle { Classic, Cyberpunk }

public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public enum OverlayLayout { Horizontal, Vertical }

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
    // Các mục thông số hiện trên overlay (1% low, GPU, CPU, RAM).
    public bool ShowOnePercentLow { get; init; } = true;
    public bool ShowGpuLoad { get; init; } = true;
    public bool ShowGpuTemp { get; init; } = true;
    public bool ShowCpuLoad { get; init; } = true;
    public bool ShowCpuTemp { get; init; } = true;
    public bool ShowRam { get; init; } = true;
    // Ghi log phiên và cờ đã mời cài PawnIO: không thuộc phần giao diện nên Khôi phục mặc định không đụng tới.
    public bool LoggingEnabled { get; init; } = true;
    public bool PawnIoOffered { get; init; }
    public StyleColors ClassicColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.Classic);
    public StyleColors CyberpunkColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.Cyberpunk);

    public static OverlaySettings Defaults { get; } = new();

    /// <summary>Kẹp mọi giá trị vào khoảng cho phép và thay màu hỏng bằng màu mặc định.</summary>
    public OverlaySettings Normalize()
    {
        var good = Math.Clamp(GoodThreshold, MinThreshold + 1, MaxThreshold);
        var bad = Math.Clamp(BadThreshold, MinThreshold, MaxThreshold);
        if (bad >= good) bad = good - 1;

        var clamped = this with
        {
            Scale = Clamp(Scale, MinScale, MaxScale, 1.0),
            BackgroundOpacity = Clamp(BackgroundOpacity, 0, 1, 0.72),
            GoodThreshold = good,
            BadThreshold = bad,
        };
        return clamped.NormalizeColors();
    }

    // Chuẩn hoá bộ màu của mọi phong cách.
    private OverlaySettings NormalizeColors()
    {
        var result = this;
        foreach (var style in Enum.GetValues<OverlayStyle>())
            result = result.WithColors(style, (ColorsFor(style) ?? StyleColors.DefaultFor(style)).Normalize(style));
        return result;
    }

    public StyleColors ColorsFor(OverlayStyle style) => style switch
    {
        OverlayStyle.Classic => ClassicColors,
        _ => CyberpunkColors,
    };

    public OverlaySettings WithColors(OverlayStyle style, StyleColors colors) => style switch
    {
        OverlayStyle.Classic => this with { ClassicColors = colors },
        _ => this with { CyberpunkColors = colors },
    };

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

    /// <summary>Về mặc định nhưng giữ ngôn ngữ, phong cách đang chọn, bộ màu của phong cách kia, ghi log và cờ PawnIO.</summary>
    public OverlaySettings ResetToDefaults()
    {
        var result = Defaults;
        foreach (var style in Enum.GetValues<OverlayStyle>())
            result = result.WithColors(style, style == Style ? Defaults.ColorsFor(style) : ColorsFor(style));
        return result with { Style = Style, Language = Language, LoggingEnabled = LoggingEnabled, PawnIoOffered = PawnIoOffered };
    }

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
}
