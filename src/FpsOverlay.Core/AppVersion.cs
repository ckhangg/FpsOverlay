namespace FpsOverlay.Core;

/// <summary>Chuỗi phiên bản hiện cho người dùng, lấy từ InformationalVersion của assembly.</summary>
public static class AppVersion
{
    /// <summary>"v" + phần trước dấu '+' (bỏ hậu tố mã commit); thiếu/rỗng thì "v?".</summary>
    public static string Display(string? informationalVersion)
    {
        var version = informationalVersion?.Split('+')[0].Trim();
        return string.IsNullOrEmpty(version) ? "v?" : "v" + version;
    }
}
