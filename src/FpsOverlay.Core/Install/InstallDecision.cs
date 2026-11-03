namespace FpsOverlay.Core.Install;

public enum InstallAction { Install, Update, RunInstalled }

/// <summary>Chạy exe từ ngoài thư mục cài thì làm gì: cài mới, cập nhật bản cài, hay chạy luôn bản đã cài.</summary>
public static class InstallDecision
{
    /// <param name="running">Phiên bản exe đang chạy.</param>
    /// <param name="runningHash">SHA-256 (hex) của exe đang chạy.</param>
    /// <param name="installed">Phiên bản bản đã cài; null = chưa cài.</param>
    /// <param name="installedHash">SHA-256 (hex) của bản đã cài; null = không đọc được (coi như khác).</param>
    public static InstallAction Decide(Version running, string runningHash, Version? installed, string? installedHash)
    {
        if (installed is null) return InstallAction.Install;
        var order = running.CompareTo(installed);
        if (order > 0) return InstallAction.Update;
        if (order < 0) return InstallAction.RunInstalled;   // bản cài mới hơn: không hạ cấp
        // Cùng phiên bản mà khác nội dung (build lại chưa tăng số): cập nhật.
        return string.Equals(runningHash, installedHash, StringComparison.OrdinalIgnoreCase)
            ? InstallAction.RunInstalled
            : InstallAction.Update;
    }
}
