namespace FpsOverlay.Core.Install;

public enum InstallAction { Install, Update, RunInstalled }

/// <summary>Chạy exe từ ngoài thư mục cài thì làm gì: cài mới, cập nhật bản cài, hay chạy luôn bản đã cài.</summary>
public static class InstallDecision
{
    /// <param name="running">Phiên bản exe đang chạy (bản mang đi).</param>
    /// <param name="runningPayloadHash">SHA-256 (hex) của <c>Payload.zip</c> nhúng trong exe đang chạy.</param>
    /// <param name="installed">Phiên bản bản đã cài; null = chưa cài.</param>
    /// <param name="installedPayloadHash">
    /// Nội dung <c>payload.sha256</c> trong thư mục cài; null = không có (bản cài cũ kiểu single-file) hoặc không đọc
    /// được — coi như khác. So hash payload chứ không so exe: exe bản cài (apphost nhiều file) khác exe bản mang đi.
    /// </param>
    public static InstallAction Decide(Version running, string runningPayloadHash, Version? installed, string? installedPayloadHash)
    {
        if (installed is null) return InstallAction.Install;
        var order = running.CompareTo(installed);
        if (order > 0) return InstallAction.Update;
        if (order < 0) return InstallAction.RunInstalled;   // bản cài mới hơn: không hạ cấp
        // Cùng phiên bản mà khác payload (build lại chưa tăng số), hoặc bản cài không có dấu payload: cập nhật.
        return string.Equals(runningPayloadHash, installedPayloadHash, StringComparison.OrdinalIgnoreCase)
            ? InstallAction.RunInstalled
            : InstallAction.Update;
    }
}
