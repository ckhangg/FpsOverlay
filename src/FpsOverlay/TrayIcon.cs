using System.Windows.Forms;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;
using Icon = System.Drawing.Icon;

namespace FpsOverlay;

/// <summary>Biểu tượng ở khay hệ thống: Ẩn/Hiện FPS, Cài đặt..., Góc hiển thị, Khởi động cùng Windows, Thoát.</summary>
sealed class TrayIcon : IDisposable
{
    static readonly (OverlayCorner Corner, StringKey Key)[] Corners =
    [
        (OverlayCorner.TopLeft, StringKey.CornerTopLeft),
        (OverlayCorner.TopRight, StringKey.CornerTopRight),
        (OverlayCorner.BottomLeft, StringKey.CornerBottomLeft),
        (OverlayCorner.BottomRight, StringKey.CornerBottomRight),
    ];

    readonly NotifyIcon _icon;
    readonly Icon _image;
    readonly ToolStripMenuItem _warningItem;
    readonly ToolStripMenuItem _toggleItem;
    readonly ToolStripMenuItem _settingsItem;
    readonly ToolStripMenuItem _cornerItem;
    readonly ToolStripMenuItem _autostartItem;
    readonly ToolStripMenuItem _exitItem;
    readonly ToolStripMenuItem[] _cornerItems;
    // Chữ cảnh báo đang hiện (null = không có); Apply giữ lại để đổi cài đặt không làm mất cảnh báo.
    string? _warning;
    AppLanguage _language = AppLanguage.Vietnamese;

    public event Action? ToggleRequested;
    public event Action? SettingsRequested;
    public event Action<OverlayCorner>? CornerRequested;
    /// <summary>Tham số là trạng thái muốn chuyển sang.</summary>
    public event Action<bool>? AutostartRequested;
    public event Action? ExitRequested;
    public event Action? MenuOpening;

    public TrayIcon()
    {
        _warningItem = new ToolStripMenuItem("") { Enabled = false, Visible = false };
        _toggleItem = new ToolStripMenuItem("", null, (_, _) => ToggleRequested?.Invoke());
        _settingsItem = new ToolStripMenuItem("", null, (_, _) => SettingsRequested?.Invoke());
        _cornerItems = Corners
            .Select(c => new ToolStripMenuItem("", null, (_, _) => CornerRequested?.Invoke(c.Corner)))
            .ToArray();
        _cornerItem = new ToolStripMenuItem("", null, _cornerItems);
        _autostartItem = new ToolStripMenuItem("", null, (sender, _) => AutostartRequested?.Invoke(!((ToolStripMenuItem)sender!).Checked));
        _exitItem = new ToolStripMenuItem("", null, (_, _) => ExitRequested?.Invoke());

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => MenuOpening?.Invoke();
        menu.Items.Add(_warningItem);
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_cornerItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _image = TrayIconImage.Create();
        _icon = new NotifyIcon
        {
            Icon = _image,
            ContextMenuStrip = menu,
        };
        Apply(OverlaySettings.Defaults, overlayVisible: true);
        _icon.Visible = true;
    }

    /// <summary>Cập nhật chữ theo ngôn ngữ, dấu chọn góc đang dùng và chữ Ẩn/Hiện theo trạng thái overlay.</summary>
    public void Apply(OverlaySettings settings, bool overlayVisible)
    {
        var language = _language = settings.Language;
        _icon.Text = _warning ?? Loc.Get(StringKey.TrayTooltip, language);
        _toggleItem.Text = Loc.Get(overlayVisible ? StringKey.TrayHide : StringKey.TrayShow, language);
        _settingsItem.Text = Loc.Get(StringKey.TraySettings, language);
        _cornerItem.Text = Loc.Get(StringKey.TrayCorner, language);
        _autostartItem.Text = Loc.Get(StringKey.TrayAutostart, language);
        _exitItem.Text = Loc.Get(StringKey.TrayExit, language);
        for (int i = 0; i < Corners.Length; i++)
        {
            _cornerItems[i].Text = Loc.Get(Corners[i].Key, language);
            _cornerItems[i].Checked = Corners[i].Corner == settings.Corner;
        }
    }

    /// <summary>Hiện dòng cảnh báo đầu menu và đặt làm tooltip; null thì ẩn dòng đó và trả tooltip về như cũ.</summary>
    public void SetWarning(string? text)
    {
        _warning = text;
        _warningItem.Text = text ?? "";
        _warningItem.Visible = text is not null;
        _icon.Text = text ?? Loc.Get(StringKey.TrayTooltip, _language);
    }

    /// <summary>Dấu tick ở mục Khởi động cùng Windows.</summary>
    public void SetAutostart(bool enabled) => _autostartItem.Checked = enabled;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
    }
}
