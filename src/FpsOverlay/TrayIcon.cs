using System.Windows.Forms;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;
using Icon = System.Drawing.Icon;

namespace FpsOverlay;

/// <summary>Biểu tượng ở khay hệ thống: Ẩn/Hiện FPS, Cài đặt..., Góc hiển thị, Di chuyển overlay,
/// Luôn hiện / Không bao giờ hiện cho app phía trước, Khởi động cùng Windows, Thoát.</summary>
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
    readonly ToolStripMenuItem _moveItem;
    readonly ToolStripMenuItem _alwaysShowItem;
    readonly ToolStripMenuItem _neverShowItem;
    readonly ToolStripMenuItem _autostartItem;
    readonly ToolStripMenuItem _exitItem;
    readonly ToolStripMenuItem[] _cornerItems;
    // Chữ cảnh báo đang hiện (null = không có); Apply giữ lại để đổi cài đặt không làm mất cảnh báo.
    string? _warning;
    AppLanguage _language = AppLanguage.Vietnamese;
    // Bản không cài: mục Khởi động cùng Windows mờ đi, kèm tooltip giải thích.
    bool _autostartLocked;
    // Tên exe của app phía trước gần nhất cho hai mục Luôn hiện / Không bao giờ hiện; null = chưa có.
    string? _appTarget;

    public event Action? ToggleRequested;
    public event Action? SettingsRequested;
    public event Action<OverlayCorner>? CornerRequested;
    /// <summary>Bật/tắt chế độ di chuyển overlay (tắt = lưu vị trí).</summary>
    public event Action? MoveRequested;
    /// <summary>Thêm app phía trước gần nhất vào danh sách "luôn hiện".</summary>
    public event Action? AlwaysShowRequested;
    /// <summary>Thêm app phía trước gần nhất vào danh sách "không bao giờ hiện".</summary>
    public event Action? NeverShowRequested;
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
        _moveItem = new ToolStripMenuItem("", null, (_, _) => MoveRequested?.Invoke());
        _alwaysShowItem = new ToolStripMenuItem("", null, (_, _) => AlwaysShowRequested?.Invoke());
        _neverShowItem = new ToolStripMenuItem("", null, (_, _) => NeverShowRequested?.Invoke());
        _autostartItem = new ToolStripMenuItem("", null, (sender, _) => AutostartRequested?.Invoke(!((ToolStripMenuItem)sender!).Checked));
        _exitItem = new ToolStripMenuItem("", null, (_, _) => ExitRequested?.Invoke());

        var menu = new ContextMenuStrip { ShowItemToolTips = true };
        menu.Opening += (_, _) => MenuOpening?.Invoke();
        menu.Items.Add(_warningItem);
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_cornerItem);
        menu.Items.Add(_moveItem);
        menu.Items.Add(_alwaysShowItem);
        menu.Items.Add(_neverShowItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _image = TrayIconImage.Create();
        _icon = new NotifyIcon
        {
            Icon = _image,
            ContextMenuStrip = menu,
        };
        Apply(OverlaySettings.Defaults, shownByUser: true);
        _icon.Visible = true;
    }

    /// <summary>
    /// Cập nhật chữ theo ngôn ngữ, dấu chọn góc đang dùng và chữ Ẩn/Hiện theo lựa chọn của người dùng
    /// (<paramref name="shownByUser"/> = người dùng không bấm Ẩn FPS; overlay vẫn có thể tự ẩn khi không chơi game).
    /// </summary>
    public void Apply(OverlaySettings settings, bool shownByUser)
    {
        var language = _language = settings.Language;
        _icon.Text = _warning ?? Loc.Get(StringKey.TrayTooltip, language);
        _toggleItem.Text = Loc.Get(shownByUser ? StringKey.TrayHide : StringKey.TrayShow, language);
        _settingsItem.Text = Loc.Get(StringKey.TraySettings, language);
        _cornerItem.Text = Loc.Get(StringKey.TrayCorner, language);
        _moveItem.Text = Loc.Get(StringKey.TrayMove, language);
        _moveItem.Enabled = shownByUser;   // người dùng đã ẩn thì không có gì để di chuyển; tự ẩn thì di chuyển sẽ hiện nó
        ApplyAppTarget();
        _autostartItem.Text = Loc.Get(StringKey.TrayAutostart, language);
        ApplyAutostartLock();
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

    /// <summary>Dấu tick ở mục Di chuyển overlay (đang ở chế độ di chuyển).</summary>
    public void SetMoving(bool moving) => _moveItem.Checked = moving;

    /// <summary>Tên exe cho hai mục Luôn hiện / Không bao giờ hiện; null thì hai mục mờ và hiện "—".</summary>
    public void SetAppTarget(string? exeName)
    {
        _appTarget = exeName;
        ApplyAppTarget();
    }

    void ApplyAppTarget()
    {
        var name = _appTarget ?? "—";
        _alwaysShowItem.Text = string.Format(Loc.Get(StringKey.TrayAlwaysShow, _language), name);
        _neverShowItem.Text = string.Format(Loc.Get(StringKey.TrayNeverShow, _language), name);
        _alwaysShowItem.Enabled = _neverShowItem.Enabled = _appTarget is not null;
    }

    /// <summary>Dấu tick ở mục Khởi động cùng Windows.</summary>
    public void SetAutostart(bool enabled) => _autostartItem.Checked = enabled;

    /// <summary>Khoá mục Khởi động cùng Windows (chỉ bản đã cài mới được tạo/sửa tác vụ).</summary>
    public void SetAutostartLocked(bool locked)
    {
        _autostartLocked = locked;
        ApplyAutostartLock();
    }

    void ApplyAutostartLock()
    {
        _autostartItem.Enabled = !_autostartLocked;
        _autostartItem.ToolTipText = _autostartLocked ? Loc.Get(StringKey.AutostartInstalledOnly, _language) : null;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
    }
}
