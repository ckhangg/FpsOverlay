using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Localization;

public enum StringKey
{
    TrayTooltip, TrayHide, TrayShow, TraySettings, TrayCorner, TrayAutostart, TrayExit,
    CornerTopLeft, CornerTopRight, CornerBottomLeft, CornerBottomRight,
    SettingsTitle, SectionAppearance, SectionColors, SectionFpsColor,
    Language, Style, StyleClassic, StyleCyberpunk, Corner, Scale, BackgroundOpacity,
    LabelColor, ValueColor, FpsColor, ColorByFps, GoodThreshold, BadThreshold,
    SectionSystem, Autostart,
    ResetDefaults, Done,
    AlreadyRunning, PresentMonStopped, PresentMonStartFailed, AutostartFailed, Details,
}

/// <summary>Chuỗi giao diện hai ngôn ngữ. Khoá thiếu ném <see cref="KeyNotFoundException"/>.</summary>
public static class Loc
{
    private static readonly Dictionary<StringKey, (string Vi, string En)> Strings = new()
    {
        [StringKey.TrayTooltip] = ("FPS Overlay", "FPS Overlay"),
        [StringKey.TrayHide] = ("Ẩn FPS", "Hide FPS"),
        [StringKey.TrayShow] = ("Hiện FPS", "Show FPS"),
        [StringKey.TraySettings] = ("Cài đặt...", "Settings..."),
        [StringKey.TrayCorner] = ("Góc hiển thị", "Position"),
        [StringKey.TrayAutostart] = ("Khởi động cùng Windows", "Start with Windows"),
        [StringKey.TrayExit] = ("Thoát", "Exit"),
        [StringKey.CornerTopLeft] = ("Trên trái", "Top left"),
        [StringKey.CornerTopRight] = ("Trên phải", "Top right"),
        [StringKey.CornerBottomLeft] = ("Dưới trái", "Bottom left"),
        [StringKey.CornerBottomRight] = ("Dưới phải", "Bottom right"),
        [StringKey.SettingsTitle] = ("CÀI ĐẶT", "SETTINGS"),
        [StringKey.SectionAppearance] = ("// GIAO DIỆN", "// APPEARANCE"),
        [StringKey.SectionColors] = ("// MÀU SẮC", "// COLORS"),
        [StringKey.SectionFpsColor] = ("// MÀU THEO FPS", "// FPS COLORS"),
        [StringKey.Language] = ("Ngôn ngữ", "Language"),
        [StringKey.Style] = ("Phong cách", "Style"),
        [StringKey.StyleClassic] = ("Cổ điển", "Classic"),
        [StringKey.StyleCyberpunk] = ("Cyberpunk", "Cyberpunk"),
        [StringKey.Corner] = ("Góc hiển thị", "Position"),
        [StringKey.Scale] = ("Kích cỡ", "Size"),
        [StringKey.BackgroundOpacity] = ("Độ mờ nền", "Background"),
        [StringKey.LabelColor] = ("Màu nhãn", "Label color"),
        [StringKey.ValueColor] = ("Màu giá trị", "Value color"),
        [StringKey.FpsColor] = ("Màu FPS", "FPS color"),
        [StringKey.ColorByFps] = ("Đổi màu theo FPS", "Color by FPS"),
        [StringKey.GoodThreshold] = ("Tốt từ", "Good from"),
        [StringKey.BadThreshold] = ("Kém dưới", "Bad below"),
        [StringKey.SectionSystem] = ("// HỆ THỐNG", "// SYSTEM"),
        [StringKey.Autostart] = ("Khởi động cùng Windows", "Start with Windows"),
        [StringKey.ResetDefaults] = ("KHÔI PHỤC MẶC ĐỊNH", "RESTORE DEFAULTS"),
        [StringKey.Done] = ("XONG", "DONE"),
        [StringKey.AlreadyRunning] = ("FPS Overlay đang chạy rồi (xem biểu tượng ở khay hệ thống).", "FPS Overlay is already running (see the tray icon)."),
        [StringKey.PresentMonStopped] = ("PresentMon đã dừng bất ngờ nên không đo được FPS.\nHãy chắc rằng app đang chạy với quyền Administrator.", "PresentMon stopped unexpectedly, so FPS can't be measured.\nMake sure the app is running as Administrator."),
        [StringKey.PresentMonStartFailed] = ("Không khởi động được PresentMon.\nFile PresentMon.exe phải nằm cùng thư mục với FpsOverlay.exe.", "Couldn't start PresentMon.\nPresentMon.exe must be in the same folder as FpsOverlay.exe."),
        [StringKey.AutostartFailed] = ("Không đổi được chế độ khởi động cùng Windows.", "Couldn't change Start with Windows."),
        [StringKey.Details] = ("Chi tiết", "Details"),
    };

    public static string Get(StringKey key, AppLanguage language)
    {
        var (vi, en) = Strings[key];
        return language == AppLanguage.English ? en : vi;
    }
}
