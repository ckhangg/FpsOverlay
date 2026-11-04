using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using FpsOverlay.Core.Update;

namespace FpsOverlay;

/// <summary>
/// Thư mục tạm chỉ Admin ghi được, nơi tải những thứ sẽ chạy với quyền Admin (bộ cài PawnIO, exe bản cập nhật). Không
/// bao giờ dùng %TEMP% của người dùng: tiến trình không Admin ghi được ở đó và tráo được file giữa lúc tải và lúc chạy.
/// </summary>
static class SecureTemp
{
    static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>
    /// Có bản cài (bản setup vừa cài xong, hoặc bản đã cài đang chạy): <c>&lt;thư mục cài&gt;\Temp</c>, thừa hưởng quyền của
    /// Program Files. Không có (<c>--no-install</c>): <c>%ProgramData%\FpsOverlay\Temp</c>, đặt ACL riêng chỉ Administrators
    /// và SYSTEM. Thư mục là liên kết (junction/symlink) thì từ chối. Ném lỗi nếu không chuẩn bị được.
    /// </summary>
    public static string PrepareDirectory()
    {
        string directory;
        if (Installer.IsInstallPathValid && File.Exists(Installer.InstalledExePath))
        {
            RejectReparsePoint(Installer.InstallDirectory);
            directory = Path.Combine(Installer.InstallDirectory, Installer.TempDirectoryName);
            Directory.CreateDirectory(directory);
            RejectReparsePoint(directory);
        }
        else
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrEmpty(programData) || !Path.IsPathFullyQualified(programData))
                throw new InvalidOperationException($"ProgramData folder is not an absolute path: \"{programData}\"");
            var root = Path.Combine(programData, Core.Install.InstallPaths.DirectoryName);
            directory = Path.Combine(root, "Temp");
            SecureDirectory(root);
            SecureDirectory(directory);
        }
        return directory;
    }

    /// <summary>
    /// Bản cài gọi lúc khởi động: xoá <c>*.exe</c> trong <c>&lt;thư mục cài&gt;\Temp</c> cũ hơn một giờ (exe bản cập nhật đã
    /// chạy xong không tự xoá được mình; bộ cài PawnIO sót lại) và thư mục bung thư viện của bản cập nhật
    /// (<see cref="UpdateHandover.ExtractDirectoryName"/>) cũng cũ hơn một giờ. Không tạo thư mục; cái nào lỗi thì bỏ qua.
    /// </summary>
    public static void DeleteStaleExecutables()
    {
        if (!Installer.IsRunningInstalled) return;
        try
        {
            var directory = Path.Combine(Installer.InstallDirectory, Installer.TempDirectoryName);
            if (!Directory.Exists(directory)) return;
            RejectReparsePoint(directory);
            var now = DateTime.UtcNow;
            foreach (var file in Directory.EnumerateFiles(directory, "*.exe"))
            {
                try
                {
                    if (UpdateHandover.IsStaleTempExe(file, File.GetLastWriteTimeUtc(file), now))
                        File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            var extract = Path.Combine(directory, UpdateHandover.ExtractDirectoryName);
            if (Directory.Exists(extract) && now - Directory.GetLastWriteTimeUtc(extract) > UpdateHandover.StaleAge)
            {
                try
                {
                    RejectReparsePoint(extract);
                    Directory.Delete(extract, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không dọn được thư mục Temp: {ex.Message}");
        }
    }

    // Tạo (hoặc đặt lại) thư mục với ACL: chủ sở hữu Administrators, tắt thừa hưởng, chỉ Administrators + SYSTEM toàn quyền.
    // Đặt xong đọc lại để kiểm; không đúng thì ném lỗi.
    static void SecureDirectory(string path)
    {
        var security = new DirectorySecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit,
            PropagationFlags.None, AccessControlType.Allow));

        var info = new DirectoryInfo(path);
        if (!info.Exists)
            info.Create(security);   // tạo luôn với ACL; có ai tạo trước giữa chừng thì ném lỗi, lần sau đi nhánh dưới
        else
        {
            RejectReparsePoint(path);
            info.SetAccessControl(security);
        }

        info.Refresh();
        RejectReparsePoint(path);
        var actual = info.GetAccessControl();
        var owner = actual.GetOwner(typeof(SecurityIdentifier));
        if (owner is null || (!owner.Equals(Administrators) && !owner.Equals(LocalSystem)))
            throw new InvalidOperationException($"Unexpected owner on {path}: {owner}");
        if (!actual.AreAccessRulesProtected)
            throw new InvalidOperationException($"Inherited permissions on {path}");
        foreach (FileSystemAccessRule rule in actual.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (!rule.IdentityReference.Equals(Administrators) && !rule.IdentityReference.Equals(LocalSystem))
                throw new InvalidOperationException($"Unexpected permission on {path}: {rule.IdentityReference}");
    }

    static void RejectReparsePoint(string path)
    {
        if (new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException($"Refusing to use a link: {path}");
    }
}
