namespace FpsOverlay.Core.Install;

/// <summary>
/// Lệnh cmd để bản cài tự gỡ thư mục của nó sau khi thoát (chạy: <c>cmd.exe /d /s /c "&lt;lệnh&gt;"</c>; /s bỏ đúng cặp
/// ngoặc kép ngoài cùng, giữ nguyên ngoặc kép quanh đường dẫn bên trong).
/// Mỗi lượt (~1 giây, tối đa <see cref="MaxRounds"/> lượt): tiến trình <c>pid</c> (đúng PID và tên exe) còn trong tasklist
/// thì chờ (ping); đã thoát thì rmdir, xoá xong mới xoá khoá đăng ký (mục gỡ cài đặt, nhánh 64-bit) rồi thoát; rmdir chưa
/// được (file còn khoá) thì thử lượt sau. Hết lượt mà thư mục còn thì khoá vẫn còn: gỡ lại được từ Settings → Apps.
/// Mọi chương trình gọi bằng đường dẫn đầy đủ trong <paramref name="systemDirectory"/>.
/// </summary>
public static class SelfDeleteCommand
{
    public const int MaxRounds = 90;

    /// <param name="registryKey">Khoá đầy đủ cho reg.exe, vd. <c>HKLM\SOFTWARE\...\Uninstall\FpsOverlay</c>.</param>
    public static string Build(string systemDirectory, int pid, string exeName, string directory, string registryKey)
    {
        string Tool(string name) => Path.Combine(systemDirectory, name);
        var wait = $"\"{Tool("PING.EXE")}\" -n 2 127.0.0.1 >nul";
        var running = $"\"{Tool("tasklist.exe")}\" /nh /fi \"PID eq {pid}\" | \"{Tool("find.exe")}\" /i \"{exeName}\" >nul";
        var deleteKey = $"\"{Tool("reg.exe")}\" delete \"{registryKey}\" /f /reg:64 >nul 2>nul";
        return $"for /l %i in (1,1,{MaxRounds}) do (({running} && {wait}) || (rmdir /s /q \"{directory}\" 2>nul & " +
               $"if not exist \"{directory}\" ({deleteKey} & exit) & {wait}))";
    }
}
