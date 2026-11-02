namespace FpsOverlay;

static class ForegroundProcess
{
    /// <summary>ID tiến trình sở hữu cửa sổ đang ở phía trước, hoặc null nếu không có.</summary>
    public static int? GetId()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == 0 ? null : (int)pid;
    }
}
