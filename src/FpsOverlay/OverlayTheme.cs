using System.Windows.Media;
using FpsOverlay.Core;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>Font và màu cố định của từng phong cách overlay. Mọi brush đều đã Freeze và được dùng lại.</summary>
static class OverlayTheme
{
    static readonly Uri FontBase = new("pack://application:,,,/");
    static readonly FontFamily Consolas = new("Consolas");
    static readonly FontFamily ChakraPetch = new(FontBase, "./Assets/Fonts/#Chakra Petch");
    static readonly FontFamily JetBrainsMono = new(FontBase, "./Assets/Fonts/#JetBrains Mono");

    static readonly Brush ClassicGood = Frozen("#39FF14");
    static readonly Brush ClassicMedium = Frozen("#FFD400");
    static readonly Brush ClassicBad = Frozen("#FF3B30");
    static readonly Brush CyberGood = Frozen("#00F0FF");
    static readonly Brush CyberMedium = Frozen("#FCEE0A");
    static readonly Brush CyberBad = Frozen("#FF003C");

    /// <summary>Viền trái của phong cách Cyberpunk.</summary>
    public static readonly Brush CyberAccent = Frozen("#FCEE0A");

    public static readonly Color ClassicBackground = Color.FromRgb(0x00, 0x00, 0x00);
    public static readonly Color CyberBackground = Color.FromRgb(0x0A, 0x0A, 0x0F);

    static readonly Dictionary<string, Brush> HexCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Màu FPS theo mức. <see cref="FpsLevel.None"/> không có màu mức riêng nên trả màu FPS mặc định của phong cách.
    /// </summary>
    public static Brush LevelBrush(OverlayStyle style, FpsLevel level) => (style, level) switch
    {
        (OverlayStyle.Classic, FpsLevel.Good) => ClassicGood,
        (OverlayStyle.Classic, FpsLevel.Medium) => ClassicMedium,
        (OverlayStyle.Classic, FpsLevel.Bad) => ClassicBad,
        (OverlayStyle.Cyberpunk, FpsLevel.Good) => CyberGood,
        (OverlayStyle.Cyberpunk, FpsLevel.Medium) => CyberMedium,
        (OverlayStyle.Cyberpunk, FpsLevel.Bad) => CyberBad,
        _ => FromHex(StyleColors.DefaultFor(style).Fps),
    };

    public static FontFamily LabelFont(OverlayStyle style) =>
        style == OverlayStyle.Classic ? Consolas : ChakraPetch;

    public static FontFamily ValueFont(OverlayStyle style) =>
        style == OverlayStyle.Classic ? Consolas : JetBrainsMono;

    /// <summary>Brush cho chuỗi <c>#RRGGBB</c> (đã chuẩn hoá bởi <see cref="OverlaySettings.Normalize"/>), có cache.</summary>
    public static Brush FromHex(string hex)
    {
        if (!HexCache.TryGetValue(hex, out var brush))
        {
            brush = Frozen(hex);
            HexCache[hex] = brush;
        }
        return brush;
    }

    static Brush Frozen(string hex)
    {
        Color color;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (Exception e) when (e is FormatException or NotSupportedException)
        {
            color = Colors.White;
        }
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
