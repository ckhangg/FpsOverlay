using System.Windows;
using System.Windows.Media;
using FpsOverlay.Core;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>Chữ nào phát sáng (cùng màu chữ): không gì, chỉ số FPS, nhãn và số FPS, hay mọi chữ.</summary>
enum TextGlow { None, FpsOnly, LabelsAndFps, All }

/// <summary>
/// Phần hình của một phong cách overlay (font, cỡ chữ, khoảng cách, khung, hiệu ứng). Màu nằm ở <see cref="StyleSpec"/>.
/// Khoảng cách (gap) là cỡ chữ của dấu cách giữa nhãn và giá trị.
/// </summary>
sealed record OverlayVisuals(
    FontFamily LabelFont, FontFamily ValueFont,
    double FpsLabelSize, double FpsValueSize, double StatLabelSize, double StatValueSize,
    double FpsGapSize, double StatGapSize,
    CornerRadius CornerRadius, Thickness BorderThickness, Brush? BorderBrush, bool BorderGlow,
    double Chamfer, Thickness Padding, TextGlow Glow, bool Outline, bool Scanlines);

/// <summary>Font và màu cố định của từng phong cách overlay. Mọi brush đều đã Freeze và được dùng lại.</summary>
static class OverlayTheme
{
    static readonly Uri FontBase = new("pack://application:,,,/");
    static readonly FontFamily Consolas = new("Consolas");
    static readonly FontFamily ChakraPetch = new(FontBase, "./Assets/Fonts/#Chakra Petch");
    static readonly FontFamily JetBrainsMono = new(FontBase, "./Assets/Fonts/#JetBrains Mono");

    static readonly Dictionary<string, Brush> HexCache = new(StringComparer.OrdinalIgnoreCase);

    // Cổ điển: Consolas, góc bo 4, viền đen quanh chữ.
    // Khoảng cách ≈ 5–6 DIP (FPS) và ≈ 4–5 DIP (mục phụ).
    static readonly OverlayVisuals Classic = new(
        Consolas, Consolas,
        FpsLabelSize: 18, FpsValueSize: 24, StatLabelSize: 14, StatValueSize: 18,
        FpsGapSize: 11, StatGapSize: 9,
        CornerRadius: new CornerRadius(4), BorderThickness: new Thickness(0), BorderBrush: null, BorderGlow: false,
        Chamfer: 0, Padding: new Thickness(12, 4, 12, 4), Glow: TextGlow.None, Outline: true, Scanlines: false);

    // Cyberpunk: viền trái vàng, góc phải dưới vát, số FPS phát sáng.
    // Chakra Petch có dấu cách hẹp nên cỡ dấu cách lớn hơn Consolas để ra cùng khoảng cách.
    // Lề phải cộng thêm độ vát để chữ không bị cắt.
    static readonly OverlayVisuals Cyberpunk = new(
        ChakraPetch, JetBrainsMono,
        FpsLabelSize: 18, FpsValueSize: 24, StatLabelSize: 14, StatValueSize: 18,
        FpsGapSize: 20, StatGapSize: 16,
        CornerRadius: new CornerRadius(0), BorderThickness: new Thickness(3, 0, 0, 0), BorderBrush: Frozen("#FCEE0A"),
        BorderGlow: false, Chamfer: 9, Padding: new Thickness(10, 3, 10 + 9, 3), Glow: TextGlow.FpsOnly,
        Outline: false, Scanlines: false);

    public static OverlayVisuals Visuals(OverlayStyle style) => style switch
    {
        OverlayStyle.Classic => Classic,
        _ => Cyberpunk,
    };

    /// <summary>
    /// Màu FPS theo mức. <see cref="FpsLevel.None"/> không có màu mức riêng nên trả màu FPS mặc định của phong cách.
    /// </summary>
    public static Brush LevelBrush(OverlayStyle style, FpsLevel level)
    {
        var spec = StyleSpecs.For(style);
        return FromHex(level switch
        {
            FpsLevel.Good => spec.Good,
            FpsLevel.Medium => spec.Medium,
            FpsLevel.Bad => spec.Bad,
            _ => spec.DefaultColors.Fps,
        });
    }

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

    /// <summary>Màu của chuỗi <c>#RRGGBB</c>; chuỗi hỏng thành trắng.</summary>
    public static Color ColorFromHex(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (Exception e) when (e is FormatException or NotSupportedException)
        {
            return Colors.White;
        }
    }

    static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush(ColorFromHex(hex));
        brush.Freeze();
        return brush;
    }
}
