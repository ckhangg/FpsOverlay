namespace FpsOverlay.Core.Update;

/// <summary>Quy tắc lúc chuyển giao sang bản mới (<c>--update-after</c>) và dọn file tải cũ (thuần logic).</summary>
public static class UpdateHandover
{
    /// <summary>Tên tiến trình của app (không đuôi .exe), như <c>Process.ProcessName</c> trả về.</summary>
    public const string ProcessName = "FpsOverlay";

    /// <summary>Tiền tố tên file exe tải về trong thư mục Temp (mỗi lần một tên riêng).</summary>
    public const string TempFilePrefix = "FpsOverlay_update.";

    /// <summary>
    /// Thư mục con của Temp nơi bản cập nhật (exe single-file) bung thư viện native khi chạy (biến môi trường
    /// <c>DOTNET_BUNDLE_EXTRACT_BASE_DIR</c>), thay cho %TEMP% mà tiến trình không Admin ghi được.
    /// </summary>
    public const string ExtractDirectoryName = "extract";

    /// <summary>File tải trong Temp cũ hơn mức này thì bản cài xoá lúc khởi động.</summary>
    public static readonly TimeSpan StaleAge = TimeSpan.FromHours(1);

    /// <summary>
    /// Tiến trình mang PID được truyền qua <c>--update-after</c> đúng là bản cài cũ (tên FpsOverlay, chạy từ exe đã cài) thì
    /// mới chờ nó thoát; PID của tiến trình khác (PID đã bị dùng lại, tham số giả) thì không chờ.
    /// </summary>
    public static bool IsOldInstance(string? processName, string? processPath, string installedExePath)
    {
        if (string.IsNullOrEmpty(processPath) || string.IsNullOrEmpty(installedExePath)) return false;
        if (!string.Equals(processName, ProcessName, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(processPath), Path.GetFullPath(installedExePath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>File <c>.exe</c> trong Temp sửa lần cuối quá <see cref="StaleAge"/> (không còn lần tải/cài nào đang dùng).</summary>
    public static bool IsStaleTempExe(string path, DateTime lastWriteUtc, DateTime nowUtc) =>
        string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) && nowUtc - lastWriteUtc > StaleAge;
}
