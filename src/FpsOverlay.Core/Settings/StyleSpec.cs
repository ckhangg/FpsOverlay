namespace FpsOverlay.Core.Settings;

/// <summary>
/// Bảng màu và ký tự của một phong cách: bộ màu mặc định, màu theo mức FPS (Bad = màu "nóng"),
/// nền, ký tự phân cách, dấu nhắc và bảng màu mẫu. Mọi màu là <c>#RRGGBB</c>.
/// </summary>
public sealed record StyleSpec(
    StyleColors DefaultColors,
    string Good, string Medium, string Bad,
    string Background, string Separator, string Prompt,
    IReadOnlyList<string> Swatches);

/// <summary>Nơi duy nhất giữ các giá trị riêng của từng <see cref="OverlayStyle"/>.</summary>
public static class StyleSpecs
{
    private static readonly StyleSpec Classic = new(
        new StyleColors("#FF8C1A", "#FFFFFF", "#39FF14"),
        "#39FF14", "#FFD400", "#FF3B30",
        "#000000", " · ", "",
        ["#FF8C1A", "#FFFFFF", "#39FF14", "#FFD400", "#FF3B30", "#00BFFF", "#FF66CC", "#A0A0A0", "#00FF99", "#FFAA00"]);

    private static readonly StyleSpec Cyberpunk = new(
        new StyleColors("#00F0FF", "#FFFFFF", "#FCEE0A"),
        "#00F0FF", "#FCEE0A", "#FF003C",
        "#0A0A0F", " · ", "",
        ["#00F0FF", "#FCEE0A", "#FF003C", "#FF2A6D", "#05D9E8", "#D1F7FF", "#39FF14", "#B967FF", "#FFFFFF", "#FF8C1A"]);

    private static readonly StyleSpec NeonPurple = new(
        new StyleColors("#FF4FD8", "#F3E6FF", "#FF4FD8"),
        "#FF4FD8", "#FFB86C", "#FF2E4D",
        "#1A0828", " · ", "",
        ["#FF4FD8", "#F3E6FF", "#FFB86C", "#FF2E4D", "#B967FF", "#7A5CFF", "#00F0FF", "#FF8AE6", "#FCEE0A", "#FFFFFF"]);

    private static readonly StyleSpec TerminalGreen = new(
        new StyleColors("#1FAA44", "#33FF66", "#7CFF9E"),
        "#7CFF9E", "#E5FF4F", "#FF5F45",
        "#000800", " | ", "> ",
        ["#1FAA44", "#33FF66", "#7CFF9E", "#E5FF4F", "#FF5F45", "#00CC88", "#B8FFC8", "#FFB000", "#00E5FF", "#FFFFFF"]);

    public static StyleSpec For(OverlayStyle style) => style switch
    {
        OverlayStyle.Classic => Classic,
        OverlayStyle.Cyberpunk => Cyberpunk,
        OverlayStyle.NeonPurple => NeonPurple,
        OverlayStyle.TerminalGreen => TerminalGreen,
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
    };
}
