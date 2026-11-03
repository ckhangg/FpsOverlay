using FpsOverlay.Core.GameOnly;

namespace FpsOverlay.Core.Settings;

public enum OverlayStyle { Classic, Cyberpunk, NeonPurple, TerminalGreen }

public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public enum OverlayLayout { Horizontal, Vertical }

public enum AppLanguage { Vietnamese, English }

/// <summary>Cài đặt của overlay. Bất biến; dùng <c>with</c> hoặc các hàm <c>With*</c> để đổi.</summary>
public sealed record OverlaySettings
{
    public const int MinThreshold = 10;
    public const int MaxThreshold = 240;
    public const int MinHotThreshold = 50;
    public const int MaxHotThreshold = 110;
    public const int MaxHotAlertSeconds = 60;
    public const double MinScale = 0.5;
    public const double MaxScale = 3.0;
    public const double DefaultOffset = 8;
    public const double MaxOffset = 4000;

    public OverlayStyle Style { get; init; } = OverlayStyle.Cyberpunk;
    public OverlayCorner Corner { get; init; } = OverlayCorner.TopLeft;
    public OverlayLayout Layout { get; init; } = OverlayLayout.Horizontal;
    // Khoảng cách (DIP) từ mép màn hình tới khung overlay, tính từ góc đã chọn.
    public double OffsetX { get; init; } = DefaultOffset;
    public double OffsetY { get; init; } = DefaultOffset;
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
    // Bốn số GPU thêm (VRAM, xung nhân, công suất, quạt): mặc định tắt để dải ngang không dài thêm.
    public bool ShowGpuMemory { get; init; }
    public bool ShowGpuClock { get; init; }
    public bool ShowGpuPower { get; init; }
    public bool ShowGpuFan { get; init; }
    public bool ShowCpuLoad { get; init; } = true;
    public bool ShowCpuTemp { get; init; } = true;
    public bool ShowRam { get; init; } = true;

    /// <summary>Số ô thông số (mười công tắc trên) và số ô đang bật, cho dòng "Đang hiện N/10" trong cài đặt.</summary>
    public const int StatCount = 10;

    [System.Text.Json.Serialization.JsonIgnore]
    public int VisibleStatCount =>
        (ShowOnePercentLow ? 1 : 0) + (ShowGpuLoad ? 1 : 0) + (ShowGpuTemp ? 1 : 0) + (ShowGpuMemory ? 1 : 0) +
        (ShowGpuClock ? 1 : 0) + (ShowGpuPower ? 1 : 0) + (ShowGpuFan ? 1 : 0) + (ShowCpuLoad ? 1 : 0) +
        (ShowCpuTemp ? 1 : 0) + (ShowRam ? 1 : 0);
    // Cảnh báo nhiệt: ngưỡng (°C), số giây nóng liên tục trước khi báo, nháy overlay và phát tiếng.
    public int GpuHotThreshold { get; init; } = 85;
    public int CpuHotThreshold { get; init; } = 90;
    public int HotAlertSeconds { get; init; } = 10;
    public bool HotAlertBlink { get; init; } = true;
    public bool HotAlertSound { get; init; }
    // Ghi log phiên và cờ đã mời cài PawnIO: không thuộc phần giao diện nên Khôi phục mặc định không đụng tới.
    public bool LoggingEnabled { get; init; } = true;
    public bool PawnIoOffered { get; init; }
    // Chỉ hiện overlay khi đang chơi game; hai danh sách exe do người dùng thêm từ menu khay ("không bao giờ" thắng "luôn hiện").
    public bool ShowOnlyInGame { get; init; } = true;
    public AppNames AlwaysShowApps { get; init; } = AppNames.Empty;
    public AppNames NeverShowApps { get; init; } = AppNames.Empty;
    public StyleColors ClassicColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.Classic);
    public StyleColors CyberpunkColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.Cyberpunk);
    public StyleColors NeonPurpleColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.NeonPurple);
    public StyleColors TerminalGreenColors { get; init; } = StyleColors.DefaultFor(OverlayStyle.TerminalGreen);

    public static OverlaySettings Defaults { get; } = new();

    /// <summary>Kẹp mọi giá trị vào khoảng cho phép và thay màu hỏng bằng màu mặc định.</summary>
    public OverlaySettings Normalize()
    {
        var good = Math.Clamp(GoodThreshold, MinThreshold + 1, MaxThreshold);
        var bad = Math.Clamp(BadThreshold, MinThreshold, MaxThreshold);
        if (bad >= good) bad = good - 1;

        var clamped = this with
        {
            OffsetX = Clamp(OffsetX, 0, MaxOffset, DefaultOffset),
            OffsetY = Clamp(OffsetY, 0, MaxOffset, DefaultOffset),
            Scale = Clamp(Scale, MinScale, MaxScale, 1.0),
            BackgroundOpacity = Clamp(BackgroundOpacity, 0, 1, 0.72),
            GoodThreshold = good,
            BadThreshold = bad,
            GpuHotThreshold = Math.Clamp(GpuHotThreshold, MinHotThreshold, MaxHotThreshold),
            CpuHotThreshold = Math.Clamp(CpuHotThreshold, MinHotThreshold, MaxHotThreshold),
            HotAlertSeconds = Math.Clamp(HotAlertSeconds, 0, MaxHotAlertSeconds),
            NeverShowApps = NeverShowApps ?? AppNames.Empty,
            AlwaysShowApps = RemoveAll(AlwaysShowApps ?? AppNames.Empty, NeverShowApps ?? AppNames.Empty),
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
        OverlayStyle.Cyberpunk => CyberpunkColors,
        OverlayStyle.NeonPurple => NeonPurpleColors,
        OverlayStyle.TerminalGreen => TerminalGreenColors,
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
    };

    public OverlaySettings WithColors(OverlayStyle style, StyleColors colors) => style switch
    {
        OverlayStyle.Classic => this with { ClassicColors = colors },
        OverlayStyle.Cyberpunk => this with { CyberpunkColors = colors },
        OverlayStyle.NeonPurple => this with { NeonPurpleColors = colors },
        OverlayStyle.TerminalGreen => this with { TerminalGreenColors = colors },
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
    };

    /// <summary>Đổi góc và đưa hai offset về mặc định (chọn góc từ menu nghĩa là đặt lại vị trí).</summary>
    public OverlaySettings WithCorner(OverlayCorner corner) =>
        this with { Corner = corner, OffsetX = DefaultOffset, OffsetY = DefaultOffset };

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

    /// <summary>Thêm vào danh sách "luôn hiện" và bỏ khỏi "không bao giờ".</summary>
    public OverlaySettings WithAlwaysShow(string exe) =>
        this with { AlwaysShowApps = AlwaysShowApps.With(exe), NeverShowApps = NeverShowApps.Without(exe) };

    /// <summary>Thêm vào danh sách "không bao giờ" và bỏ khỏi "luôn hiện".</summary>
    public OverlaySettings WithNeverShow(string exe) =>
        this with { NeverShowApps = NeverShowApps.With(exe), AlwaysShowApps = AlwaysShowApps.Without(exe) };

    /// <summary>Bỏ khỏi cả hai danh sách.</summary>
    public OverlaySettings WithoutApp(string exe) =>
        this with { AlwaysShowApps = AlwaysShowApps.Without(exe), NeverShowApps = NeverShowApps.Without(exe) };

    private static AppNames RemoveAll(AppNames from, AppNames remove)
    {
        var result = from;
        foreach (var name in remove) result = result.Without(name);
        return result;
    }

    /// <summary>Về mặc định nhưng giữ ngôn ngữ, phong cách đang chọn, bộ màu của phong cách kia, ghi log, cờ PawnIO và hai danh sách ứng dụng.</summary>
    public OverlaySettings ResetToDefaults()
    {
        var result = Defaults;
        foreach (var style in Enum.GetValues<OverlayStyle>())
            result = result.WithColors(style, style == Style ? Defaults.ColorsFor(style) : ColorsFor(style));
        return result with { Style = Style, Language = Language, LoggingEnabled = LoggingEnabled, PawnIoOffered = PawnIoOffered,
            AlwaysShowApps = AlwaysShowApps, NeverShowApps = NeverShowApps };
    }

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
}
