using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using FpsOverlay.Core.Install;
using Microsoft.Win32;

namespace FpsOverlay;

/// <summary>
/// Cài vào <c>C:\Program Files\FpsOverlay</c> (chỉ Admin ghi được): chép exe, bung PresentMon và giấy phép, lối tắt
/// Start Menu cho mọi người dùng, mục gỡ trong Settings → Apps. App chạy với quyền Admin nên ghi được HKLM và Program Files.
/// </summary>
static class Installer
{
    /// <summary>Tên mutex một bản chạy (App giữ nó suốt lúc chạy); dùng để biết bản cài có đang chạy không.</summary>
    public const string SingleInstanceMutexName = @"Local\FpsOverlay.SingleInstance";

    const string DirectoryName = InstallPaths.DirectoryName;
    const string ExeName = "FpsOverlay.exe";
    const string ShortcutName = "FPS Overlay.lnk";
    const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FpsOverlay";

    static readonly string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    /// <summary>
    /// Windows báo được thư mục Program Files tuyệt đối. Không thì không cài được: thư mục cài sẽ thành đường dẫn tương đối
    /// (chỗ người dùng ghi được), nên coi như không có bản cài, không cho cài và không cho đụng tác vụ khởi động.
    /// </summary>
    public static bool IsInstallPathValid => ValidatedDirectory is not null;

    // Kiểm một lần lúc khởi tạo (khai báo sau ProgramFiles: khởi tạo tĩnh chạy theo thứ tự trong file).
    static readonly string? ValidatedDirectory =
        InstallPaths.TryGetInstallDirectory(ProgramFiles, out var directory) ? directory : null;

    /// <summary><c>C:\Program Files\FpsOverlay</c>; chuỗi rỗng khi <see cref="IsInstallPathValid"/> là false.</summary>
    public static string InstallDirectory => ValidatedDirectory ?? "";

    /// <summary>Exe trong thư mục cài; chuỗi rỗng khi <see cref="IsInstallPathValid"/> là false.</summary>
    public static string InstalledExePath => IsInstallPathValid ? Path.Combine(InstallDirectory, ExeName) : "";

    static string RunningExePath { get; } = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ExeName);

    static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), ShortcutName);

    /// <summary>Exe đang chạy chính là bản trong thư mục cài (so đường dẫn, không phân biệt hoa thường).</summary>
    public static bool IsRunningInstalled { get; } = IsInstallPathValid &&
        string.Equals(Path.GetFullPath(RunningExePath), Path.GetFullPath(InstalledExePath), StringComparison.OrdinalIgnoreCase);

    /// <summary>Phiên bản exe đang chạy (từ &lt;Version&gt; trong csproj).</summary>
    public static Version RunningVersion { get; } = typeof(Installer).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>Một bản FPS Overlay khác (thường là bản cài, ở khay) đang giữ mutex một bản chạy.</summary>
    public static bool IsInstalledCopyRunning()
    {
        if (!Mutex.TryOpenExisting(SingleInstanceMutexName, out var mutex)) return false;
        mutex.Dispose();
        return true;
    }

    /// <summary>Cài mới, cập nhật hay chạy luôn bản đã cài. Đọc file (băm cả exe) nên có thể ném lỗi IO.</summary>
    public static InstallAction Decide()
    {
        Version? installedVersion = null;
        string? installedHash = null;
        if (File.Exists(InstalledExePath))
        {
            // Phiên bản bản cài lấy từ FileVersion của exe (cũng sinh từ <Version>), cùng 4 phần như AssemblyVersion.
            var info = FileVersionInfo.GetVersionInfo(InstalledExePath);
            installedVersion = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            try { installedHash = FileHash(InstalledExePath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }   // không đọc được: coi như khác → cập nhật
        }
        return InstallDecision.Decide(RunningVersion, FileHash(RunningExePath), installedVersion, installedHash);
    }

    /// <summary>
    /// Cài hoặc cập nhật (gọi ngoài luồng UI: chép exe vài chục MB). Ném lỗi nếu không làm được; gọi lại an toàn
    /// (mọi bước ghi đè). Không đụng tới cài đặt ở %AppData% và log.
    /// </summary>
    public static void Install()
    {
        EnsureInstallPathValid();
        Directory.CreateDirectory(InstallDirectory);

        // Chép qua file tạm (tên riêng mỗi lần) rồi đổi tên: lỗi giữa chừng không để lại exe cụt trong thư mục cài.
        var temp = $"{InstalledExePath}.{Guid.NewGuid():N}.new";
        try
        {
            File.Copy(RunningExePath, temp, overwrite: true);
            File.Move(temp, InstalledExePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }

        EmbeddedFiles.ExtractAll(InstallDirectory);
        CreateShortcut();
        WriteUninstallEntry();
    }

    /// <summary>
    /// Bản cài gọi mỗi lần khởi động: tạo lại lối tắt và mục gỡ cài đặt (ghi đè, làm bao nhiêu lần cũng được), để lần
    /// cài trước dừng giữa chừng vẫn gỡ được từ Settings → Apps. Lỗi chỉ ghi vết, không chặn app.
    /// </summary>
    public static void RepairEntries()
    {
        if (!IsRunningInstalled) return;
        try { CreateShortcut(); }
        catch (Exception ex) { Trace.TraceWarning($"Không tạo được lối tắt Start Menu: {ex.Message}"); }
        try { WriteUninstallEntry(); }
        catch (Exception ex) { Trace.TraceWarning($"Không ghi được mục gỡ cài đặt: {ex.Message}"); }
    }

    /// <summary>
    /// Gỡ: xoá tác vụ khởi động cùng Windows, lối tắt, mục đăng ký và thư mục cài. Giữ cài đặt (%AppData%) và log.
    /// Làm hết các bước dù có bước lỗi; lỗi gom lại ném ở cuối. Exe đang chạy từ thư mục cài thì không tự xoá được:
    /// khởi chạy ẩn một cmd chờ app thoát rồi xoá thư mục — gọi ngay trước khi thoát.
    /// </summary>
    public static void Uninstall()
    {
        // Đường dẫn cài không hợp lệ: không xoá gì, không đụng tác vụ khởi động (giống Install).
        EnsureInstallPathValid();
        var errors = new List<string>();
        bool Step(Action action)
        {
            try { action(); return true; }
            catch (Exception ex) { errors.Add(ex.Message); return false; }
        }

        Step(() =>
        {
            if (!AutostartService.DeleteTask(out var error)) throw new InvalidOperationException(error);
        });
        Step(() =>
        {
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
        });
        // Mục gỡ cài đặt xoá sau cùng, và chỉ khi đã xoá (hoặc hẹn xoá) được thư mục: còn sót file thì vẫn gỡ lại được từ Settings → Apps.
        if (Step(DeleteInstallDirectory))
            Step(() =>
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                hklm.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
            });

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join("\n", errors));
    }

    /// <summary>
    /// Chạy bản đã cài (nó cần Admin; bản này đã là Admin nên không hiện UAC lần nữa). <paramref name="arguments"/>: tham số
    /// truyền cho bản cài (vd. <see cref="PawnIoSetup.OfferedArgument"/>).
    /// </summary>
    public static void LaunchInstalled(params string[] arguments)
    {
        var start = new ProcessStartInfo(InstalledExePath)
        {
            UseShellExecute = true,
            WorkingDirectory = InstallDirectory,
            Arguments = string.Join(' ', arguments),   // chỉ là cờ cố định kiểu --abc, không có dấu cách
        };
        using var _ = Process.Start(start);
    }

    public static string FileHash(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    // ---------- Gỡ thư mục ----------

    // Chặn mọi đường dẫn khác: chỉ đúng <Program Files>\FpsOverlay, tuyệt đối, không phải liên kết (junction/symlink).
    /// <summary>Ném lỗi (kèm lý do ngắn) nếu Windows không báo được thư mục Program Files tuyệt đối.</summary>
    public static void EnsureInstallPathValid()
    {
        if (!IsInstallPathValid)
            throw new InvalidOperationException($"Program Files folder is not an absolute path: \"{ProgramFiles}\"");
    }

    static string SafeInstallDirectory()
    {
        EnsureInstallPathValid();
        var full = Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var ok = ProgramFiles.Length > 0 &&
                 Path.IsPathFullyQualified(ProgramFiles) &&
                 string.Equals(full, Path.Combine(Path.GetFullPath(ProgramFiles).TrimEnd(Path.DirectorySeparatorChar), DirectoryName),
                     StringComparison.OrdinalIgnoreCase) &&
                 full.EndsWith(Path.DirectorySeparatorChar + DirectoryName, StringComparison.OrdinalIgnoreCase);
        if (!ok) throw new InvalidOperationException($"Refusing to delete unexpected path: {full}");
        if (Directory.Exists(full) && new DirectoryInfo(full).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException($"Refusing to delete a link: {full}");
        return full;
    }

    static void DeleteInstallDirectory()
    {
        var dir = SafeInstallDirectory();
        if (!Directory.Exists(dir)) return;

        if (!IsRunningInstalled)
        {
            Directory.Delete(dir, recursive: true);
            return;
        }

        // Xoá trước mọi thứ trừ exe đang chạy; phần còn lại cmd xoá sau khi app thoát.
        foreach (var file in Directory.GetFiles(dir))
            if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(RunningExePath), StringComparison.OrdinalIgnoreCase))
                File.Delete(file);
        ScheduleSelfDelete(dir);
    }

    // cmd /s /c "...": bỏ đúng cặp ngoặc kép ngoài cùng, giữ nguyên ngoặc kép quanh đường dẫn bên trong.
    // Chờ ~2 giây (ping) rồi rmdir; app còn chạy (vd. đang hiện hộp thông báo) thì thử lại, tối đa ~30 lần.
    static void ScheduleSelfDelete(string dir)
    {
        var system = Environment.SystemDirectory;
        var ping = Path.Combine(system, "PING.EXE");
        var command = $"for /l %i in (1,1,30) do (\"{ping}\" -n 3 127.0.0.1 >nul & rmdir /s /q \"{dir}\" 2>nul & if not exist \"{dir}\" exit)";
        using var _ = Process.Start(new ProcessStartInfo(Path.Combine(system, "cmd.exe"), $"/d /s /c \"{command}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = system,   // không đứng trong thư mục sắp xoá
        });
    }

    // ---------- Lối tắt và mục gỡ cài đặt ----------

    static void CreateShortcut()
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(InstalledExePath);
            link.SetWorkingDirectory(InstallDirectory);
            link.SetDescription("FPS Overlay");
            link.SetIconLocation(InstalledExePath, 0);
            ((IPersistFile)link).Save(ShortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    static void WriteUninstallEntry()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.CreateSubKey(UninstallKeyPath, writable: true);
        key.SetValue("DisplayName", "FPS Overlay");
        key.SetValue("DisplayVersion", $"{RunningVersion.Major}.{RunningVersion.Minor}.{Math.Max(RunningVersion.Build, 0)}");
        key.SetValue("Publisher", "FPS Overlay");
        key.SetValue("DisplayIcon", InstalledExePath);
        key.SetValue("InstallLocation", InstallDirectory);
        key.SetValue("UninstallString", $"\"{InstalledExePath}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink { }   // không sealed: phải ép được sang interface COM

    // Thứ tự phương thức phải đúng vtable của IShellLinkW (shobjidl_core.h).
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
