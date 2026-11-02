namespace FpsOverlay.Core.Settings;

/// <summary>Chuẩn hoá chuỗi màu hex <c>#RRGGBB</c>.</summary>
public static class HexColor
{
    /// <summary>
    /// Nhận <c>#RRGGBB</c> hoặc <c>RRGGBB</c> (không phân biệt hoa thường), trả về <c>#RRGGBB</c> viết hoa.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (input is null) return false;

        var digits = input.StartsWith('#') ? input[1..] : input;
        if (digits.Length != 6) return false;
        foreach (var c in digits)
        {
            if (!Uri.IsHexDigit(c)) return false;
        }

        normalized = "#" + digits.ToUpperInvariant();
        return true;
    }
}
