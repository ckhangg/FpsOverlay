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

    /// <summary>Lề trong suốt quanh khối khi có viền phát sáng (DIP), cũng là độ toả của ánh sáng viền.</summary>
    const double FrameGlowPad = 8;

    // Viền tối quanh chữ (Outline) và ánh sáng của viền Neon; phong cách chọn dùng cái nào.
    readonly DropShadowEffect _outline = new() { Color = Colors.Black, ShadowDepth = 0, BlurRadius = 3, Opacity = 1 };
    readonly DropShadowEffect _frameGlow = new() { ShadowDepth = 0, BlurRadius = FrameGlowPad, Opacity = 1 };

    // Chữ phát sáng cùng màu chữ: một hiệu ứng đã Freeze cho mỗi brush (brush đều được cache),
    // nên đổi màu theo mức FPS hay nhịp nháy chỉ tra từ điển, không tạo đối tượng mới. Xoá khi áp phong cách.
    readonly Dictionary<Brush, Effect> _glows = new();

    // Mục FPS: dấu nhắc (rỗng ở Cổ điển/Cyberpunk) + nhãn + khoảng cách + số. Số nằm trong InlineUIContainer để phát sáng được.
    // Phong cách có nhãn phát sáng thì dấu nhắc + nhãn chuyển vào _fpsLabelBox (cũng trong InlineUIContainer).
    readonly Run _prompt = new(""), _fpsLabel = new("FPS"), _fpsGap = new(" ");
    readonly TextBlock _fpsValue = new() { Text = "--" };
    readonly TextBlock _fpsItem = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _fpsLabelBox = new();
    readonly InlineUIContainer _fpsLabelHost, _fpsValueHost;
    bool? _fpsLabelBoxed;

    OverlaySettings _settings = OverlaySettings.Defaults;
    OverlayVisuals _visuals = OverlayTheme.Visuals(OverlaySettings.Defaults.Style);
    double? _lastFps;
    string _lastText = "--";
    double? _lastOnePercentLow;
    SensorSnapshot _lastSensors;
    readonly RowView[] _rows;

    // Nháy số nhiệt (~2 Hz): bộ hẹn 250 ms chỉ chạy khi có ô đang nháy; chỉ đổi brush, không đổi kích thước.
    readonly DispatcherTimer _blinkTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly Dictionary<Color, Brush> _dimBrushes = new();
    bool _gpuBlinking, _cpuBlinking;
    bool _blinkDim;
    long _testUntilTick;   // Environment.TickCount64 lúc hết lần thử cảnh báo; 0 = không thử

    /// <summary>
    /// Đối tượng chữ của một <see cref="StatRow"/>, tạo một lần: dấu ngăn + nhãn + ô First + ô Second.
    /// Ở bố cục ngang tất cả là Run trong một TextBlock (chung đường chân chữ); ẩn mục là ẩn cả dấu ngăn.
    /// Phần nào phát sáng (nhãn kèm dấu ngăn, từng ô giá trị) thì Run của nó chuyển vào TextBlock riêng trong
    /// InlineUIContainer (vẫn theo đường chân chữ) để mang hiệu ứng đúng màu của chính nó.
    /// </summary>
    sealed class RowView
    {
        public readonly StatRow Row;
        public readonly TextBlock Item = new() { VerticalAlignment = VerticalAlignment.Center };
        public readonly Run Sep = new(), Label, Gap = new(" "), First = new("--"), Gap2 = new(""), Second = new("");
        readonly TextBlock _labelBox = new(), _firstBox = new(), _secondBox = new();
        readonly InlineUIContainer _labelHost, _firstHost, _secondHost;
        readonly Func<Brush, Effect?> _glow;
        bool? _glowLabel, _glowValues;

        public RowView(StatRow row, Func<Brush, Effect?> glow)
        {
            Row = row;
            _glow = glow;
            Label = new Run(StatRows.Label(row));
            _labelHost = Host(_labelBox);
            _firstHost = Host(_firstBox);
            _secondHost = Host(_secondBox);
            Rebuild(glowLabel: false, glowValues: false);
        }

        public void ApplyStyle(OverlayVisuals v, string separator, Brush labelBrush)
        {
            if (Sep.Text != separator) Sep.Text = separator;
            foreach (var r in new[] { Sep, Label, Gap, Gap2 })
            {
                r.FontFamily = v.LabelFont;
                r.FontSize = v.StatLabelSize;
                r.FontWeight = v.TextWeight;
                r.Foreground = labelBrush;
            }
            Gap.FontSize = v.StatGapSize;
            Gap2.FontSize = v.StatGapSize;
            foreach (var r in new[] { First, Second })
            {
                r.FontFamily = v.ValueFont;
                r.FontSize = v.StatValueSize;
                r.FontWeight = v.TextWeight;
                r.Foreground = null;   // ép gán lại màu (và ánh sáng) ở lần SetStats kế tiếp
            }

            Rebuild(glowLabel: v.Glow is TextGlow.LabelsAndFps or TextGlow.All, glowValues: v.Glow == TextGlow.All);
            SetBoxFont(_labelBox, v.LabelFont, v.StatLabelSize, v.TextWeight);
            SetBoxFont(_firstBox, v.ValueFont, v.StatValueSize, v.TextWeight);
            SetBoxFont(_secondBox, v.ValueFont, v.StatValueSize, v.TextWeight);
            _labelBox.Effect = _glowLabel == true ? _glow(labelBrush) : null;
            _firstBox.Effect = null;
            _secondBox.Effect = null;
        }

        // Xếp lại các Run: thẳng trong Item, hoặc trong TextBlock riêng khi phần đó phát sáng. Chỉ chạy khi đổi phong cách.
        void Rebuild(bool glowLabel, bool glowValues)
        {
            if (_glowLabel == glowLabel && _glowValues == glowValues)
                return;
            _glowLabel = glowLabel;
            _glowValues = glowValues;
            Item.Inlines.Clear();
            _labelBox.Inlines.Clear();
            _firstBox.Inlines.Clear();
            _secondBox.Inlines.Clear();
            Place(Item.Inlines, _labelBox, _labelHost, glowLabel, Sep, Label);
            Item.Inlines.Add(Gap);
            Place(Item.Inlines, _firstBox, _firstHost, glowValues, First);
            Item.Inlines.Add(Gap2);
            Place(Item.Inlines, _secondBox, _secondHost, glowValues, Second);
        }

        public void SetVisible(bool visible)
        {
            var v = visible ? Visibility.Visible : Visibility.Collapsed;
            if (Item.Visibility != v) Item.Visibility = v;
        }

        /// <summary>Hai ô giá trị; ô thứ hai rỗng thì bỏ luôn khoảng cách trước nó, không để khoảng trống thừa.</summary>
        public void SetValues(string first, Brush firstBrush, string second, Brush secondBrush)
        {
            Set(First, _glowValues == true ? _firstBox : null, first, firstBrush);
            Set(Gap2, null, second.Length > 0 ? " " : "", null);
            Set(Second, _glowValues == true ? _secondBox : null, second, secondBrush);
        }

        // Ánh sáng (nếu ô phát sáng) đổi cùng lúc với màu chữ, lấy từ cache nên không cấp phát.
        void Set(Run run, TextBlock? glowBox, string text, Brush? brush)
        {
            if (run.Text != text) run.Text = text;
            if (brush is null || ReferenceEquals(run.Foreground, brush)) return;
            run.Foreground = brush;
            if (glowBox is not null) glowBox.Effect = _glow(brush);
        }
    }

    static InlineUIContainer Host(TextBlock box) => new(box) { BaselineAlignment = BaselineAlignment.Baseline };

    // Đặt các Run vào target: thẳng, hoặc gói trong box (đã nằm trong host) khi cần mang hiệu ứng riêng.
    static void Place(InlineCollection target, TextBlock box, InlineUIContainer host, bool boxed, params Run[] runs)
    {
        if (boxed)
        {
            box.Inlines.AddRange(runs);
            target.Add(host);
        }
        else
            target.AddRange(runs);
    }

    // Box có cùng font với Run bên trong để chiều cao dòng (kể cả khi rỗng) giống hệt.
    static void SetBoxFont(TextBlock box, FontFamily family, double size, FontWeight weight)
    {
        box.FontFamily = family;
        box.FontSize = size;
        box.FontWeight = weight;
    }

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => UpdatePosition();
        DpiChanged += (_, _) => UpdatePosition();
        Chrome.SizeChanged += (_, _) => UpdateChamfer();
        _topmostTimer.Tick += (_, _) => KeepOnTop();
        _blinkTimer.Tick += (_, _) => OnBlinkTick();

        _fpsLabelHost = Host(_fpsLabelBox);
        _fpsValueHost = Host(_fpsValue);
        RebuildFpsItem(boxLabel: false);
        Items.Children.Add(_fpsItem);
        Func<Brush, Effect?> glow = GlowFor;
        _rows = StatRows.Order.Select(row => new RowView(row, glow)).ToArray();
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
        _glows.Clear();   // độ toả/độ đậm ánh sáng theo phong cách mới
        var labelBrush = OverlayTheme.FromHex(settings.ColorsFor(style).Label);   // dấu nhắc và dấu ngăn cũng dùng màu nhãn

        if (_prompt.Text != spec.Prompt) _prompt.Text = spec.Prompt;
        foreach (var r in new[] { _prompt, _fpsLabel })
        {
            r.FontFamily = v.LabelFont;
            r.FontSize = v.FpsLabelSize;
            r.FontWeight = v.TextWeight;
            r.Foreground = labelBrush;
        }
        _fpsGap.FontFamily = v.LabelFont;
        _fpsGap.FontSize = v.FpsGapSize;
        _fpsValue.FontFamily = v.ValueFont;
        _fpsValue.FontSize = v.FpsValueSize;
        _fpsValue.FontWeight = v.TextWeight;
        var glowLabels = v.Glow is TextGlow.LabelsAndFps or TextGlow.All;
        RebuildFpsItem(glowLabels);
        SetBoxFont(_fpsLabelBox, v.LabelFont, v.FpsLabelSize, v.TextWeight);
        _fpsLabelBox.Effect = glowLabels ? GlowFor(labelBrush) : null;

        foreach (var row in _rows)
            row.ApplyStyle(v, spec.Separator, labelBrush);

        var background = OverlayTheme.ColorFromHex(spec.Background);
        background.A = (byte)Math.Round(settings.BackgroundOpacity * 255);
        Chrome.Background = v.Scanlines ? ScanlineBrush(background, settings.BackgroundOpacity) : Frozen(new SolidColorBrush(background));
        Chrome.CornerRadius = v.CornerRadius;
        Chrome.BorderBrush = v.BorderBrush;
        Chrome.BorderThickness = v.BorderThickness;
        Chrome.Padding = v.Padding;
        Items.Effect = v.Outline ? _outline : null;
        UpdateChamfer();
        UpdateFrameGlow(v, settings.Scale);

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

        // Số FPS phát sáng cùng màu (mọi phong cách có Glow); "--" không phát sáng.
        var glow = OverlayTheme.Visuals(settings.Style).Glow != TextGlow.None && fps is not null ? GlowFor(brush) : null;
        if (ReferenceEquals(_fpsValue.Foreground, brush) && ReferenceEquals(_fpsValue.Effect, glow))
            return;
        _fpsValue.Foreground = brush;
        _fpsValue.Effect = glow;
    }

    // Hiệu ứng phát sáng cùng màu với brush, theo độ toả/độ đậm của phong cách hiện tại; tạo một lần cho mỗi brush.
    Effect? GlowFor(Brush brush)
    {
        if (brush is not SolidColorBrush solid)
            return null;
        if (!_glows.TryGetValue(brush, out var effect))
        {
            var shadow = new DropShadowEffect
            {
                Color = solid.Color, ShadowDepth = 0, BlurRadius = _visuals.GlowBlur, Opacity = _visuals.GlowOpacity,
            };
            shadow.Freeze();
            _glows[brush] = effect = shadow;
        }
        return effect;
    }

    // Mục FPS: dấu nhắc + nhãn thẳng trong _fpsItem, hoặc gói trong _fpsLabelBox khi nhãn phát sáng.
    void RebuildFpsItem(bool boxLabel)
    {
        if (_fpsLabelBoxed == boxLabel)
            return;
        _fpsLabelBoxed = boxLabel;
        _fpsItem.Inlines.Clear();
        _fpsLabelBox.Inlines.Clear();
        Place(_fpsItem.Inlines, _fpsLabelBox, _fpsLabelHost, boxLabel, _prompt, _fpsLabel);
        _fpsItem.Inlines.Add(_fpsGap);
        _fpsItem.Inlines.Add(_fpsValueHost);
    }

    // Viền phát sáng (Neon): GlowFrame trùng khung Chrome, nằm ngoài LayoutTransform nên tự nhân Scale;
    // Root có lề FrameGlowPad để ánh sáng không bị mép cửa sổ cắt (UpdatePosition trừ lại lề này).
    void UpdateFrameGlow(OverlayVisuals v, double scale)
    {
        if (v.BorderGlow && v.BorderBrush is SolidColorBrush frame)
        {
            var t = v.BorderThickness;
            var r = v.CornerRadius;
            GlowFrame.BorderBrush = frame;
            GlowFrame.BorderThickness = new Thickness(t.Left * scale, t.Top * scale, t.Right * scale, t.Bottom * scale);
            GlowFrame.CornerRadius = new CornerRadius(r.TopLeft * scale, r.TopRight * scale, r.BottomRight * scale, r.BottomLeft * scale);
            _frameGlow.Color = frame.Color;
            GlowFrame.Effect = _frameGlow;
            GlowFrame.Visibility = Visibility.Visible;
            Root.Margin = new Thickness(FrameGlowPad);
        }
        else
        {
            GlowFrame.Effect = null;
            GlowFrame.Visibility = Visibility.Collapsed;
            Root.Margin = new Thickness(0);
        }
    }

    // Nền kèm vạch CRT: DrawingBrush lặp chu kỳ 3 DIP, 1 DIP trên cùng tối hơn (đen 25%, nhân độ mờ nền để nền 0% vẫn trong suốt).
    static Brush ScanlineBrush(Color background, double opacity)
    {
        var line = Color.FromArgb((byte)Math.Round(0.25 * opacity * 255), 0, 0, 0);
        var tile = new DrawingGroup();
        tile.Children.Add(new GeometryDrawing(new SolidColorBrush(background), null, new RectangleGeometry(new Rect(0, 0, 1, 3))));
        tile.Children.Add(new GeometryDrawing(new SolidColorBrush(line), null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        return Frozen(new DrawingBrush(tile)
        {
            TileMode = TileMode.Tile, Stretch = Stretch.Fill,
            Viewport = new Rect(0, 0, 1, 3), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 1, 3), ViewboxUnits = BrushMappingMode.Absolute,
        });
    }

    static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
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
        var testing = IsTesting;
        if (testing)   // đang thử cảnh báo: hai số nhiệt luôn hiện dù công tắc tắt
            settings = settings with { ShowGpuTemp = true, ShowCpuTemp = true };
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
            if (tempCell != 0)
            {
                Brush? tempBrush = null;
                if (IsBlinking(row, testing))
                    tempBrush = _blinkDim ? DimBrush(hot) : hot;
                else if (IsHot(row, sensors, settings))
                    tempBrush = hot;
                if (tempBrush is not null)
                {
                    if (tempCell == 1) firstBrush = tempBrush;
                    else secondBrush = tempBrush;
                }
            }
            view.SetValues(first, firstBrush, second, secondBrush);
        }
    }

    bool IsTesting => _testUntilTick != 0 && Environment.TickCount64 < _testUntilTick;

    bool IsBlinking(StatRow row, bool testing) => testing || row switch
    {
        StatRow.Gpu => _gpuBlinking,
        StatRow.Cpu => _cpuBlinking,
        _ => false,
    };

    // Bản 25% alpha của màu, đóng băng và nhớ theo màu để mỗi nhịp nháy không tạo brush mới.
    Brush DimBrush(Brush hot)
    {
        var color = ((SolidColorBrush)hot).Color;
        if (!_dimBrushes.TryGetValue(color, out var dim))
        {
            color.A = (byte)Math.Round(color.A * 0.25);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            _dimBrushes[((SolidColorBrush)hot).Color] = dim = brush;
        }
        return dim;
    }

    /// <summary>
    /// App gọi mỗi giây: ô nhiệt GPU/CPU có đang báo động (nháy) không. Hết nháy thì trả brush thường/nóng như <see cref="SetStats"/> chọn.
    /// Lần thử cảnh báo đang chạy thì thắng: cờ được OR với trạng thái thử.
    /// </summary>
    public void SetAlerts(bool gpuBlinking, bool cpuBlinking)
    {
        if (gpuBlinking == _gpuBlinking && cpuBlinking == _cpuBlinking)
            return;
        _gpuBlinking = gpuBlinking;
        _cpuBlinking = cpuBlinking;
        _blinkDim = false;
        SetStats(_lastOnePercentLow, _lastSensors, _settings);
        UpdateBlinkTimer();
    }

    /// <summary>Thử cảnh báo: trong <paramref name="duration"/> hai số nhiệt hiện ra (dù đang tắt) và nháy bằng màu nóng.</summary>
    public void RunAlertTest(TimeSpan duration)
    {
        _testUntilTick = Environment.TickCount64 + (long)duration.TotalMilliseconds;
        _blinkDim = false;
        SetStats(_lastOnePercentLow, _lastSensors, _settings);
        UpdateBlinkTimer();
    }

    void UpdateBlinkTimer()
    {
        var needed = _gpuBlinking || _cpuBlinking || IsTesting;
        if (needed && !_blinkTimer.IsEnabled) _blinkTimer.Start();
        else if (!needed && _blinkTimer.IsEnabled) _blinkTimer.Stop();
    }

    void OnBlinkTick()
    {
        if (_testUntilTick != 0 && !IsTesting)
        {
            // Hết lần thử: ô nhiệt về trạng thái thật (có thể lại ẩn).
            _testUntilTick = 0;
            _blinkDim = false;
            SetStats(_lastOnePercentLow, _lastSensors, _settings);
            UpdateBlinkTimer();
            return;
        }
        if (!_blinkTimer.IsEnabled) return;
        _blinkDim = !_blinkDim;
        SetStats(_lastOnePercentLow, _lastSensors, _settings);
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
        // Lề trong suốt của Root (ánh sáng viền) được trừ ra để khung vẫn cách mép ScreenMargin.
        var margin = ScreenMargin - Root.Margin.Left;
        var (x, y) = OverlayPlacement.Compute(new PixelRect(m.Left, m.Top, m.Right, m.Bottom),
            self.Right - self.Left, self.Bottom - self.Top, _settings.Corner, (int)Math.Round(margin * dpi / 96.0));
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
        _blinkTimer.Stop();
        base.OnClosed(e);
    }
}
