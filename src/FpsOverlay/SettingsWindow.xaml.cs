using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FpsOverlay.Core;
using FpsOverlay.Core.GameOnly;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.PawnIo;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>
/// Cửa sổ cài đặt, theo chủ đề của phong cách đang chọn (xem <see cref="ThemeManager"/>). Mọi control gọi <see cref="SettingsService.Update"/>; cửa sổ nghe
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
    readonly Action _testAlert;
    readonly Action _checkUpdates;
    readonly Func<bool> _cpuTempAvailable;
    readonly Func<Task<PawnIoResult>> _installPawnIo;
    readonly RadioButton[][] _swatches = new RadioButton[Rows][];
    // Đọc registry lúc mở cửa sổ và sau mỗi lần cài, không đọc mỗi giây.
    bool _pawnIoInstalled;
    bool _pawnIoBusy;
    PawnIoResult? _pawnIoResult;   // kết quả lần cài gần nhất trong cửa sổ này (hiện dưới nút)
    readonly TextBox[] _hexBoxes;
    OverlayStyle? _swatchStyle;

    int _statCount;   // số ô đang bật, để dựng lại dòng "Đang hiện N/10" khi đổi ngôn ngữ
    // Hai danh sách exe: chỉ dựng lại khi nội dung đổi (so bằng giá trị).
    AppNames? _shownAlways, _shownNever;
    AppLanguage? _appListsLanguage;

    // Đang đẩy giá trị từ cài đặt vào control: sự kiện của control không được gọi Update ngược lại.
    bool _refreshing = true;

    /// <param name="setAutostart">Bật/tắt khởi động cùng Windows (App lo báo lỗi); kết quả về qua <see cref="AutostartService.Changed"/>.</param>
    /// <param name="cpuTempAvailable">Đọc được nhiệt độ CPU chưa; chưa thì hiện ghi chú cần PawnIO dưới công tắc.</param>
    /// <param name="installPawnIo">Tải + kiểm chữ ký + cài PawnIO (App lo đọc lại cảm biến; đang cài thì trả về lần đang chạy).</param>
    /// <param name="testAlert">Chạy thử cảnh báo trên overlay (App lo nháy 3 giây và tiếng).</param>
    /// <param name="checkUpdates">Nút "Kiểm tra cập nhật" (App kiểm trên nền và báo kết quả).</param>
    /// <param name="pawnIoInstalling">App đang cài PawnIO (vd. bắt đầu từ cửa sổ trước đã đóng).</param>
    internal SettingsWindow(SettingsService service, AutostartService autostart, Action<bool> setAutostart, Func<bool> cpuTempAvailable,
        Func<Task<PawnIoResult>> installPawnIo, bool pawnIoInstalling, Action testAlert, Action checkUpdates)
    {
        _service = service;
        _autostart = autostart;
        _setAutostart = setAutostart;
        _cpuTempAvailable = cpuTempAvailable;
        _installPawnIo = installPawnIo;
        _testAlert = testAlert;
        _checkUpdates = checkUpdates;
        _pawnIoInstalled = PawnIoSetup.IsInstalled();
        InitializeComponent();   // _refreshing = true: giá trị mặc định trong XAML không ghi vào cài đặt
        // Màn hình thấp thì phần giữa cuộn, tiêu đề và hai nút dưới vẫn thấy.
        MaxHeight = SystemParameters.WorkArea.Height;
        // Bản không cài không được tạo/sửa tác vụ: công tắc mờ đi, tooltip (đặt trong ApplyLanguage) giải thích lý do.
        AutostartSwitch.IsEnabled = autostart.CanManage;
        if (!autostart.CanManage) AutostartSwitch.Opacity = AutostartLabel.Opacity = 0.45;
        ToolTipService.SetShowOnDisabled(AutostartSwitch, true);
        _hexBoxes = [LabelHex, ValueHex, FpsHex];
        Icon = CreateWindowIcon();
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));
        BuildSwatches();
        VersionText.Text = AppVersion.Display(
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

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
            StyleNeonPurple.IsChecked = s.Style == OverlayStyle.NeonPurple;
            StyleTerminalGreen.IsChecked = s.Style == OverlayStyle.TerminalGreen;
            CornerTopLeft.IsChecked = s.Corner == OverlayCorner.TopLeft;
            CornerTopRight.IsChecked = s.Corner == OverlayCorner.TopRight;
            CornerBottomLeft.IsChecked = s.Corner == OverlayCorner.BottomLeft;
            CornerBottomRight.IsChecked = s.Corner == OverlayCorner.BottomRight;
            LayoutHorizontal.IsChecked = s.Layout == OverlayLayout.Horizontal;
            LayoutVertical.IsChecked = s.Layout == OverlayLayout.Vertical;

            var scale = (int)Math.Round(s.Scale * 100);
            ScaleSlider.Value = scale;
            ScaleValue.Text = $"{scale}%";
            var opacity = (int)Math.Round(s.BackgroundOpacity * 100);
            OpacitySlider.Value = opacity;
            OpacityValue.Text = $"{opacity}%";

            RefreshColors(s);
            RefreshSectionSummaries(s);

            OnePercentCheck.IsChecked = s.ShowOnePercentLow;
            GpuLoadCheck.IsChecked = s.ShowGpuLoad;
            GpuTempCheck.IsChecked = s.ShowGpuTemp;
            GpuMemoryCheck.IsChecked = s.ShowGpuMemory;
            GpuClockCheck.IsChecked = s.ShowGpuClock;
            GpuPowerCheck.IsChecked = s.ShowGpuPower;
            GpuFanCheck.IsChecked = s.ShowGpuFan;
            CpuLoadCheck.IsChecked = s.ShowCpuLoad;
            CpuTempCheck.IsChecked = s.ShowCpuTemp;
            RamCheck.IsChecked = s.ShowRam;
            RefreshSensorNote();
            _statCount = s.VisibleStatCount;
            RefreshStatsSummary();
            OnlyInGameSwitch.IsChecked = s.ShowOnlyInGame;
            RefreshAppLists(s);

            GpuHotSlider.Value = s.GpuHotThreshold;
            GpuHotValue.Text = $"{s.GpuHotThreshold}°";
            CpuHotSlider.Value = s.CpuHotThreshold;
            CpuHotValue.Text = $"{s.CpuHotThreshold}°";
            HotSecondsSlider.Value = s.HotAlertSeconds;
            HotSecondsValue.Text = $"{s.HotAlertSeconds}s";
            HotBlinkSwitch.IsChecked = s.HotAlertBlink;
            HotSoundSwitch.IsChecked = s.HotAlertSound;

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

        LanguageLabel.Text = T(StringKey.Language);
        StyleLabel.Text = T(StringKey.Style);
        StyleClassic.Content = T(StringKey.StyleClassic);
        StyleCyberpunk.Content = T(StringKey.StyleCyberpunk);
        StyleNeonPurple.Content = T(StringKey.StyleNeonPurple);
        StyleTerminalGreen.Content = T(StringKey.StyleTerminalGreen);
        CornerLabel.Text = T(StringKey.Corner);
        CornerTopLeft.Content = T(StringKey.CornerTopLeft);
        CornerTopRight.Content = T(StringKey.CornerTopRight);
        CornerBottomLeft.Content = T(StringKey.CornerBottomLeft);
        CornerBottomRight.Content = T(StringKey.CornerBottomRight);
        LayoutLabel.Text = T(StringKey.Layout);
        LayoutHorizontal.Content = T(StringKey.LayoutHorizontal);
        LayoutVertical.Content = T(StringKey.LayoutVertical);
        ScaleLabel.Text = T(StringKey.Scale);
        OpacityLabel.Text = T(StringKey.BackgroundOpacity);

        LabelColorLabel.Text = T(StringKey.LabelColor);
        ValueColorLabel.Text = T(StringKey.ValueColor);
        FpsColorLabel.Text = T(StringKey.FpsColor);

        ColorByFpsLabel.Text = T(StringKey.ColorByFps);
        GoodLabel.Text = T(StringKey.GoodThreshold);
        BadLabel.Text = T(StringKey.BadThreshold);

        StatsSection.Content = T(StringKey.SectionStats);
        OnePercentCheck.Content = T(StringKey.StatOnePercentLow);
        GpuLoadCheck.Content = T(StringKey.StatGpuLoad);
        GpuTempCheck.Content = T(StringKey.StatGpuTemp);
        GpuMemoryCheck.Content = T(StringKey.StatGpuMemory);
        GpuClockCheck.Content = T(StringKey.StatGpuClock);
        GpuPowerCheck.Content = T(StringKey.StatGpuPower);
        GpuFanCheck.Content = T(StringKey.StatGpuFan);
        CpuLoadCheck.Content = T(StringKey.StatCpuLoad);
        CpuTempCheck.Content = T(StringKey.StatCpuTemp);
        RamCheck.Content = T(StringKey.StatRam);
        PawnIoNote.Text = T(StringKey.PawnIoNeeded);
        PawnIoInstallButton.Content = T(StringKey.PawnIoInstallButton);
        RefreshPawnIoStatus(language);

        OnlyInGameLabel.Text = T(StringKey.OnlyInGame);
        RefreshToggleTitles(language);
        RefreshStatsSummary(language);

        GpuHotLabel.Text = T(StringKey.GpuHotThreshold);
        CpuHotLabel.Text = T(StringKey.CpuHotThreshold);
        HotSecondsLabel.Text = T(StringKey.HotAlertSeconds);
        HotBlinkLabel.Text = T(StringKey.HotAlertBlink);
        HotSoundLabel.Text = T(StringKey.HotAlertSound);
        TestAlertButton.Content = T(StringKey.TestAlert);

        SystemSection.Content = T(StringKey.SectionSystem);
        AutostartLabel.Text = T(StringKey.Autostart);
        AutostartSwitch.ToolTip = _autostart.CanManage ? null : T(StringKey.AutostartInstalledOnly);
        LogLabel.Text = T(StringKey.LogFps);
        OpenLogButton.Content = T(StringKey.OpenLogFolder);
        CheckUpdatesButton.Content = T(StringKey.CheckUpdates);

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

    // ---------- Ô thu gọn: màu, màu theo FPS, thông số, danh sách ứng dụng, cảnh báo ----------
    // Một cơ chế chung: panel đổi Visible/Collapsed, tiêu đề (nút hoặc nhãn mục) hiện ký hiệu đóng/mở của chủ đề
    // (Ui.ExpandGlyph / Ui.CollapseGlyph, vd. ▾/▴). Đổi chủ đề thì Refresh dựng lại tiêu đề (App đổi chủ đề trước).

    string Arrow(UIElement panel) =>
        TryFindResource(panel.Visibility == Visibility.Visible ? "Ui.CollapseGlyph" : "Ui.ExpandGlyph") as string ?? "";

    void ToggleSection(UIElement panel)
    {
        panel.Visibility = panel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        RefreshToggleTitles(_service.Current.Language);
    }

    // Tag của nút / thanh tiêu đề là tên panel cần xổ/thu.
    void OnToggleClick(object sender, RoutedEventArgs e) => ToggleSection((UIElement)FindName((string)((FrameworkElement)sender).Tag));

    void RefreshToggleTitles(AppLanguage language)
    {
        string Title(StringKey key, UIElement panel) => $"{Loc.Get(key, language)} {Arrow(panel)}";
        StatsButton.Content = Title(StringKey.ChooseStats, StatsPanel);
        AppListsButton.Content = Title(StringKey.AppLists, AppListsPanel);
        ColorsSection.Content = Title(StringKey.SectionColors, ColorsPanel);
        FpsColorSection.Content = Title(StringKey.SectionFpsColor, FpsColorPanel);
        AlertsSection.Content = Title(StringKey.SectionAlerts, AlertsPanel);
    }

    // Tóm tắt của ba mục mới thu gọn: ba ô màu, "Bật · tốt/kém" (hoặc "Tắt"), "GPU x° · CPU y° · zs".
    void RefreshSectionSummaries(OverlaySettings s)
    {
        var colors = s.ColorsFor(s.Style);
        LabelSummarySwatch.Background = OverlayTheme.FromHex(colors.Label);
        ValueSummarySwatch.Background = OverlayTheme.FromHex(colors.Value);
        FpsSummarySwatch.Background = OverlayTheme.FromHex(colors.Fps);
        FpsColorSummary.Text = s.ColorByFps
            ? string.Format(Loc.Get(StringKey.FpsColorSummaryOn, s.Language), s.GoodThreshold, s.BadThreshold)
            : Loc.Get(StringKey.FpsColorSummaryOff, s.Language);
        AlertsSummary.Text = string.Format(Loc.Get(StringKey.AlertsSummary, s.Language),
            s.GpuHotThreshold, s.CpuHotThreshold, s.HotAlertSeconds);
    }

    void RefreshStatsSummary(AppLanguage? language = null) =>
        StatsSummary.Text = string.Format(Loc.Get(StringKey.StatsSummary, language ?? _service.Current.Language),
            _statCount, OverlaySettings.StatCount);

    // Tóm tắt + các dòng "tên exe  [Xoá]" của hai danh sách; dựng lại khi danh sách đổi hoặc đổi ngôn ngữ.
    void RefreshAppLists(OverlaySettings s)
    {
        var language = s.Language;
        AppListsSummary.Text = string.Format(Loc.Get(StringKey.AppListsSummary, language), s.AlwaysShowApps.Count, s.NeverShowApps.Count);
        if (Equals(_shownAlways, s.AlwaysShowApps) && Equals(_shownNever, s.NeverShowApps) && _appListsLanguage == language) return;
        _shownAlways = s.AlwaysShowApps;
        _shownNever = s.NeverShowApps;
        _appListsLanguage = language;
        AppListsPanel.Children.Clear();
        AddAppGroup(Loc.Get(StringKey.AlwaysShowList, language), s.AlwaysShowApps, language);
        AddAppGroup(Loc.Get(StringKey.NeverShowList, language), s.NeverShowApps, language);
    }

    void AddAppGroup(string title, AppNames apps, AppLanguage language)
    {
        // Gắn khoá chủ đề bằng SetResourceReference (động): đổi chủ đề thì các dòng dựng từ code cũng đổi theo.
        var heading = new TextBlock { Text = title, Margin = new Thickness(0, 4, 0, 2) };
        heading.SetResourceReference(StyleProperty, "Ui.FieldLabel");
        AppListsPanel.Children.Add(heading);
        if (apps.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = Loc.Get(StringKey.AppListEmpty, language), Margin = new Thickness(8, 0, 0, 0),
            };
            empty.SetResourceReference(StyleProperty, "Ui.FieldLabel");
            empty.SetResourceReference(TextBlock.FontSizeProperty, "Ui.SmallFontSize");
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Ui.Muted");
            AppListsPanel.Children.Add(empty);
            return;
        }
        foreach (var exe in apps)
        {
            var row = new DockPanel { Margin = new Thickness(8, 1, 0, 1), LastChildFill = true };
            var remove = new Button
            {
                Content = Loc.Get(StringKey.RemoveApp, language),
                Padding = new Thickness(8, 1, 8, 1), Tag = exe,
            };
            remove.SetResourceReference(StyleProperty, "Ui.GhostButton");
            remove.SetResourceReference(FontSizeProperty, "Ui.SmallFontSize");
            remove.Click += OnRemoveAppClick;
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            var name = new TextBlock
            {
                Text = exe, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            name.SetResourceReference(TextBlock.FontFamilyProperty, "Ui.Mono");
            name.SetResourceReference(TextBlock.FontSizeProperty, "Ui.CompactFontSize");
            name.SetResourceReference(TextBlock.ForegroundProperty, "Ui.Text");
            row.Children.Add(name);
            AppListsPanel.Children.Add(row);
        }
    }

    void OnRemoveAppClick(object sender, RoutedEventArgs e)
    {
        var exe = (string)((FrameworkElement)sender).Tag;
        _service.Update(s => s.WithoutApp(exe));
    }

    void OnOnlyInGameChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var on = OnlyInGameSwitch.IsChecked == true;
        _service.Update(s => s with { ShowOnlyInGame = on });
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
        _service.Update(s => s.WithCorner(corner));
    }

    void OnLayoutChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        var layout = (OverlayLayout)((FrameworkElement)sender).Tag;
        _service.Update(s => s with { Layout = layout });
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
        bool one = OnePercentCheck.IsChecked == true, gpuLoad = GpuLoadCheck.IsChecked == true,
             gpuTemp = GpuTempCheck.IsChecked == true, cpuLoad = CpuLoadCheck.IsChecked == true,
             cpuTemp = CpuTempCheck.IsChecked == true, ram = RamCheck.IsChecked == true,
             gpuMemory = GpuMemoryCheck.IsChecked == true, gpuClock = GpuClockCheck.IsChecked == true,
             gpuPower = GpuPowerCheck.IsChecked == true, gpuFan = GpuFanCheck.IsChecked == true;
        _service.Update(s => s with
        {
            ShowOnePercentLow = one, ShowGpuLoad = gpuLoad, ShowGpuTemp = gpuTemp,
            ShowGpuMemory = gpuMemory, ShowGpuClock = gpuClock, ShowGpuPower = gpuPower, ShowGpuFan = gpuFan,
            ShowCpuLoad = cpuLoad, ShowCpuTemp = cpuTemp, ShowRam = ram,
        });
    }

    void OnGpuHotChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshing) return;
        var value = (int)Math.Round(e.NewValue);
        _service.Update(s => s with { GpuHotThreshold = value });
    }

    void OnCpuHotChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshing) return;
        var value = (int)Math.Round(e.NewValue);
        _service.Update(s => s with { CpuHotThreshold = value });
    }

    void OnHotSecondsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshing) return;
        var value = (int)Math.Round(e.NewValue);
        _service.Update(s => s with { HotAlertSeconds = value });
    }

    void OnHotAlertToggled(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        bool blink = HotBlinkSwitch.IsChecked == true, sound = HotSoundSwitch.IsChecked == true;
        _service.Update(s => s with { HotAlertBlink = blink, HotAlertSound = sound });
    }

    void OnTestAlertClick(object sender, RoutedEventArgs e) => _testAlert();

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

    void OnCheckUpdatesClick(object sender, RoutedEventArgs e) => _checkUpdates();

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
        for (int r = 0; r < Rows; r++)
        {
            _swatches[r] = new RadioButton[StyleSpecs.For(OverlayStyle.Cyberpunk).Swatches.Count];
            for (int i = 0; i < _swatches[r].Length; i++)
            {
                var swatch = new RadioButton { GroupName = $"Swatch{r}" };
                swatch.SetResourceReference(StyleProperty, "Ui.Swatch");
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
