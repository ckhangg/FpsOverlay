namespace FpsOverlay.Core;

/// <summary>
/// Thanh trượt dạng chữ của chủ đề Terminal: <c>[######----]</c>, số ô <c>#</c> là phần đã chọn của khoảng
/// [min, max], làm tròn nửa lên tới ô gần nhất. Ngoài khoảng thì kẹp lại; NaN hay khoảng rỗng (min ≥ max) → toàn <c>-</c>.
/// </summary>
public static class AsciiBar
{
    public static string Render(double value, double min, double max, int cells = 10)
    {
        cells = Math.Max(cells, 1);
        var filled = 0;
        if (max > min && !double.IsNaN(value))
        {
            // Nhân trước rồi chia: 5 trên 0–100 với 10 ô ra đúng 0.5 (không lệch số thực), làm tròn lên 1 ô.
            var exact = (Math.Clamp(value, min, max) - min) * cells / (max - min);
            filled = (int)Math.Round(exact, MidpointRounding.AwayFromZero);
        }
        return string.Create(cells + 2, filled, static (span, n) =>
        {
            span[0] = '[';
            span[^1] = ']';
            for (int i = 1; i < span.Length - 1; i++) span[i] = i <= n ? '#' : '-';
        });
    }
}
