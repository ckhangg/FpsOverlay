using System.Runtime.InteropServices;
using System.Text;
using FpsOverlay.Core.GameOnly;

namespace FpsOverlay;

/// <summary>
/// Thông tin về cửa sổ phía trước dùng chung cho overlay, bộ ghi log và quy tắc chỉ-hiện-khi-chơi:
/// loại cửa sổ, có phủ kín màn hình không, đường dẫn exe của tiến trình. Chỉ dùng trên luồng UI.
/// </summary>
static class ForegroundWindowInfo
{
    /// <summary>Số PID nhớ đường dẫn exe; cửa sổ phía trước chỉ có vài tiến trình nên ngần này là đủ.</summary>
    const int CacheSize = 8;

    /// <summary>PID không được hỏi lại trong chừng này thì coi như cũ (tiến trình có thể đã thoát và PID bị dùng lại).</summary>
    const long StaleAfterMs = 5000;

    static readonly StringBuilder ClassName = new(64);
    static readonly char[] PathBuffer = new char[1024];
    static readonly (int Pid, string? Path, long SeenAt)[] PathCache = new (int, string?, long)[CacheSize];

    /// <summary>Zero → None; cửa sổ của app này → Own; màn hình nền, taskbar/shell → Desktop; còn lại → Other.</summary>
    public static ForegroundKind Kind(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return ForegroundKind.None;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == (uint)Environment.ProcessId)
            return ForegroundKind.Own;
        if (hwnd == NativeMethods.GetShellWindow())
            return ForegroundKind.Desktop;
        ClassName.Clear();
        NativeMethods.GetClassName(hwnd, ClassName, ClassName.Capacity);
        return IsDesktopClass(ClassName) ? ForegroundKind.Desktop : ForegroundKind.Other;
    }

    // So trực tiếp trên StringBuilder để không cấp phát chuỗi mỗi lần dò.
    static bool IsDesktopClass(StringBuilder name) => name.Equals("Progman") || name.Equals("WorkerW");

    /// <summary>Cửa sổ phủ kín màn hình chứa nó (game toàn màn hình hoặc borderless); không có cửa sổ hay màn hình nền thì không tính.</summary>
    public static bool CoversMonitor(IntPtr hwnd)
    {
        if (Kind(hwnd) is ForegroundKind.None or ForegroundKind.Desktop)
            return false;

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetWindowRect(hwnd, out var window) || !NativeMethods.GetMonitorInfo(monitor, ref info))
            return false;

        var m = info.rcMonitor;
        return window.Left <= m.Left && window.Top <= m.Top && window.Right >= m.Right && window.Bottom >= m.Bottom;
    }

    /// <summary>
    /// Đường dẫn đầy đủ của exe; không mở được tiến trình (đã thoát, bị chặn) thì null. Nhớ theo PID trong bảng cỡ nhỏ:
    /// mục lâu không được hỏi tới bị thay trước và hỏi lại hệ điều hành, nên PID đã thoát không giữ đường dẫn cũ.
    /// </summary>
    public static string? ExePath(int pid)
    {
        var now = Environment.TickCount64;
        int slot = -1, oldest = 0;
        for (int i = 0; i < PathCache.Length; i++)
        {
            if (PathCache[i].Pid == pid && PathCache[i].SeenAt != 0)
            {
                slot = i;
                break;
            }
            if (PathCache[i].SeenAt < PathCache[oldest].SeenAt)
                oldest = i;
        }

        if (slot >= 0 && now - PathCache[slot].SeenAt <= StaleAfterMs)
        {
            PathCache[slot].SeenAt = now;
            return PathCache[slot].Path;
        }

        var path = QueryExePath(pid);
        PathCache[slot >= 0 ? slot : oldest] = (pid, path, now);
        return path;
    }

    static string? QueryExePath(int pid)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
            return null;
        try
        {
            uint size = (uint)PathBuffer.Length;
            return NativeMethods.QueryFullProcessImageName(handle, 0, PathBuffer, ref size)
                ? new string(PathBuffer, 0, (int)size)
                : null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
