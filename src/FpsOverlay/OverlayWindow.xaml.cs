using System.Windows;
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
    /// <summary>Khoảng cách tới mép màn hình chính (DIP), ở cả bốn góc.</summary>
    const double ScreenMargin = 8;
    const double ValueFontSize = 24;
    const double LabelFontSize = 18;
    const double CyberChamfer = 9;

    readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    IntPtr _hwnd;

    // Viền tối quanh chữ (Cổ điển) và ánh sáng quanh số FPS (Cyberpunk); chỉ dùng một trong hai.
    readonly DropShadowEffect _classicOutline = new() { Color = Colors.Black, ShadowDepth = 0, BlurRadius = 3, Opacity = 1 };
    readonly DropShadowEffect _cyberGlow = new() { ShadowDepth = 0, BlurRadius = 6, Opacity = 0.7 };

    OverlaySettings _settings = OverlaySettings.Defaults;
    double? _lastFps;
    string _lastText = "--";
    bool _glowOn;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => UpdatePosition();
        DpiChanged += (_, _) => UpdatePosition();
        Chrome.SizeChanged += (_, _) => UpdateChamfer();
        _topmostTimer.Tick += (_, _) => KeepOnTop();
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

    // Đặt cửa sổ vào góc đã chọn của màn hình chính, cách mép 8 DIP (không trừ taskbar).
    void UpdatePosition()
    {
        double w = ActualWidth, h = ActualHeight;
        double screenW = SystemParameters.PrimaryScreenWidth, screenH = SystemParameters.PrimaryScreenHeight;
        var corner = _settings.Corner;
        bool right = corner is OverlayCorner.TopRight or OverlayCorner.BottomRight;
        bool bottom = corner is OverlayCorner.BottomLeft or OverlayCorner.BottomRight;
        double left = right ? Math.Max(0, screenW - ScreenMargin - w) : ScreenMargin;
        double top = bottom ? Math.Max(0, screenH - ScreenMargin - h) : ScreenMargin;
        // Chỉ gán khi đổi, để gọi định kỳ không làm cửa sổ di chuyển thừa.
        if (Left != left) Left = left;
        if (Top != top) Top = top;
    }

    void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED |
                 NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        _topmostTimer.Start();
    }

    // Game borderless đôi khi đè lên cửa sổ topmost; đặt lại định kỳ.
    void KeepOnTop()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible)
            return;
        UpdatePosition();   // theo kịp đổi độ phân giải / màn hình chính
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostTimer.Stop();
        base.OnClosed(e);
    }
}
