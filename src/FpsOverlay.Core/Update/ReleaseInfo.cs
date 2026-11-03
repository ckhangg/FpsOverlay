using System.Text.Json;

namespace FpsOverlay.Core.Update;

/// <summary>Bản phát hành mới nhất đọc từ GitHub Releases API (chỉ giữ phần app cần).</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string Notes, string PageUrl, string ExeUrl, string HashUrl)
{
    public const string ExeAssetName = "FpsOverlay.exe";
    public const string HashAssetName = "FpsOverlay.exe.sha256";

    /// <summary>Null khi JSON hỏng, tag không đọc được, hoặc thiếu một trong hai asset (exe, sha256). Chỉ nhận URL https.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var tag = Str(root, "tag_name");
            var version = UpdatePolicy.ParseVersion(tag);
            if (tag is null || version is null) return null;

            string? exe = null, hash = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.ValueKind != JsonValueKind.Object) continue;
                    var name = Str(asset, "name");
                    var url = Str(asset, "browser_download_url");
                    if (string.Equals(name, ExeAssetName, StringComparison.OrdinalIgnoreCase)) exe = url;
                    else if (string.Equals(name, HashAssetName, StringComparison.OrdinalIgnoreCase)) hash = url;
                }
            var page = Str(root, "html_url");
            if (!IsHttps(exe) || !IsHttps(hash) || !IsHttps(page)) return null;

            return new ReleaseInfo(version, tag, Str(root, "body") ?? "", page!, exe!, hash!);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
