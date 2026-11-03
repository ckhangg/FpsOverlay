using System.Text.RegularExpressions;

namespace FpsOverlay.Core.Update;

/// <summary>Quy tắc phiên bản và quyết định báo bản mới (thuần logic, không mạng, không UI).</summary>
public static partial class UpdatePolicy
{
    /// <summary>"v1.4.0", "1.4.0-beta", "V2.0.1+abc" cho 1.4.0 / 1.4.0 / 2.0.1. Cần đúng 3 số, không thì null.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var s = text.Trim();
        if (s[0] is 'v' or 'V') s = s[1..];
        var cut = s.IndexOfAny(['-', '+']);
        if (cut >= 0) s = s[..cut];
        var parts = s.Split('.');
        if (parts.Length != 3 || parts.Any(p => p.Length == 0 || !p.All(char.IsAsciiDigit))) return null;
        return int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b) && int.TryParse(parts[2], out var c)
            ? new Version(a, b, c)
            : null;
    }

    /// <summary>Báo khi có bản mới hơn. Kiểm tra tự động thì bỏ qua bản người dùng đã chọn "bỏ qua"; bấm tay thì luôn báo.</summary>
    public static bool ShouldOffer(Version current, ReleaseInfo? latest, string? skippedVersion, bool manual)
    {
        if (latest is null || latest.Version <= current) return false;
        return manual || ParseVersion(skippedVersion) != latest.Version;
    }

    /// <summary>Chuỗi 64 ký tự hex đầu tiên trong file .sha256 (định dạng "hash *tên"), chữ thường; không có thì null.</summary>
    public static string? ParseSha256File(string text)
    {
        var m = Sha256Regex().Match(text ?? "");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    [GeneratedRegex("(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])")]
    private static partial Regex Sha256Regex();
}
