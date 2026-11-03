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
    const double ValueFontSize = 24;
    const double LabelFontSize = 18;
    const double CyberChamfer = 9;
    // Mục phụ (1%, GPU, CPU, RAM) nhỏ hơn mục FPS để cả dải không quá dài.
    const double StatValueFontSize = 18;
    const double StatLabelFontSize = 14;

    readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    IntPtr _hwnd;
    (int X, int Y)? _lastPos;

    /// <summary>App đặt: trả handle cửa sổ cài đặt khi đang xem trước (overlay theo màn hình đó), ngược lại null/Zero.</summary>
    public Func<IntPtr?>? PreviewWindow { get; set; }

    // Viền tối quanh chữ (Cổ điển) và ánh sáng quanh số FPS (Cyberpunk); chỉ dùng một trong hai.
    readonly DropShadowEffect _classicOutline = new() { Color = Colors.Black, ShadowDepth = 0, BlurRadius = 3, Opacity = 1 };
    readonly DropShadowEffect _cyberGlow = new() { ShadowDepth = 0, BlurRadius = 6, Opacity = 0.7 };

    OverlaySettings _settings = OverlaySettings.Defaults;
    double? _lastFps;
    string _lastText = "--";
    bool _glowOn;
    double? _lastOnePercentLow;
    SensorSnapshot _lastSensors;
    readonly StatItem _one, _gpu, _cpu, _ram;

    /// <summary>Một mục phụ: dấu ngăn + nhãn + một hoặc hai số (số thứ hai chỉ GPU/CPU có). Ẩn mục là ẩn cả dấu ngăn.</summary>
    sealed class StatItem(TextBlock item, Run sep, Run label, Run gap, Run first, Run? gap2 = null, Run? second = null)
    {
        public void ApplyStyle(OverlayStyle style, Brush labelBrush)
        {
            var cyber = style == OverlayStyle.Cyberpunk;
            foreach (var r in new[] { sep, label, gap, gap2 })
            {
                if (r is null) continue;
                r.FontFamily = OverlayTheme.LabelFont(style);
                r.FontSize = StatLabelFontSize;
                r.FontWeight = FontWeights.Bold;
                r.Foreground = labelBrush;
            }
            // Khoảng cách như FpsGap (≈ 4–5 DIP): Chakra Petch có dấu cách hẹp nên cỡ chữ lớn hơn Consolas.
            gap.FontSize = cyber ? 16 : 9;
            if (gap2 is not null) gap2.FontSize = cyber ? 16 : 9;
            foreach (var r in new[] { first, second })
            {
                if (r is null) continue;
                r.FontFamily = OverlayTheme.ValueFont(style);
                r.FontSize = StatValueFontSize;
                r.FontWeight = FontWeights.Bold;
                r.Foreground = null;   // ép gán lại màu ở lần SetStats kế tiếp
            }
        }

        public void SetVisible(bool visible)
        {
            var v = visible ? Visibility.Visible : Visibility.Collapsed;
            if (item.Visibility != v) item.Visibility = v;
        }

        public void SetFirst(string text, Brush brush) => Set(first, text, brush);

        /// <summary>Số thứ hai; <paramref name="gapText"/> là khoảng cách giữa hai số ("" khi chỉ hiện một số).</summary>
        public void SetSecond(string gapText, string text, Brush brush)
        {
            Set(gap2!, gapText, null);
            Set(second!, text, brush);
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
        _one = new(OneItem, OneSep, OneLabel, OneGap, OneValue);
        _gpu = new(GpuItem, GpuSep, GpuLabel, GpuGap, GpuLoad, GpuTempGap, GpuTemp);
        _cpu = new(CpuItem, CpuSep, CpuLabel, CpuGap, CpuLoad, CpuTempGap, CpuTemp);
        _ram = new(RamItem, RamSep, RamLabel, RamGap, RamValue);
        Apply(_settings);
    }

    /// <summary>Áp phong cách, màu, kích cỡ, độ mờ nền và góc. Gọi lại bao nhiêu lần cũng được.</summary>
    public void Apply(OverlaySettings settings)
    {
        _settings = settings;
        var style = settings.Style;
        var colors = settings.ColorsFor(style);
        var cyber = style == OverlayStyle.Cyberpunk;

        FpsLabel.FontFamily = OverlayTheme.LabelFont(style);
        FpsLabel.FontSize = LabelFontSize;
        FpsLabel.FontWeight = FontWeights.Bold;
        FpsLabel.Foreground = OverlayTheme.FromHex(colors.Label);
        FpsGap.FontFamily = OverlayTheme.LabelFont(style);
        FpsGap.FontSize = cyber ? 20 : 11;   // ≈ 5–6 DIP giữa nhãn và giá trị
        FpsValue.FontFamily = OverlayTheme.ValueFont(style);
        FpsValue.FontSize = ValueFontSize;
        FpsValue.FontWeight = FontWeights.Bold;

        var labelBrush = OverlayTheme.FromHex(colors.Label);   // dấu ngăn cũng dùng màu nhãn
        _one.ApplyStyle(style, labelBrush);
        _gpu.ApplyStyle(style, labelBrush);
        _cpu.ApplyStyle(style, labelBrush);
        _ram.ApplyStyle(style, labelBrush);

        var background = cyber ? OverlayTheme.CyberBackground : OverlayTheme.ClassicBackground;
        background.A = (byte)Math.Round(settings.BackgroundOpacity * 255);
        var backgroundBrush = new SolidColorBrush(background);
        backgroundBrush.Freeze();
        Chrome.Background = backgroundBrush;

        if (cyber)
        {
            Chrome.CornerRadius = new CornerRadius(0);
            Chrome.BorderBrush = OverlayTheme.CyberAccent;
            Chrome.BorderThickness = new Thickness(3, 0, 0, 0);
            Chrome.Padding = new Thickness(10, 3, 10 + CyberChamfer, 3);
            Items.Effect = null;
        }
        else
        {
            Chrome.CornerRadius = new CornerRadius(4);
            Chrome.BorderBrush = null;
            Chrome.BorderThickness = new Thickness(0);
            Chrome.Padding = new Thickness(12, 4, 12, 4);
            Items.Effect = _classicOutline;
        }
        UpdateChamfer();

        Zoom.ScaleX = settings.Scale;
        Zoom.ScaleY = settings.Scale;

        // Ép tính lại màu số FPS theo phong cách/màu mới.
        FpsValue.Foreground = null;
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
            FpsValue.Text = text;
        }

        var style = settings.Style;
        var colors = settings.ColorsFor(style);
        Brush brush;
        if (fps is null)
            brush = OverlayTheme.FromHex(colors.Value);   // "--" luôn dùng màu giá trị thường
        else
        {
            var level = settings.ColorByFps
                ? FpsLevels.Classify(fps, settings.GoodThreshold, settings.BadThreshold)
                : FpsLevel.None;
            brush = level != FpsLevel.None ? OverlayTheme.LevelBrush(style, level) : OverlayTheme.FromHex(colors.Fps);
        }

        // Cyberpunk: số FPS phát sáng nhẹ cùng màu; "--" không phát sáng.
        var glow = style == OverlayStyle.Cyberpunk && fps is not null;
        if (ReferenceEquals(FpsValue.Foreground, brush) && glow == _glowOn)
            return;
        FpsValue.Foreground = brush;
        _glowOn = glow;
        if (glow && brush is SolidColorBrush solid)
        {
            _cyberGlow.Color = solid.Color;
            FpsValue.Effect = _cyberGlow;
        }
        else
            FpsValue.Effect = null;
    }

    /// <summary>
    /// Cập nhật 1% low và các số cảm biến. Gọi mỗi giây và khi cài đặt đổi: chỉ gán chữ, màu, hiện/ẩn khi chúng thật sự đổi.
    /// </summary>
    public void SetStats(double? onePercentLow, SensorSnapshot sensors, OverlaySettings settings)
    {
        _lastOnePercentLow = onePercentLow;
        _lastSensors = sensors;
        var style = settings.Style;
        var colors = settings.ColorsFor(style);
        var normal = OverlayTheme.FromHex(colors.Value);
        var hot = OverlayTheme.LevelBrush(style, FpsLevel.Bad);

        // 1% low tô màu theo cùng mốc với FPS.
        _one.SetVisible(settings.ShowOnePercentLow);
        if (settings.ShowOnePercentLow)
        {
            Brush brush = normal;
            if (onePercentLow is not null)
            {
                var level = settings.ColorByFps
                    ? FpsLevels.Classify(onePercentLow, settings.GoodThreshold, settings.BadThreshold)
                    : FpsLevel.None;
                brush = level != FpsLevel.None ? OverlayTheme.LevelBrush(style, level) : OverlayTheme.FromHex(colors.Fps);
            }
            _one.SetFirst(FpsCalculator.Format(onePercentLow), brush);
        }

        SetPair(_gpu, settings.ShowGpuLoad, settings.ShowGpuTemp, sensors.GpuLoad, sensors.GpuTemp, HeatLevels.GpuHotCelsius, normal, hot);
        SetPair(_cpu, settings.ShowCpuLoad, settings.ShowCpuTemp, sensors.CpuLoad, sensors.CpuTemp, HeatLevels.CpuHotCelsius, normal, hot);

        _ram.SetVisible(settings.ShowRam);
        if (settings.ShowRam)
            _ram.SetFirst(StatFormat.Gigabytes(sensors.RamUsedGb), normal);
    }

    // Mục có hai số (phần trăm tải + nhiệt độ): tắt cả hai thì ẩn cả mục; tắt một thì số kia vẫn hiện, không để khoảng trống thừa.
    static void SetPair(StatItem item, bool showLoad, bool showTemp, double? load, double? temp, double hotLimit, Brush normal, Brush hot)
    {
        item.SetVisible(showLoad || showTemp);
        if (!showLoad && !showTemp) return;
        item.SetFirst(showLoad ? StatFormat.Percent(load) : "", normal);
        item.SetSecond(showLoad && showTemp ? " " : "", showTemp ? StatFormat.Temperature(temp) : "",
            HeatLevels.IsHot(temp, hotLimit) ? hot : normal);
    }

    // Góc phải dưới vát chéo (Cyberpunk). Clip theo kích thước trước LayoutTransform nên vát cũng phóng theo Scale.
    void UpdateChamfer()
    {
        if (_settings.Style != OverlayStyle.Cyberpunk)
        {
            Chrome.Clip = null;
            return;
        }
        double w = Chrome.ActualWidth, h = Chrome.ActualHeight;
        if (w <= CyberChamfer || h <= CyberChamfer)
            return;
        var figure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(new[]
        {
            new Point(w, 0), new Point(w, h - CyberChamfer), new Point(w - CyberChamfer, h), new Point(0, h),
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
        // Chỉ đặt khi đổi, để gọi định kỳ không làm cửa sổ di chuyển thừa (và không vòng lặp với DpiChanged).
        if (_lastPos == (x, y))
            return;
        _lastPos = (x, y);
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
