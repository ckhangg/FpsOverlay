using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.PawnIo;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>
/// Cửa sổ cài đặt kiểu Cyberpunk. Mọi control gọi <see cref="SettingsService.Update"/>; cửa sổ nghe
/// <see cref="SettingsService.Changed"/> để cập nhật lại control và chữ theo ngôn ngữ.
/// </summary>
public partial class SettingsWindow : Window
{
    // Ba dòng màu: 0 = nhãn, 1 = giá trị, 2 = FPS.
    const int Rows = 3;
    const string InvalidTag = "invalid";

    readonly SettingsService _service;
    readonly AutostartService _autostart;
    readonly Action<bool> _setAutostart;
    readonly Func<bool> _cpuTempAvailable;
    readonly Func<Task<PawnIoResult>> _installPawnIo;
    readonly RadioButton[][] _swatches = new RadioButton[Rows][];
    // Đọc registry lúc mở cửa sổ và sau mỗi lần cài, không đọc mỗi giây.
    bool _pawnIoInstalled;
    bool _pawnIoBusy;
    PawnIoResult? _pawnIoResult;   // kết quả lần cài gần nhất trong cửa sổ này (hiện dưới nút)
    readonly TextBox[] _hexBoxes;
    OverlayStyle? _swatchStyle;

    // Đang đẩy giá trị từ cài đặt vào control: sự kiện của control không được gọi Update ngược lại.
    bool _refreshing = true;

    /// <param name="setAutostart">Bật/tắt khởi động cùng Windows (App lo báo lỗi); kết quả về qua <see cref="AutostartService.Changed"/>.</param>
    /// <param name="cpuTempAvailable">Đọc được nhiệt độ CPU chưa; chưa thì hiện ghi chú cần PawnIO dưới công tắc.</param>
    /// <param name="installPawnIo">Tải + kiểm chữ ký + cài PawnIO (App lo đọc lại cảm biến; đang cài thì trả về lần đang chạy).</param>
    /// <param name="pawnIoInstalling">App đang cài PawnIO (vd. bắt đầu từ cửa sổ trước đã đóng).</param>
    internal SettingsWindow(SettingsService service, AutostartService autostart, Action<bool> setAutostart, Func<bool> cpuTempAvailable,
        Func<Task<PawnIoResult>> installPawnIo, bool pawnIoInstalling)
    {
        _service = service;
        _autostart = autostart;
        _setAutostart = setAutostart;
        _cpuTempAvailable = cpuTempAvailable;
        _installPawnIo = installPawnIo;
        _pawnIoInstalled = PawnIoSetup.IsInstalled();
        InitializeComponent();   // _refreshing = true: giá trị mặc định trong XAML không ghi vào cài đặt
        // Bản không cài không được tạo/sửa tác vụ: công tắc mờ đi, tooltip (đặt trong ApplyLanguage) giải thích lý do.
        AutostartSwitch.IsEnabled = autostart.CanManage;
        if (!autostart.CanManage) AutostartSwitch.Opacity = AutostartLabel.Opacity = 0.45;
        ToolTipService.SetShowOnDisabled(AutostartSwitch, true);
        _hexBoxes = [LabelHex, ValueHex, FpsHex];
        Icon = CreateWindowIcon();
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));
        BuildSwatches();

        Refresh(service.Current);
        service.Changed += Refresh;
        RefreshAutostart(autostart.IsEnabled);
        autostart.Changed += RefreshAutostart;
        if (pawnIoInstalling) _ = RunPawnIoInstallAsync();   // nhận lại lần cài đang chạy để hiện tiến trình/kết quả
    }

    protected override void OnClosed(EventArgs e)
    {
        _service.Changed -= Refresh;
        _autostart.Changed -= RefreshAutostart;
        base.OnClosed(e);
    }

    // ---------- Cài đặt → control ----------

    void Refresh(OverlaySettings s)
    {
        _refreshing = true;
        try
        {
            ApplyLanguage(s.Language);

            LanguageVi.IsChecked = s.Language == AppLanguage.Vietnamese;
            LanguageEn.IsChecked = s.Language == AppLanguage.English;
            StyleClassic.IsChecked = s.Style == OverlayStyle.Classic;
            StyleCyberpunk.IsChecked = s.Style == OverlayStyle.Cyberpunk;
            CornerTopLeft.IsChecked = s.Corner == OverlayCorner.TopLeft;
            CornerTopRight.IsChecked = s.Corner == OverlayCorner.TopRight;
            CornerBottomLeft.IsChecked = s.Corner == OverlayCorner.BottomLeft;
            CornerBottomRight.IsChecked = s.Corner == OverlayCorner.BottomRight;

            var scale = (int)Math.Round(s.Scale * 100);
            ScaleSlider.Value = scale;
            ScaleValue.Text = $"{scale}%";
            var opacity = (int)Math.Round(s.BackgroundOpacity * 100);
            OpacitySlider.Value = opacity;
            OpacityValue.Text = $"{opacity}%";

            RefreshColors(s);

            OnePercentSwitch.IsChecked = s.ShowOnePercentLow;
            GpuLoadSwitch.IsChecked = s.ShowGpuLoad;
            GpuTempSwitch.IsChecked = s.ShowGpuTemp;
            CpuLoadSwitch.IsChecked = s.ShowCpuLoad;
            CpuTempSwitch.IsChecked = s.ShowCpuTemp;
            RamSwitch.IsChecked = s.ShowRam;
            RefreshSensorNote();

            LogSwitch.IsChecked = s.LoggingEnabled;
            ColorByFpsSwitch.IsChecked = s.ColorByFps;
            // Mốc bị đẩy theo mốc kia (WithGood/BadThreshold) hiện ngay trên thanh của nó.
            GoodSlider.Value = s.GoodThreshold;
            GoodValue.Text = s.GoodThreshold.ToString();
            BadSlider.Value = s.BadThreshold;
            BadValue.Text = s.BadThreshold.ToString();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Hiện ghi chú PawnIO khi chưa đọc được nhiệt độ CPU. App gọi lại mỗi giây vì PawnIO có thể được cài lúc cửa sổ đang mở.</summary>
    internal void RefreshSensorNote()
    {
        var visibility = _cpuTempAvailable() ? Visibility.Collapsed : Visibility.Visible;
        if (PawnIoPanel.Visibility != visibility) PawnIoPanel.Visibility = visibility;
        // Nút cài: chỉ khi PawnIO chưa cài (đang cài thì vẫn hiện nhưng mờ).
        var button = _pawnIoInstalled && !_pawnIoBusy ? Visibility.Collapsed : Visibility.Visible;
        if (PawnIoInstallButton.Visibility != button) PawnIoInstallButton.Visibility = button;
        if (PawnIoInstallButton.IsEnabled == _pawnIoBusy) PawnIoInstallButton.IsEnabled = !_pawnIoBusy;
        var status = _pawnIoBusy || _pawnIoResult is not null ? Visibility.Visible : Visibility.Collapsed;
        if (PawnIoStatus.Visibility != status) PawnIoStatus.Visibility = status;
    }

    void RefreshPawnIoStatus(AppLanguage language)
    {
        PawnIoStatus.Text = _pawnIoBusy ? Loc.Get(StringKey.PawnIoWorking, language)
            : _pawnIoResult is { } result ? Loc.Get(PawnIoResults.MessageKey(result), language)
            : "";
    }

    void OnInstallPawnIoClick(object sender, RoutedEventArgs e)
    {
        if (_pawnIoBusy) return;
        _ = RunPawnIoInstallAsync();
    }

    // Chạy (hoặc nhận lại) lần cài của App ngoài luồng UI; cửa sổ chỉ hiện tiến trình và kết quả.
    async Task RunPawnIoInstallAsync()
    {
        _pawnIoBusy = true;
        _pawnIoResult = null;
        RefreshPawnIoStatus(_service.Current.Language);
        RefreshSensorNote();
        PawnIoResult result;
        try
        {
            result = await _installPawnIo();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Cài PawnIO lỗi: {ex.Message}");
            result = PawnIoResult.InstallFailed;
        }
        _pawnIoBusy = false;
        _pawnIoResult = result;
        _pawnIoInstalled = PawnIoSetup.IsInstalled();
        RefreshPawnIoStatus(_service.Current.Language);
        RefreshSensorNote();
    }

    void RefreshAutostart(bool enabled)
    {
        var previous = _refreshing;
        _refreshing = true;
        try
        {
            AutostartSwitch.IsChecked = enabled;
        }
        finally
        {
            _refreshing = previous;
        }
    }

    void ApplyLanguage(AppLanguage language)
    {
        string T(StringKey key) => Loc.Get(key, language);

        Title = T(StringKey.SettingsTitle);
        AppearanceSection.Content = T(StringKey.SectionAppearance);
        ColorsSection.Content = T(StringKey.SectionColors);
        FpsColorSection.Content = T(StringKey.SectionFpsColor);

        LanguageLabel.Text = T(StringKey.Language);
        StyleLabel.Text = T(StringKey.Style);
        StyleClassic.Content = T(StringKey.StyleClassic);
        StyleCyberpunk.Content = T(StringKey.StyleCyberpunk);
        CornerLabel.Text = T(StringKey.Corner);
        CornerTopLeft.Content = T(StringKey.CornerTopLeft);
        CornerTopRight.Content = T(StringKey.CornerTopRight);
        CornerBottomLeft.Content = T(StringKey.CornerBottomLeft);
        CornerBottomRight.Content = T(StringKey.CornerBottomRight);
        ScaleLabel.Text = T(StringKey.Scale);
        OpacityLabel.Text = T(StringKey.BackgroundOpacity);

        LabelColorLabel.Text = T(StringKey.LabelColor);
        ValueColorLabel.Text = T(StringKey.ValueColor);
        FpsColorLabel.Text = T(StringKey.FpsColor);

        ColorByFpsLabel.Text = T(StringKey.ColorByFps);
        GoodLabel.Text = T(StringKey.GoodThreshold);
        BadLabel.Text = T(StringKey.BadThreshold);

        StatsSection.Content = T(StringKey.SectionStats);
        OnePercentLabel.Text = T(StringKey.StatOnePercentLow);
        GpuLoadLabel.Text = T(StringKey.StatGpuLoad);
        GpuTempLabel.Text = T(StringKey.StatGpuTemp);
        CpuLoadLabel.Text = T(StringKey.StatCpuLoad);
        CpuTempLabel.Text = T(StringKey.StatCpuTemp);
        RamLabel.Text = T(StringKey.StatRam);
        PawnIoNote.Text = T(StringKey.PawnIoNeeded);
        PawnIoInstallButton.Content = T(StringKey.PawnIoInstallButton);
        RefreshPawnIoStatus(language);

        SystemSection.Content = T(StringKey.SectionSystem);
        AutostartLabel.Text = T(StringKey.Autostart);
        AutostartSwitch.ToolTip = _autostart.CanManage ? null : T(StringKey.AutostartInstalledOnly);
        LogLabel.Text = T(StringKey.LogFps);
        OpenLogButton.Content = T(StringKey.OpenLogFolder);

        ResetButton.Content = T(StringKey.ResetDefaults);
        DoneButton.Content = T(StringKey.Done);
    }

    void RefreshColors(OverlaySettings s)
    {
        if (_swatchStyle != s.Style)
        {
            _swatchStyle = s.Style;
            var palette = StyleSpecs.For(s.Style).Swatches;
            foreach (var row in _swatches)
                for (int i = 0; i < row.Length; i++)
                {
                    row[i].Tag = palette[i];
                    row[i].ToolTip = palette[i];
                    row[i].Background = OverlayTheme.FromHex(palette[i]);
                }
        }

        var colors = s.ColorsFor(s.Style);
        for (int r = 0; r < Rows; r++)
        {
            var current = ColorAt(colors, r);
            foreach (var swatch in _swatches[r])
                swatch.IsChecked = string.Equals((string)swatch.Tag, current, StringComparison.OrdinalIgnoreCase);

            // Đang gõ trong ô thì không ghi đè (con trỏ sẽ nhảy, hoặc mất mã đang gõ dở).
            var box = _hexBoxes[r];
            if (box.IsKeyboardFocused && (!HexColor.TryNormalize(box.Text, out var typed) || typed == current))
                continue;
            box.Text = current;
            box.Tag = null;
        }
    }

    // ---------- Control → cài đặt ----------

    void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var language = (AppLanguage)((FrameworkElement)sender).Tag;
        _service.Update(s => s with { Language = language });
    }

    void OnStyleChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var style = (OverlayStyle)((FrameworkElement)sender).Tag;
        _service.Update(s => s with { Style = style });
    }

    void OnCornerChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var corner = (OverlayCorner)((FrameworkElement)sender).Tag;
        _service.Update(s => s with { Corner = corner });
    }

    void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshing) return;
        var scale = Math.Round(e.NewValue) / 100;
        _service.Update(s => s with { Scale = scale });
    }

    void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshing) return;
        var opacity = Math.Round(e.NewValue) / 100;
        _service.Update(s => s with { BackgroundOpacity = opacity });
    }

    void OnColorByFpsChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var on = ColorByFpsSwitch.IsChecked == true;
        _service.Update(s => s with { ColorByFps = on });
    }

    void OnStatChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        bool one = OnePercentSwitch.IsChecked == true, gpuLoad = GpuLoadSwitch.IsChecked == true,
             gpuTemp = GpuTempSwitch.IsChecked == true, cpuLoad = CpuLoadSwitch.IsChecked == true,
             cpuTemp = CpuTempSwitch.IsChecked == true, ram = RamSwitch.IsChecked == true;
        _service.Update(s => s with
        {
            ShowOnePercentLow = one, ShowGpuLoad = gpuLoad, ShowGpuTemp = gpuTemp,
            ShowCpuLoad = cpuLoad, ShowCpuTemp = cpuTemp, ShowRam = ram,
        });
    }

    void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        _setAutostart(AutostartSwitch.IsChecked == true);
        RefreshAutostart(_autostart.IsEnabled);   // lỗi thì công tắc về đúng trạng thái thật
    }

    void OnLogChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var on = LogSwitch.IsChecked == true;
        _service.Update(s => s with { LoggingEnabled = on });
    }

    // Mở thư mục log trong Explorer (tạo nếu chưa có). Lỗi chỉ ghi vết, không làm sập cửa sổ.
    void OnOpenLogFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(SessionLogger.LogDirectory);
            // Đường dẫn đầy đủ: tên trần thì CreateProcess tìm trong thư mục exe trước (chỗ người dùng ghi được).
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            using var _ = Process.Start(explorer, $"\"{SessionLogger.LogDirectory}\"");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không mở được thư mục log: {ex.Message}");
        }
    }

    void OnGoodChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshing) return;
        var value = (int)Math.Round(e.NewValue);
        _service.Update(s => s.WithGoodThreshold(value));
    }

    void OnBadChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshing) return;
        var value = (int)Math.Round(e.NewValue);
        _service.Update(s => s.WithBadThreshold(value));
    }

    void OnSwatchChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var swatch = (RadioButton)sender;
        var row = Array.FindIndex(_swatches, r => Array.IndexOf(r, swatch) >= 0);
        SetColor(row, (string)swatch.Tag);
    }

    void OnHexChanged(object sender, TextChangedEventArgs e)
    {
        if (_refreshing) return;
        var box = (TextBox)sender;
        if (HexColor.TryNormalize(box.Text, out var hex))
        {
            box.Tag = null;
            SetColor(Array.IndexOf(_hexBoxes, box), hex);   // chỉ áp dụng mã hợp lệ
        }
        else
            box.Tag = InvalidTag;
    }

    // Rời ô với mã dở dang: trả lại mã đang dùng.
    void OnHexLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var current = ColorAt(_service.Current.ColorsFor(_service.Current.Style), Array.IndexOf(_hexBoxes, box));
        var previous = _refreshing;
        _refreshing = true;
        try
        {
            box.Text = current;
            box.Tag = null;
        }
        finally
        {
            _refreshing = previous;
        }
    }

    void OnResetClick(object sender, RoutedEventArgs e) => _service.Update(s => s.ResetToDefaults());

    void OnDoneClick(object sender, RoutedEventArgs e) => Close();

    void SetColor(int row, string hex) =>
        _service.Update(s => s.WithColors(s.Style, WithColorAt(s.ColorsFor(s.Style), row, hex)));

    // ---------- Phụ trợ ----------

    void BuildSwatches()
    {
        var panels = new[] { LabelSwatches, ValueSwatches, FpsSwatches };
        var style = (Style)FindResource("Cp.Swatch");
        for (int r = 0; r < Rows; r++)
        {
            _swatches[r] = new RadioButton[StyleSpecs.For(OverlayStyle.Cyberpunk).Swatches.Count];
            for (int i = 0; i < _swatches[r].Length; i++)
            {
                var swatch = new RadioButton { Style = style, GroupName = $"Swatch{r}" };
                swatch.Checked += OnSwatchChecked;
                _swatches[r][i] = swatch;
                panels[r].Children.Add(swatch);
            }
        }
    }

    static string ColorAt(StyleColors colors, int row) => row switch
    {
        0 => colors.Label,
        1 => colors.Value,
        _ => colors.Fps,
    };

    static StyleColors WithColorAt(StyleColors colors, int row, string hex) => row switch
    {
        0 => colors with { Label = hex },
        1 => colors with { Value = hex },
        _ => colors with { Fps = hex },
    };

    internal static BitmapSource CreateWindowIcon()
    {
        using var icon = TrayIconImage.Create();
        var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }
}
