namespace FpsOverlay.Core.GameOnly;

/// <summary>
/// Cửa sổ đang ở trước: không có (thoáng qua khi chuyển cửa sổ) hoặc chính app này hoặc giao diện shell thoáng qua
/// (taskbar, khay ẩn, Alt-Tab) — giữ quyết định trước; màn hình nền; hay ứng dụng khác.
/// </summary>
public enum ForegroundKind { None, Own, Desktop, Other }

/// <summary>Quy tắc nhận ra game và quyết định hiện/ẩn overlay. Thuần logic, không đụng Win32.</summary>
public static class GameWindowRule
{
    private static readonly string[] GameFolders = [@"\steamapps\common\", @"\XboxGames\"];

    public static bool IsGameFolder(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return false;
        foreach (var folder in GameFolders)
            if (exePath.Contains(folder, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Danh sách "không bao giờ" thắng tất cả; còn lại: phủ kín màn hình, nằm trong thư mục game, hoặc có trong danh sách "luôn hiện".</summary>
    public static bool IsGame(bool coversMonitor, IReadOnlyList<string?> exePaths, AppNames always, AppNames never)
    {
        foreach (var path in exePaths)
            if (never.Contains(path)) return false;
        if (coversMonitor) return true;
        foreach (var path in exePaths)
            if (IsGameFolder(path) || always.Contains(path)) return true;
        return false;
    }

    /// <summary>
    /// Hiện hay ẩn overlay. Ẩn thủ công thắng tất cả; đang di chuyển/xem trước thì hiện; tắt "chỉ khi chơi" thì hiện;
    /// còn lại: ứng dụng khác theo <paramref name="isGame"/>, app này hoặc không có cửa sổ phía trước giữ <paramref name="previous"/>,
    /// màn hình nền ẩn.
    /// </summary>
    public static bool ShouldShow(bool hiddenByUser, bool moving, bool previewing, bool onlyInGame,
        ForegroundKind kind, bool isGame, bool previous)
    {
        if (hiddenByUser) return false;
        if (moving || previewing) return true;
        if (!onlyInGame) return true;
        return kind switch
        {
            ForegroundKind.Own or ForegroundKind.None => previous,
            ForegroundKind.Other => isGame,
            _ => false,
        };
    }
}
