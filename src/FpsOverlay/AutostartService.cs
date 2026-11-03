using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using FpsOverlay.Core.Autostart;

namespace FpsOverlay;

/// <summary>
/// Bật/tắt khởi động cùng Windows bằng tác vụ Task Scheduler (qua schtasks.exe). Nguồn sự thật là chính tác vụ,
/// không lưu trong file cài đặt: ai xoá tác vụ trong Task Scheduler thì ô tick cũng tự bỏ. Dùng trên luồng UI.
/// </summary>
sealed class AutostartService
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    readonly string _exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FpsOverlay.exe");

    public bool IsEnabled { get; private set; }

    /// <summary>Phát khi <see cref="IsEnabled"/> đổi, để menu khay và cửa sổ cài đặt đổi theo nhau.</summary>
    public event Action<bool>? Changed;

    /// <summary>
    /// Đọc trạng thái tác vụ. Nếu tác vụ trỏ tới exe ở chỗ khác (thư mục app bị chuyển đi) thì tạo lại cho đúng.
    /// </summary>
    public void Refresh()
    {
        var (exitCode, output, _) = Run($"/Query /TN \"{AutostartTask.TaskName}\" /XML");
        var enabled = exitCode == 0;
        // Tạo lại lỗi thì tác vụ cũ vẫn còn, nên vẫn tính là đang bật.
        if (enabled && !string.Equals(AutostartTask.ReadCommand(output), _exePath, StringComparison.OrdinalIgnoreCase))
            TryCreate(out _);
        SetEnabled(enabled);
    }

    /// <summary>Bật hoặc tắt. Trả về false và lý do nếu schtasks báo lỗi; trạng thái vẫn đúng với thực tế.</summary>
    public bool TrySet(bool enable, out string error)
    {
        bool ok;
        if (enable)
            ok = TryCreate(out error);
        else
        {
            var (exitCode, _, stderr) = Run($"/Delete /TN \"{AutostartTask.TaskName}\" /F");
            ok = exitCode == 0;
            error = stderr;
        }
        if (ok)
            SetEnabled(enable);
        else
            Refresh();
        return ok;
    }

    bool TryCreate(out string error)
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"FpsOverlay-task-{Guid.NewGuid():N}.xml");
        try
        {
            var user = WindowsIdentity.GetCurrent().Name;
            // schtasks đọc file theo khai báo encoding trong XML (UTF-16).
            File.WriteAllText(xmlPath, AutostartTask.BuildXml(_exePath, user), Encoding.Unicode);
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
            try { File.Delete(xmlPath); } catch (Exception) { }
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
