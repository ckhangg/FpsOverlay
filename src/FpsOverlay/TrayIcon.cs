using System.Windows.Forms;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;
using Icon = System.Drawing.Icon;

namespace FpsOverlay;

/// <summary>Biểu tượng ở khay hệ thống: Ẩn/Hiện FPS, Cài đặt..., Góc hiển thị, Thoát.</summary>
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
    readonly ToolStripMenuItem _toggleItem;
    readonly ToolStripMenuItem _settingsItem;
    readonly ToolStripMenuItem _cornerItem;
    readonly ToolStripMenuItem _exitItem;
    readonly ToolStripMenuItem[] _cornerItems;

    public event Action? ToggleRequested;
    public event Action? SettingsRequested;
    public event Action<OverlayCorner>? CornerRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        _toggleItem = new ToolStripMenuItem("", null, (_, _) => ToggleRequested?.Invoke());
        _settingsItem = new ToolStripMenuItem("", null, (_, _) => SettingsRequested?.Invoke());
        _cornerItems = Corners
            .Select(c => new ToolStripMenuItem("", null, (_, _) => CornerRequested?.Invoke(c.Corner)))
            .ToArray();
        _cornerItem = new ToolStripMenuItem("", null, _cornerItems);
        _exitItem = new ToolStripMenuItem("", null, (_, _) => ExitRequested?.Invoke());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_cornerItem);
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
        var language = settings.Language;
        _icon.Text = Loc.Get(StringKey.TrayTooltip, language);
        _toggleItem.Text = Loc.Get(overlayVisible ? StringKey.TrayHide : StringKey.TrayShow, language);
        _settingsItem.Text = Loc.Get(StringKey.TraySettings, language);
        _cornerItem.Text = Loc.Get(StringKey.TrayCorner, language);
        _exitItem.Text = Loc.Get(StringKey.TrayExit, language);
        for (int i = 0; i < Corners.Length; i++)
        {
            _cornerItems[i].Text = Loc.Get(Corners[i].Key, language);
            _cornerItems[i].Checked = Corners[i].Corner == settings.Corner;
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
    }
}
