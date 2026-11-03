using System.Diagnostics;

namespace FpsOverlay;

/// <summary>
/// Các tiến trình có thể đang vẽ cửa sổ ở phía trước: tiến trình sở hữu cửa sổ, rồi tới tiến trình của các cửa sổ con
/// thuộc tiến trình khác. Game Store/UWP cũ chạy bên trong khung của ApplicationFrameHost, nên cửa sổ phía trước
/// thuộc tiến trình khung còn hình do tiến trình game (cửa sổ con) vẽ. Dùng trên luồng UI.
/// </summary>
static class ForegroundProcess
{
    /// <summary>Cửa sổ con có thể xuất hiện sau khi game mở, nên danh sách được dò lại định kỳ.</summary>
    static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(1);

    static readonly List<int> Candidates = [];
    static readonly NativeMethods.EnumWindowsProc CollectChild = OnChildWindow;
    static readonly Stopwatch SinceScan = new();
    static IntPtr _scannedWindow;

    /// <summary>Danh sách dùng lại giữa các lần gọi (không cấp phát mỗi khung hình); rỗng nếu không có cửa sổ phía trước.</summary>
    public static IReadOnlyList<int> GetCandidates()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == _scannedWindow && SinceScan.Elapsed < RescanInterval)
            return Candidates;

        _scannedWindow = hwnd;
        SinceScan.Restart();
        Candidates.Clear();
        if (hwnd == IntPtr.Zero)
            return Candidates;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return Candidates;
        Candidates.Add((int)pid);
        NativeMethods.EnumChildWindows(hwnd, CollectChild, IntPtr.Zero);
        return Candidates;
    }

    static bool OnChildWindow(IntPtr child, IntPtr _)
    {
        NativeMethods.GetWindowThreadProcessId(child, out var pid);
        if (pid != 0 && !Candidates.Contains((int)pid))
            Candidates.Add((int)pid);
        return true;   // dò tiếp
    }
}
