using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using FpsOverlay.Core;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>Cửa sổ trong suốt hiện số FPS: luôn trên cùng, chuột bấm xuyên qua, không chiếm focus.</summary>
public partial class OverlayWindow : Window
{
    /// <summary>Khoảng cách tới mép màn hình (DIP), ở cả bốn góc.</summary>
    const double ScreenMargin = 8;

    readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    IntPtr _hwnd;

    /// <summary>App đặt: trả handle cửa sổ cài đặt khi đang xem trước (overlay theo màn hình đó), ngược lại null/Zero.</summary>
    public Func<IntPtr?>? PreviewWindow { get; set; }

    // Viền tối quanh chữ (Outline) và ánh sáng quanh số FPS (Glow); phong cách chọn dùng cái nào.
    readonly DropShadowEffect _outline = new() { Color = Colors.Black, ShadowDepth = 0, BlurRadius = 3, Opacity = 1 };
    readonly DropShadowEffect _fpsGlow = new() { ShadowDepth = 0, BlurRadius = 6, Opacity = 0.7 };

    // Mục FPS: dấu nhắc (rỗng ở Cổ điển/Cyberpunk) + nhãn + khoảng cách + số. Số nằm trong InlineUIContainer để phát sáng được.
    readonly Run _prompt = new(""), _fpsLabel = new("FPS"), _fpsGap = new(" ");
    readonly TextBlock _fpsValue = new() { Text = "--" };

    OverlaySettings _settings = OverlaySettings.Defaults;
    OverlayVisuals _visuals = OverlayTheme.Visuals(OverlaySettings.Defaults.Style);
    double? _lastFps;
    string _lastText = "--";
    bool _glowOn;
    double? _lastOnePercentLow;
    SensorSnapshot _lastSensors;
    readonly RowView[] _rows;

    /// <summary>
    /// Đối tượng chữ của một <see cref="StatRow"/>, tạo một lần: dấu ngăn + nhãn + ô First + ô Second.
    /// Ở bố cục ngang tất cả là Run trong một TextBlock (chung đường chân chữ); ẩn mục là ẩn cả dấu ngăn.
    /// </summary>
    sealed class RowView
    {
        public readonly StatRow Row;
        public readonly TextBlock Item = new() { VerticalAlignment = VerticalAlignment.Center };
        public readonly Run Sep = new(), Label, Gap = new(" "), First = new("--"), Gap2 = new(""), Second = new("");

        public RowView(StatRow row)
        {
            Row = row;
            Label = new Run(StatRows.Label(row));
            Item.Inlines.AddRange(new Inline[] { Sep, Label, Gap, First, Gap2, Second });
        }

        public void ApplyStyle(OverlayVisuals v, string separator, Brush labelBrush)
        {
            if (Sep.Text != separator) Sep.Text = separator;
            foreach (var r in new[] { Sep, Label, Gap, Gap2 })
            {
                r.FontFamily = v.LabelFont;
                r.FontSize = v.StatLabelSize;
                r.FontWeight = FontWeights.Bold;
                r.Foreground = labelBrush;
            }
            Gap.FontSize = v.StatGapSize;
            Gap2.FontSize = v.StatGapSize;
            foreach (var r in new[] { First, Second })
            {
                r.FontFamily = v.ValueFont;
                r.FontSize = v.StatValueSize;
                r.FontWeight = FontWeights.Bold;
                r.Foreground = null;   // ép gán lại màu ở lần SetStats kế tiếp
            }
        }

        public void SetVisible(bool visible)
        {
            var v = visible ? Visibility.Visible : Visibility.Collapsed;
            if (Item.Visibility != v) Item.Visibility = v;
        }

        /// <summary>Hai ô giá trị; ô thứ hai rỗng thì bỏ luôn khoảng cách trước nó, không để khoảng trống thừa.</summary>
        public void SetValues(string first, Brush firstBrush, string second, Brush secondBrush)
        {
            Set(First, first, firstBrush);
            Set(Gap2, second.Length > 0 ? " " : "", null);
            Set(Second, second, secondBrush);
        }

        static void Set(Run run, string text, Brush? brush)
        {
            if (run.Text != text) run.Text = text;
            if (brush is not null && !ReferenceEquals(run.Foreground, brush)) run.Foreground = brush;
        }
    }

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => UpdatePosition();
        DpiChanged += (_, _) => UpdatePosition();
        Chrome.SizeChanged += (_, _) => UpdateChamfer();
        _topmostTimer.Tick += (_, _) => KeepOnTop();

        var fpsItem = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        fpsItem.Inlines.AddRange(new Inline[]
        {
            _prompt, _fpsLabel, _fpsGap, new InlineUIContainer(_fpsValue) { BaselineAlignment = BaselineAlignment.Baseline },
        });
        Items.Children.Add(fpsItem);
        _rows = StatRows.Order.Select(row => new RowView(row)).ToArray();
        foreach (var row in _rows)
            Items.Children.Add(row.Item);
        Apply(_settings);
    }

    /// <summary>Áp phong cách, màu, kích cỡ, độ mờ nền và góc. Gọi lại bao nhiêu lần cũng được.</summary>
    public void Apply(OverlaySettings settings)
    {
        _settings = settings;
        var style = settings.Style;
        var spec = StyleSpecs.For(style);
        var v = _visuals = OverlayTheme.Visuals(style);
        var labelBrush = OverlayTheme.FromHex(settings.ColorsFor(style).Label);   // dấu nhắc và dấu ngăn cũng dùng màu nhãn

        if (_prompt.Text != spec.Prompt) _prompt.Text = spec.Prompt;
        foreach (var r in new[] { _prompt, _fpsLabel })
        {
            r.FontFamily = v.LabelFont;
            r.FontSize = v.FpsLabelSize;
            r.FontWeight = FontWeights.Bold;
            r.Foreground = labelBrush;
        }
        _fpsGap.FontFamily = v.LabelFont;
        _fpsGap.FontSize = v.FpsGapSize;
        _fpsValue.FontFamily = v.ValueFont;
        _fpsValue.FontSize = v.FpsValueSize;
        _fpsValue.FontWeight = FontWeights.Bold;

        foreach (var row in _rows)
            row.ApplyStyle(v, spec.Separator, labelBrush);

        var background = OverlayTheme.ColorFromHex(spec.Background);
        background.A = (byte)Math.Round(settings.BackgroundOpacity * 255);
        var backgroundBrush = new SolidColorBrush(background);
        backgroundBrush.Freeze();
        Chrome.Background = backgroundBrush;
        Chrome.CornerRadius = v.CornerRadius;
        Chrome.BorderBrush = v.BorderBrush;
        Chrome.BorderThickness = v.BorderThickness;
        Chrome.Padding = v.Padding;
        Items.Effect = v.Outline ? _outline : null;
        UpdateChamfer();

        Zoom.ScaleX = settings.Scale;
        Zoom.ScaleY = settings.Scale;

        // Ép tính lại màu số FPS theo phong cách/màu mới.
        _fpsValue.Foreground = null;
        SetFps(_lastFps, settings);
        SetStats(_lastOnePercentLow, _lastSensors, settings);
        UpdatePosition();
    }

    /// <summary>Cập nhật số FPS. Gọi mỗi khung hình: chỉ đổi chữ và màu khi chúng thật sự đổi.</summary>
    public void SetFps(double? fps, OverlaySettings settings)
    {
        _lastFps = fps;
        var text = FpsCalculator.Format(fps);
        if (text != _lastText)
        {
            _lastText = text;
            _fpsValue.Text = text;
        }

        var brush = FpsBrush(fps, settings);

        // Số FPS phát sáng nhẹ cùng màu (phong cách có Glow); "--" không phát sáng.
        var glow = OverlayTheme.Visuals(settings.Style).Glow != TextGlow.None && fps is not null;
        if (ReferenceEquals(_fpsValue.Foreground, brush) && glow == _glowOn)
            return;
        _fpsValue.Foreground = brush;
        _glowOn = glow;
        if (glow && brush is SolidColorBrush solid)
        {
            _fpsGlow.Color = solid.Color;
            _fpsValue.Effect = _fpsGlow;
        }
        else
            _fpsValue.Effect = null;
    }

    // Màu theo mốc FPS, dùng cho cả số FPS và 1% low. "--" luôn dùng màu giá trị thường.
    static Brush FpsBrush(double? fps, OverlaySettings settings)
    {
        var style = settings.Style;
        var colors = settings.ColorsFor(style);
        if (fps is null)
            return OverlayTheme.FromHex(colors.Value);
        var level = settings.ColorByFps
            ? FpsLevels.Classify(fps, settings.GoodThreshold, settings.BadThreshold)
            : FpsLevel.None;
        return level != FpsLevel.None ? OverlayTheme.LevelBrush(style, level) : OverlayTheme.FromHex(colors.Fps);
    }

    /// <summary>
    /// Cập nhật 1% low và các số cảm biến. Gọi mỗi giây và khi cài đặt đổi: chỉ gán chữ, màu, hiện/ẩn khi chúng thật sự đổi.
    /// </summary>
    public void SetStats(double? onePercentLow, SensorSnapshot sensors, OverlaySettings settings)
    {
        _lastOnePercentLow = onePercentLow;
        _lastSensors = sensors;
        var style = settings.Style;
        var normal = OverlayTheme.FromHex(settings.ColorsFor(style).Value);
        var hot = OverlayTheme.FromHex(StyleSpecs.For(style).Bad);

        foreach (var view in _rows)
        {
            var row = view.Row;
            var visible = StatRows.IsVisible(row, settings);
            view.SetVisible(visible);
            if (!visible) continue;

            var (first, second) = StatRows.Values(row, onePercentLow, sensors, settings, OverlayLayout.Horizontal);
            Brush firstBrush = normal, secondBrush = normal;
            if (row == StatRow.OnePercentLow)
                firstBrush = FpsBrush(onePercentLow, settings);   // 1% low tô màu theo cùng mốc với FPS
            // Ô mang nhiệt độ đổi sang màu "nóng" khi vượt ngưỡng.
            var tempCell = StatRows.TempCell(row, settings);
            if (tempCell != 0 && IsHot(row, sensors, settings))
            {
                if (tempCell == 1) firstBrush = hot;
                else secondBrush = hot;
            }
            view.SetValues(first, firstBrush, second, secondBrush);
        }
    }

    static bool IsHot(StatRow row, SensorSnapshot s, OverlaySettings settings) => row switch
    {
        StatRow.Gpu => HeatLevels.IsHot(s.GpuTemp, settings.GpuHotThreshold),
        StatRow.Cpu => HeatLevels.IsHot(s.CpuTemp, settings.CpuHotThreshold),
        _ => false,
    };

    // Góc phải dưới vát chéo (phong cách có Chamfer). Clip theo kích thước trước LayoutTransform nên vát cũng phóng theo Scale.
    void UpdateChamfer()
    {
        var chamfer = _visuals.Chamfer;
        if (chamfer <= 0)
        {
            Chrome.Clip = null;
            return;
        }
        double w = Chrome.ActualWidth, h = Chrome.ActualHeight;
        if (w <= chamfer || h <= chamfer)
            return;
        var figure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(new[]
        {
            new Point(w, 0), new Point(w, h - chamfer), new Point(w - chamfer, h), new Point(0, h),
        }, isStroked: false));
        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        Chrome.Clip = geometry;
    }

    // Đặt cửa sổ vào góc đã chọn của màn hình đang có cửa sổ phía trước (game), cách mép 8 DIP (không trừ taskbar).
    void UpdatePosition()
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(_hwnd, out var self))
            return;
        var monitor = NativeMethods.MonitorFromWindow(TargetWindow(), NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return;
        uint dpi = 96;
        if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
            dpi = dpiX;
        var m = info.rcMonitor;
        var (x, y) = OverlayPlacement.Compute(new PixelRect(m.Left, m.Top, m.Right, m.Bottom),
            self.Right - self.Left, self.Bottom - self.Top, _settings.Corner, (int)Math.Round(ScreenMargin * dpi / 96.0));
        // So với vị trí thật của cửa sổ (WPF có thể đã dời khi DPI đổi); chỉ đặt khi khác, để không vòng lặp với DpiChanged.
        if (self.Left == x && self.Top == y)
            return;
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
    }

    // Cửa sổ quyết định màn hình: cửa sổ cài đặt khi xem trước; cửa sổ phía trước; màn hình chính nếu đó là app này hoặc desktop/shell.
    IntPtr TargetWindow()
    {
        var preview = PreviewWindow?.Invoke();
        if (preview is { } p && p != IntPtr.Zero)
            return p;
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == NativeMethods.GetShellWindow())
            return IntPtr.Zero;
        NativeMethods.GetWindowThreadProcessId(fg, out var pid);
        if (pid == (uint)Environment.ProcessId)
            return IntPtr.Zero;
        var className = new System.Text.StringBuilder(64);
        NativeMethods.GetClassName(fg, className, className.Capacity);
        return className.ToString() is "Progman" or "WorkerW" ? IntPtr.Zero : fg;
    }

    void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED |
                 NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        _topmostTimer.Start();
        UpdatePosition();
    }

    // Game borderless đôi khi đè lên cửa sổ topmost; đặt lại định kỳ.
    void KeepOnTop()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible)
            return;
        UpdatePosition();   // theo kịp game chuyển màn hình / đổi độ phân giải
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostTimer.Stop();
        base.OnClosed(e);
    }
}
