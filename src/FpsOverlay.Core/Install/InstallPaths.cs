namespace FpsOverlay.Core.Install;

/// <summary>Đường dẫn thư mục cài, tính từ thư mục Program Files mà Windows báo.</summary>
public static class InstallPaths
{
    public const string DirectoryName = "FpsOverlay";

    /// <summary>
    /// <c>&lt;programFiles&gt;\FpsOverlay</c>, chỉ khi <paramref name="programFiles"/> là đường dẫn tuyệt đối đầy đủ.
    /// Rỗng hoặc tương đối (vd. "FpsOverlay" theo thư mục hiện tại, chỗ người dùng ghi được) thì false: không cài được.
    /// </summary>
    public static bool TryGetInstallDirectory(string? programFiles, out string directory)
    {
        directory = "";
        if (string.IsNullOrWhiteSpace(programFiles) || !Path.IsPathFullyQualified(programFiles)) return false;
        var root = Path.GetFullPath(programFiles).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root.Length == 0 || root.EndsWith(':')) return false;   // "C:\" (gốc ổ đĩa) cũng không nhận
        directory = Path.Combine(root, DirectoryName);
        return true;
    }
}
