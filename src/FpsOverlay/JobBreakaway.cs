using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FpsOverlay.Core;

namespace FpsOverlay;

/// <summary>
/// App chạy từ tác vụ Task Scheduler (khởi động cùng Windows) nằm trong Job Object của tác vụ; tiến trình con thừa hưởng job
/// đó và có thể bị giết cùng tác vụ khi app cũ thoát. Bản cập nhật (và bản cài nó mở lại) phải sống tiếp sau khi app cũ
/// thoát, nên khi đang trong job thì chạy bằng CreateProcess với CREATE_BREAKAWAY_FROM_JOB (cố gắng hết sức).
/// </summary>
static class JobBreakaway
{
    /// <summary>Tiến trình này đang nằm trong một Job Object (bất kỳ). Không hỏi được thì coi như không.</summary>
    public static bool IsInJob()
    {
        if (NativeMethods.IsProcessInJob(NativeMethods.GetCurrentProcess(), IntPtr.Zero, out var inJob)) return inJob;
        Trace.TraceWarning($"IsProcessInJob lỗi {Marshal.GetLastWin32Error()}.");
        return false;
    }

    /// <summary>
    /// Đang trong job: chạy <paramref name="start"/> (FileName, ArgumentList, WorkingDirectory, Environment) tách khỏi job
    /// và trả về true. Trả về false khi không trong job, hoặc job không cho tách (ERROR_ACCESS_DENIED) / cần hỏi quyền
    /// (ERROR_ELEVATION_REQUIRED): người gọi chạy như cũ (Process.Start). Lỗi khác thì ném <see cref="Win32Exception"/>.
    /// <paramref name="useEnvironment"/>: false = con thừa hưởng môi trường của tiến trình này (không đọc start.Environment).
    /// </summary>
    public static bool TryStart(ProcessStartInfo start, bool useEnvironment)
    {
        if (!IsInJob()) return false;

        var commandLine = new StringBuilder(Win32CommandLine.Build(start.FileName, start.ArgumentList));
        var environment = IntPtr.Zero;
        var info = new NativeMethods.PROCESS_INFORMATION();
        try
        {
            if (useEnvironment)
                environment = Marshal.StringToHGlobalUni(Win32CommandLine.EnvironmentBlock(start.Environment));
            var startup = new NativeMethods.STARTUPINFO { cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>() };
            var flags = NativeMethods.CREATE_BREAKAWAY_FROM_JOB | (useEnvironment ? NativeMethods.CREATE_UNICODE_ENVIRONMENT : 0);
            if (NativeMethods.CreateProcess(start.FileName, commandLine, IntPtr.Zero, IntPtr.Zero, false, flags, environment,
                    string.IsNullOrEmpty(start.WorkingDirectory) ? null : start.WorkingDirectory, ref startup, out info))
                return true;

            var error = Marshal.GetLastWin32Error();
            if (error is NativeMethods.ERROR_ACCESS_DENIED or NativeMethods.ERROR_ELEVATION_REQUIRED)
            {
                Trace.TraceWarning($"Không tách được {start.FileName} khỏi job (lỗi {error}); chạy trong job, có thể bị tắt cùng tác vụ.");
                return false;
            }
            throw new Win32Exception(error);
        }
        finally
        {
            if (info.hThread != IntPtr.Zero) NativeMethods.CloseHandle(info.hThread);
            if (info.hProcess != IntPtr.Zero) NativeMethods.CloseHandle(info.hProcess);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
        }
    }
}
