using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>
/// Cửa sổ cài đặt kiểu Cyberpunk. Mọi control gọi <see cref="SettingsService.Update"/>; cửa sổ nghe
/// <see cref="SettingsService.Changed"/> để cập nhật lại control và chữ theo ngôn ngữ.
/// </summary>
public partial class SettingsWindow : Window
{
    static readonly string[] CyberpunkSwatches =
        ["#00F0FF", "#FCEE0A", "#FF003C", "#FF2A6D", "#05D9E8", "#D1F7FF", "#39FF14", "#B967FF", "#FFFFFF", "#FF8C1A"];

    static readonly string[] ClassicSwatches =
        ["#FF8C1A", "#FFFFFF", "#39FF14", "#FFD400", "#FF3B30", "#00BFFF", "#FF66CC", "#A0A0A0", "#00FF99", "#FFAA00"];

    // Ba dòng màu: 0 = nhãn, 1 = giá trị, 2 = FPS.
    const int Rows = 3;
    const string InvalidTag = "invalid";

    readonly SettingsService _service;
    readonly RadioButton[][] _swatches = new RadioButton[Rows][];
    readonly TextBox[] _hexBoxes;
    OverlayStyle? _swatchStyle;

    // Đang đẩy giá trị từ cài đặt vào control: sự kiện của control không được gọi Update ngược lại.
    bool _refreshing = true;

    internal SettingsWindow(SettingsService service)
    {
        _service = service;
        InitializeComponent();   // _refreshing = true: giá trị mặc định trong XAML không ghi vào cài đặt
        _hexBoxes = [LabelHex, ValueHex, FpsHex];
        Icon = CreateWindowIcon();
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));
        BuildSwatches();

        Refresh(service.Current);
        service.Changed += Refresh;
    }

    protected override void OnClosed(EventArgs e)
    {
        _service.Changed -= Refresh;
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

        ResetButton.Content = T(StringKey.ResetDefaults);
        DoneButton.Content = T(StringKey.Done);
    }

    void RefreshColors(OverlaySettings s)
    {
        if (_swatchStyle != s.Style)
        {
            _swatchStyle = s.Style;
            var palette = s.Style == OverlayStyle.Classic ? ClassicSwatches : CyberpunkSwatches;
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
        _refreshing = true;
        try
        {
            box.Text = current;
            box.Tag = null;
        }
        finally
        {
            _refreshing = false;
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
            _swatches[r] = new RadioButton[CyberpunkSwatches.Length];
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

    static BitmapSource CreateWindowIcon()
    {
        using var icon = TrayIconImage.Create();
        var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }
}
