using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using FpsOverlay.Core.Install;
using Microsoft.Win32;

namespace FpsOverlay;

/// <summary>
/// Cài vào <c>C:\Program Files\FpsOverlay</c> (chỉ Admin ghi được): bung bản nhiều file (<c>Payload.zip</c> nhúng trong bản
/// mang đi: apphost, thư viện native, PresentMon, giấy phép) để bản cài không bung thư viện vào %TEMP%; lối tắt Start Menu
/// cho mọi người dùng, mục gỡ trong Settings → Apps. App chạy với quyền Admin nên ghi được HKLM và Program Files.
/// </summary>
static class Installer
{
    /// <summary>Tên mutex một bản chạy (App giữ nó suốt lúc chạy); dùng để biết bản cài có đang chạy không.</summary>
    public const string SingleInstanceMutexName = @"Local\FpsOverlay.SingleInstance";

    /// <summary>Thư mục con PawnIoSetup tải bộ cài về; cài/cập nhật không đụng tới.</summary>
    public const string TempDirectoryName = PayloadFiles.TempDirectoryName;

    /// <summary>Tên PresentMon trong thư mục cài (nằm sẵn trong payload).</summary>
    public const string PresentMonName = "PresentMon.exe";

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

    /// <summary>
    /// Exe đang chạy có mang theo bản cài (<c>Payload.zip</c>) không. Không có (bản đã cài nhiều file chép đi chỗ khác,
    /// hay bản <c>dotnet build</c>) thì không cài được: App báo lỗi thay vì cài một bản hỏng.
    /// </summary>
    public static bool HasPayload => EmbeddedFiles.HasPayload;

    // Băm một lần (vài chục MB), dùng cho cả Decide lẫn Install.
    static readonly Lazy<string> RunningPayloadHash = new(() => EmbeddedFiles.ResourceHash(EmbeddedFiles.PayloadName));

    /// <summary>Cài mới, cập nhật hay chạy luôn bản đã cài. Đọc file (băm payload) nên có thể ném lỗi IO.</summary>
    public static InstallAction Decide()
    {
        EnsureHasPayload();
        Version? installedVersion = null;
        string? installedHash = null;
        if (File.Exists(InstalledExePath))
        {
            // Phiên bản bản cài lấy từ FileVersion của exe (apphost mang tài nguyên phiên bản của FpsOverlay.dll, cũng sinh
            // từ <Version>), cùng 4 phần như AssemblyVersion.
            var info = FileVersionInfo.GetVersionInfo(InstalledExePath);
            installedVersion = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            installedHash = PayloadFiles.ReadHash(InstallDirectory);   // null (bản single-file cũ) → cập nhật
        }
        return InstallDecision.Decide(RunningVersion, RunningPayloadHash.Value, installedVersion, installedHash);
    }

    /// <summary>
    /// Cài hoặc cập nhật (gọi ngoài luồng UI: bung payload hơn trăm MB). <see cref="PayloadFiles.Apply"/>: bung
    /// <c>Payload.zip</c> vào thư mục tạm trong thư mục cài, chuyển từng file vào chỗ, xoá file cũ không còn trong payload
    /// (trừ <c>Temp</c>), ghi <c>payload.sha256</c> sau cùng. Ném lỗi nếu không làm được; gọi lại an toàn. Không đụng tới
    /// cài đặt ở %AppData% và log. Bản cài phải đang không chạy (InstallWindow kiểm tra mutex trước).
    /// </summary>
    public static void Install()
    {
        EnsureHasPayload();
        EnsureInstallPathValid();
        Directory.CreateDirectory(InstallDirectory);
        var dir = SafeInstallDirectory();   // đúng <Program Files>\FpsOverlay, không phải liên kết

        using (var payload = EmbeddedFiles.Open(EmbeddedFiles.PayloadName))
            PayloadFiles.Apply(dir, payload, RunningPayloadHash.Value, [ExeName, PresentMonName], keepPath: RunningExePath);

        CreateShortcut();
        WriteUninstallEntry();
    }

    static void EnsureHasPayload()
    {
        if (!HasPayload)
            throw new InvalidOperationException("This copy has no setup payload (Payload.zip); build it with dotnet publish.");
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

    // Bản cài đang tự gỡ: thư mục cài (đã kiểm bằng SafeInstallDirectory) chờ FinishUninstall hẹn xoá.
    static string? _pendingSelfDelete;

    /// <summary>
    /// Gỡ: xoá tác vụ khởi động cùng Windows, lối tắt, mục đăng ký và thư mục cài. Giữ cài đặt (%AppData%) và log.
    /// Làm hết các bước dù có bước lỗi; lỗi gom lại ném ở cuối. Exe đang chạy từ thư mục cài thì không tự xoá được:
    /// bước thư mục và mục đăng ký để dành cho <see cref="FinishUninstall"/> (gọi sau hộp thông báo, ngay trước khi thoát).
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
        // Mục gỡ cài đặt xoá sau cùng, và chỉ khi đã xoá được thư mục: còn sót file thì vẫn gỡ lại được từ Settings → Apps.
        // Bản cài tự gỡ thì cả hai để cho cmd của FinishUninstall (xoá mục đăng ký sau khi rmdir xong).
        if (Step(DeleteInstallDirectory) && _pendingSelfDelete is null)
            Step(DeleteUninstallEntry);

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join("\n", errors));
    }

    /// <summary>
    /// Bước cuối của gỡ cài đặt khi bản cài tự gỡ (không thì không làm gì): gọi SAU hộp thông báo xong/lỗi, ngay trước
    /// Shutdown. Thứ tự: thông báo → hẹn cmd → app thoát → cmd thấy tiến trình này đã thoát mới rmdir → rmdir xong mới xoá
    /// mục đăng ký. Hộp thông báo không còn giữ app sống (và khoá file) trong lúc cmd đếm lượt thử; rmdir không được thì
    /// mục gỡ vẫn còn, gỡ lại được từ Settings → Apps.
    /// </summary>
    public static void FinishUninstall()
    {
        if (_pendingSelfDelete is not { } dir) return;
        _pendingSelfDelete = null;
        ScheduleSelfDelete(dir);
    }

    static void DeleteUninstallEntry()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        hklm.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
    }

    /// <summary>
    /// Chạy bản đã cài (nó cần Admin; bản này đã là Admin nên không hiện UAC lần nữa). <paramref name="arguments"/>: tham số
    /// truyền cho bản cài (vd. <see cref="PawnIoSetup.OfferedArgument"/>).
    /// ShellExecute từ tiến trình đã là Admin vẫn gọi CreateProcess ngay trong tiến trình này, nên bản cài thừa hưởng Job
    /// Object nếu bản setup đang nằm trong job: khi đó tách khỏi job trước (xem <see cref="JobBreakaway"/>), không được
    /// thì chạy như cũ.
    /// </summary>
    public static void LaunchInstalled(params string[] arguments)
    {
        var breakaway = new ProcessStartInfo(InstalledExePath) { WorkingDirectory = InstallDirectory };
        foreach (var argument in arguments) breakaway.ArgumentList.Add(argument);
        if (JobBreakaway.TryStart(breakaway, useEnvironment: false)) return;

        var start = new ProcessStartInfo(InstalledExePath)
        {
            UseShellExecute = true,
            WorkingDirectory = InstallDirectory,
            Arguments = string.Join(' ', arguments),   // chỉ là cờ cố định kiểu --abc, không có dấu cách
        };
        using var _ = Process.Start(start);
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

        // Bản cài nhiều file đang chạy khoá cả exe lẫn các dll đã nạp (coreclr, wpfgfx, ...): không xoá trước được gì,
        // để cmd xoá cả thư mục sau khi app thoát (FinishUninstall hẹn, sau hộp thông báo).
        _pendingSelfDelete = dir;
    }

    // Cmd chờ tiến trình này thoát rồi mới rmdir, xoá xong thư mục mới xoá mục gỡ cài đặt (xem SelfDeleteCommand).
    static void ScheduleSelfDelete(string dir)
    {
        var system = Environment.SystemDirectory;
        var command = SelfDeleteCommand.Build(system, Environment.ProcessId, Path.GetFileName(RunningExePath), dir,
            $@"HKLM\{UninstallKeyPath}");
        using var _ = Process.Start(new ProcessStartInfo(Path.Combine(system, "cmd.exe"), $"/d /s /c \"{command}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = system,   // không đứng trong thư mục sắp xoá
            // cmd đọc COMSPEC từ môi trường thừa hưởng; ghim về System32 để không chạy cmd lạ.
            Environment = { ["COMSPEC"] = Path.Combine(system, "cmd.exe") },
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
