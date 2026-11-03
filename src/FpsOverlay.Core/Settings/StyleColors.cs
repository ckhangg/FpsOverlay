namespace FpsOverlay.Core.Settings;

/// <summary>Bộ ba màu của một phong cách: nhãn, giá trị, và số FPS.</summary>
public sealed record StyleColors(string Label, string Value, string Fps)
{
    /// <summary>Bộ màu mặc định của phong cách <paramref name="style"/>.</summary>
    public static StyleColors DefaultFor(OverlayStyle style) => StyleSpecs.For(style).DefaultColors;

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
