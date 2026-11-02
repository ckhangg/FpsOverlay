using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FpsOverlay.Core;

namespace FpsOverlay;

/// <summary>Chạy PresentMon.exe ngầm và đưa từng khung hình nó ghi nhận vào callback.</summary>
sealed class PresentMonRunner : IDisposable
{
    const string ExeName = "PresentMon.exe";
    const string Arguments =
        "--output_stdout --no_console_stats --v1_metrics --stop_existing_session --session_name FpsOverlay";

    readonly CancellationTokenSource _cts = new();
    readonly StringBuilder _stderr = new();
    Process? _process;
    IntPtr _job;
    bool _disposed;

    /// <summary>PresentMon tự thoát khi app chưa yêu cầu. Tham số là phần stderr cuối cùng nó in ra.</summary>
    public event Action<string>? Exited;

    public void Start(Action<FrameSample> onFrame)
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, ExeName);
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"Không tìm thấy {ExeName}.", exePath);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo(exePath, Arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (_stderr) _stderr.AppendLine(e.Data);
        };
        process.Exited += (_, _) =>
        {
            if (_disposed) return;
            string message;
            lock (_stderr) message = _stderr.ToString().Trim();
            Exited?.Invoke(message);
        };

        process.Start();
        _process = process;
        AttachToJob(process);
        process.BeginErrorReadLine();

        _ = Task.Run(() => PresentMonCsvParser.ReadFramesAsync(process.StandardOutput, onFrame, _cts.Token));
    }

    // Gắn PresentMon vào Job Object: khi app thoát (kể cả bị End task), Windows tự tắt PresentMon.
    void AttachToJob(Process process)
    {
        _job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (_job == IntPtr.Zero)
            return;

        var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = { LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE },
        };
        NativeMethods.SetInformationJobObject(_job, NativeMethods.JobObjectExtendedLimitInformation,
            ref info, (uint)Marshal.SizeOf(info));
        NativeMethods.AssignProcessToJobObject(_job, process.Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();

        if (_process is { HasExited: false })
        {
            try { _process.Kill(); } catch (InvalidOperationException) { }
        }
        _process?.Dispose();

        if (_job != IntPtr.Zero)
            NativeMethods.CloseHandle(_job);
    }
}
