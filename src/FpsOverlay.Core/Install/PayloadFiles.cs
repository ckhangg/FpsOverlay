using System.Diagnostics;
using System.IO.Compression;

namespace FpsOverlay.Core.Install;

/// <summary>
/// Thay nội dung thư mục cài bằng bản nhiều file trong <c>Payload.zip</c>. Bung vào thư mục tạm <b>bên trong</b> thư mục cài
/// (thừa hưởng ACL của Program Files), chuyển từng file vào chỗ, xoá file cũ không còn trong payload, ghi dấu
/// <see cref="HashFileName"/> sau cùng. Không đụng thư mục <see cref="TempDirectoryName"/> (PawnIoSetup tải về đó).
/// Người gọi kiểm tra thư mục cài trước (đường dẫn đúng, không phải liên kết) và bảo đảm bản cài không đang chạy.
/// </summary>
public static class PayloadFiles
{
    /// <summary>SHA-256 (hex) của Payload.zip đã cài; ghi sau cùng, xoá đầu tiên khi cập nhật.</summary>
    public const string HashFileName = "payload.sha256";

    /// <summary>Thư mục con PawnIoSetup tải bộ cài về; cài/cập nhật không đụng tới.</summary>
    public const string TempDirectoryName = "Temp";

    /// <summary>Tiền tố thư mục tạm bung payload (một thư mục mới mỗi lần cài).</summary>
    public const string StagingPrefix = ".staging-";

    /// <summary>Nội dung dấu payload trong thư mục cài; null khi không có (bản cài cũ kiểu single-file) hoặc không đọc được.</summary>
    public static string? ReadHash(string installDirectory)
    {
        try
        {
            var path = Path.Combine(installDirectory, HashFileName);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cài/cập nhật <paramref name="installDirectory"/> (đã có sẵn) từ <paramref name="payloadZip"/>. Ném lỗi nếu không làm được;
    /// dấu payload cũ bị xoá trước tiên nên dừng giữa chừng thì lần sau vẫn thấy "khác" và cập nhật lại.
    /// </summary>
    /// <param name="payloadHash">Ghi vào <see cref="HashFileName"/> khi xong.</param>
    /// <param name="requiredFiles">Tên file (cấp đầu) payload bắt buộc phải có, vd. FpsOverlay.exe.</param>
    /// <param name="keepPath">File không bao giờ xoá dù không có trong payload (exe đang chạy), hoặc null.</param>
    public static void Apply(string installDirectory, Stream payloadZip, string payloadHash, IEnumerable<string> requiredFiles,
        string? keepPath = null)
    {
        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDirectory));
        DeleteLeftoverStaging(dir);
        var hashFile = Path.Combine(dir, HashFileName);
        if (File.Exists(hashFile)) File.Delete(hashFile);

        var existing = ListInstalledFiles(dir, out var existingDirectories);   // trước khi chuyển file mới vào
        var staging = Path.Combine(dir, StagingPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            var payload = Extract(payloadZip, staging);
            foreach (var required in requiredFiles)
                if (!payload.Contains(required))
                    throw new InvalidDataException($"Payload is missing {required}.");

            foreach (var relative in payload)
            {
                var target = Path.Combine(dir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(Path.Combine(staging, relative), target, overwrite: true);
            }
            DeleteObsolete(dir, existing, existingDirectories, payload, keepPath);
            WriteHash(hashFile, payloadHash);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    /// <summary>
    /// Bung zip vào <paramref name="staging"/> (thư mục mới, rỗng). Trả về đường dẫn tương đối của mọi file. Từ chối mục thoát
    /// ra ngoài staging (zip-slip: <c>..</c>, đường dẫn tuyệt đối, ổ đĩa), có <c>:</c> (luồng phụ NTFS), trùng tên (không phân
    /// biệt hoa thường), hoặc dùng tên dành riêng ở cấp đầu (<see cref="IsReservedName"/>).
    /// </summary>
    static HashSet<string> Extract(Stream payloadZip, string staging)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(staging)) + Path.DirectorySeparatorChar;
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var zip = new ZipArchive(payloadZip, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            var inside = target.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(target + Path.DirectorySeparatorChar, root, StringComparison.OrdinalIgnoreCase);
            if (!inside)
                throw new InvalidDataException($"Payload entry escapes the staging folder: {entry.FullName}");
            var relative = target.Length > root.Length ? Path.TrimEndingDirectorySeparator(target[root.Length..]) : "";
            if (relative.Contains(':') || (relative.Length > 0 && IsReservedName(relative)))
                throw new InvalidDataException($"Payload entry has an invalid name: {entry.FullName}");

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))   // mục thư mục
            {
                Directory.CreateDirectory(target);
                continue;
            }
            if (relative.Length == 0 || !files.Add(relative))
                throw new InvalidDataException($"Payload entry is empty or duplicated: {entry.FullName}");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
        return files;
    }

    // Tên ở cấp đầu của thư mục cài mà payload không được có, và cài/cập nhật không xoá: Temp, dấu payload, staging.
    static bool IsReservedName(string relative)
    {
        var top = relative.Split(Path.DirectorySeparatorChar, 2)[0];
        return string.Equals(top, TempDirectoryName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(top, HashFileName, StringComparison.OrdinalIgnoreCase) ||
               top.StartsWith(StagingPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// File (đường dẫn tương đối) và thư mục con đang có trong thư mục cài, bỏ qua tên dành riêng ở cấp đầu. Gặp liên kết
    /// (junction/symlink) thì ném lỗi: không đi theo, không xoá gì qua liên kết.
    /// </summary>
    static List<string> ListInstalledFiles(string dir, out List<string> directories)
    {
        var files = new List<string>();
        var dirs = new List<string>();
        void Walk(DirectoryInfo current, string prefix)
        {
            foreach (var entry in current.EnumerateFileSystemInfos())
            {
                var relative = prefix + entry.Name;
                if (prefix.Length == 0 && IsReservedName(relative)) continue;
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException($"Refusing to touch a link in the install folder: {entry.FullName}");
                if (entry is DirectoryInfo sub)
                {
                    dirs.Add(relative);
                    Walk(sub, relative + Path.DirectorySeparatorChar);
                }
                else files.Add(relative);
            }
        }
        Walk(new DirectoryInfo(dir), "");
        directories = dirs;
        return files;
    }

    // Xoá file cũ không còn trong payload (bản cài single-file cũ chỉ có exe, PresentMon, giấy phép — đều bị ghi đè; bản nhiều
    // file cũ có thể còn dll thừa), rồi xoá thư mục con cũ đã rỗng (sâu nhất trước). Mọi đường dẫn đều từ ListInstalledFiles.
    static void DeleteObsolete(string dir, List<string> existing, List<string> existingDirectories, HashSet<string> payload,
        string? keepPath)
    {
        var keep = keepPath is null ? null : Path.GetFullPath(keepPath);
        foreach (var relative in existing)
        {
            if (payload.Contains(relative)) continue;
            var path = Path.Combine(dir, relative);
            if (!File.Exists(path)) continue;
            if (string.Equals(Path.GetFullPath(path), keep, StringComparison.OrdinalIgnoreCase)) continue;
            File.SetAttributes(path, FileAttributes.Normal);   // file chỉ đọc cũng xoá được
            File.Delete(path);
        }
        foreach (var relative in existingDirectories.OrderByDescending(d => d.Length))
        {
            var path = Path.Combine(dir, relative);
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
    }

    // Qua file tạm rồi đổi tên: không bao giờ có dấu ghi dở. File tạm sót lại bị lần cài sau xoá (không có trong payload).
    static void WriteHash(string hashFile, string payloadHash)
    {
        var temp = $"{hashFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, payloadHash);
            File.Move(temp, hashFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    // Thư mục staging của lần cài trước bị dừng giữa chừng. Directory.Delete đệ quy không đi theo liên kết (chỉ gỡ liên kết).
    static void DeleteLeftoverStaging(string dir)
    {
        foreach (var leftover in Directory.GetDirectories(dir, StagingPrefix + "*"))
            TryDeleteDirectory(leftover);
    }

    // Dọn dẹp, không che lỗi chính: lỗi chỉ ghi vết.
    static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Không xoá được thư mục tạm {path}: {ex.Message}");
        }
    }
}
