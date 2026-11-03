using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using FpsOverlay.Core.Autostart;

namespace FpsOverlay;

/// <summary>
/// Bật/tắt khởi động cùng Windows bằng tác vụ Task Scheduler (qua schtasks.exe). Nguồn sự thật là chính tác vụ,
/// không lưu trong file cài đặt: ai xoá tác vụ trong Task Scheduler thì ô tick cũng tự bỏ. Gọi từ luồng UI.
/// Tác vụ chạy exe với quyền Admin mỗi lần đăng nhập nên chỉ được trỏ vào exe trong thư mục cài (chỉ Admin ghi được):
/// bản chạy ở chỗ khác (--no-install) chỉ đọc trạng thái, không tạo, không sửa.
/// </summary>
sealed class AutostartService
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    readonly string _exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FpsOverlay.exe");

    public bool IsEnabled { get; private set; }

    /// <summary>Được tạo/sửa/xoá tác vụ không: chỉ khi đang chạy bản đã cài.</summary>
    public bool CanManage { get; } = Installer.IsRunningInstalled;

    /// <summary>Phát khi <see cref="IsEnabled"/> đổi, để menu khay và cửa sổ cài đặt đổi theo nhau.</summary>
    public event Action<bool>? Changed;

    /// <summary>
    /// Đọc lại trạng thái tác vụ (ngoài luồng UI để overlay không khựng), vì người dùng có thể xoá tác vụ trong
    /// Task Scheduler lúc app đang chạy. Bản đã cài: nếu tác vụ trỏ tới exe ở chỗ khác thì tạo lại cho đúng.
    /// Bản không cài: chỉ đọc.
    /// </summary>
    public async Task RefreshAsync() => SetEnabled(await Task.Run(() => CanManage ? QueryAndRepair() : Exists()));

    /// <summary>Bật hoặc tắt. Trả về false và lý do nếu không làm được; <see cref="IsEnabled"/> luôn đúng với thực tế.</summary>
    public bool TrySet(bool enable, out string error)
    {
        if (!CanManage)
        {
            error = "";
            return false;
        }
        bool ok;
        if (enable)
            ok = TryCreate(_exePath, out error);
        else
        {
            var (exitCode, _, stderr) = Run($"/Delete /TN \"{AutostartTask.TaskName}\" /F");
            ok = exitCode == 0;
            error = stderr;
        }
        if (!ok)
        {
            // schtasks báo lỗi nhưng thực tế đã đúng ý (vd. tác vụ bị xoá sẵn trong Task Scheduler): không coi là lỗi.
            var exists = Exists();
            SetEnabled(exists);
            return exists == enable;
        }
        SetEnabled(enable);
        return true;
    }

    /// <summary>
    /// Bước cài/cập nhật (chạy từ exe ngoài thư mục cài): tác vụ đang có thì tạo lại trỏ vào <paramref name="exePath"/>,
    /// chưa có thì thôi. Chỉ nhận đúng exe đã cài. Lỗi bỏ qua: bản cài khi chạy sẽ tự sửa (QueryAndRepair).
    /// </summary>
    public void Retarget(string exePath)
    {
        if (!string.Equals(Path.GetFullPath(exePath), Path.GetFullPath(Installer.InstalledExePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Autostart may only point to the installed exe.", nameof(exePath));
        if (Exists() && !TryCreate(exePath, out var error))
            Trace.TraceWarning($"Không trỏ lại được tác vụ khởi động cùng Windows: {error}");
    }

    /// <summary>Bước gỡ cài đặt: xoá tác vụ nếu có. Trả về false và lý do nếu tác vụ vẫn còn.</summary>
    public static bool DeleteTask(out string error)
    {
        error = "";
        if (!Exists()) return true;
        var (exitCode, _, stderr) = Run($"/Delete /TN \"{AutostartTask.TaskName}\" /F");
        error = stderr;
        return exitCode == 0 || !Exists();
    }

    bool QueryAndRepair()
    {
        var (exitCode, output, _) = Run($"/Query /TN \"{AutostartTask.TaskName}\" /XML");
        var exists = exitCode == 0;
        // Tạo lại lỗi thì tác vụ cũ vẫn còn, nên vẫn tính là đang bật.
        if (exists && !string.Equals(AutostartTask.ReadCommand(output), _exePath, StringComparison.OrdinalIgnoreCase))
            TryCreate(_exePath, out _);
        return exists;
    }

    static bool Exists() => Run($"/Query /TN \"{AutostartTask.TaskName}\"").ExitCode == 0;

    static bool TryCreate(string exePath, out string error)
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"FpsOverlay-task-{Guid.NewGuid():N}.xml");
        try
        {
            var user = WindowsIdentity.GetCurrent().Name;
            // schtasks đọc file theo khai báo encoding trong XML (UTF-16, có BOM).
            var bytes = Encoding.Unicode.GetPreamble()
                .Concat(Encoding.Unicode.GetBytes(AutostartTask.BuildXml(exePath, user))).ToArray();
            // %TEMP% thì tiến trình thường cũng ghi được: giữ file mở, chỉ cho đọc, suốt lúc schtasks chạy để không ai
            // tráo nội dung (tráo được là lệnh của họ chạy với quyền Admin mỗi lần đăng nhập). CreateNew: không dùng file có sẵn.
            using var stream = new FileStream(xmlPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            stream.Write(bytes);
            stream.Flush();
            var (exitCode, _, stderr) = Run($"/Create /TN \"{AutostartTask.TaskName}\" /XML \"{xmlPath}\" /F");
            error = stderr;
            return exitCode == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch (Exception) { }   // chạy sau khi stream đã đóng
        }
    }

    void SetEnabled(bool enabled)
    {
        if (enabled == IsEnabled) return;
        IsEnabled = enabled;
        Changed?.Invoke(enabled);
    }

    static (int ExitCode, string Output, string Error) Run(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                try { process.Kill(); } catch (InvalidOperationException) { }
                return (-1, "", "schtasks.exe timed out");
            }
            return (process.ExitCode, output.Result, error.Result.Trim());
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return (-1, "", ex.Message);
        }
    }
}
