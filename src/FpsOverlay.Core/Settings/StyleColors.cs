namespace FpsOverlay.Core.Settings;

/// <summary>Bộ ba màu của một phong cách: nhãn, giá trị, và số FPS.</summary>
public sealed record StyleColors(string Label, string Value, string Fps)
{
    /// <summary>Bộ màu mặc định của phong cách <paramref name="style"/>.</summary>
    public static StyleColors DefaultFor(OverlayStyle style) => style switch
    {
        OverlayStyle.Classic => new StyleColors("#FF8C1A", "#FFFFFF", "#39FF14"),
        _ => new StyleColors("#00F0FF", "#FFFFFF", "#FCEE0A"),
    };

    /// <summary>Chuẩn hoá từng màu; màu không hợp lệ được thay bằng màu mặc định của đúng ô đó.</summary>
    internal StyleColors Normalize(OverlayStyle style)
    {
        var d = DefaultFor(style);
        return new StyleColors(
            HexColor.TryNormalize(Label, out var l) ? l : d.Label,
            HexColor.TryNormalize(Value, out var v) ? v : d.Value,
            HexColor.TryNormalize(Fps, out var f) ? f : d.Fps);
    }
}
