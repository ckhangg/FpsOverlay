using System.Drawing;
using System.Windows.Forms;

namespace FpsOverlay;

/// <summary>Biểu tượng ở khay hệ thống với menu Hiện/Ẩn FPS và Thoát.</summary>
sealed class TrayIcon : IDisposable
{
    const string HideText = "Ẩn FPS";
    const string ShowText = "Hiện FPS";

    readonly NotifyIcon _icon;
    readonly ToolStripMenuItem _toggleItem;

    public event Action? ToggleRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        _toggleItem = new ToolStripMenuItem(HideText, null, (_, _) => ToggleRequested?.Invoke());
        var exitItem = new ToolStripMenuItem("Thoát", null, (_, _) => ExitRequested?.Invoke());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "FPS Overlay",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    /// <summary>Cập nhật chữ của mục bật/tắt theo trạng thái cửa sổ FPS.</summary>
    public void SetOverlayVisible(bool visible) => _toggleItem.Text = visible ? HideText : ShowText;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
