using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public class OverlaySettingsTests
{
    [Fact]
    public void Defaults_match_spec()
    {
        var d = OverlaySettings.Defaults;
        Assert.Equal(OverlayStyle.Cyberpunk, d.Style);
        Assert.Equal(OverlayCorner.TopLeft, d.Corner);
        Assert.Equal(1.0, d.Scale);
        Assert.Equal(0.72, d.BackgroundOpacity);
        Assert.True(d.ColorByFps);
        Assert.Equal(60, d.GoodThreshold);
        Assert.Equal(30, d.BadThreshold);
        Assert.Equal(AppLanguage.Vietnamese, d.Language);
        Assert.Equal(new StyleColors("#00F0FF", "#FFFFFF", "#FCEE0A"), d.CyberpunkColors);
        Assert.Equal(new StyleColors("#FF8C1A", "#FFFFFF", "#39FF14"), d.ClassicColors);
    }

    [Fact]
    public void Normalize_clamps_ranges()
    {
        var s = (OverlaySettings.Defaults with { Scale = 9, BackgroundOpacity = -1, GoodThreshold = 999, BadThreshold = 1 }).Normalize();
        Assert.Equal(3.0, s.Scale);
        Assert.Equal(0, s.BackgroundOpacity);
        Assert.Equal(240, s.GoodThreshold);
        Assert.Equal(10, s.BadThreshold);
    }

    [Fact]
    public void Normalize_fixes_inverted_thresholds()
    {
        var s = (OverlaySettings.Defaults with { GoodThreshold = 40, BadThreshold = 50 }).Normalize();
        Assert.Equal(40, s.GoodThreshold);
        Assert.Equal(39, s.BadThreshold);
    }

    [Fact]
    public void Normalize_replaces_only_bad_color()
    {
        var s = (OverlaySettings.Defaults with { CyberpunkColors = new("nope", "#abcdef", "#FCEE0A") }).Normalize();
        Assert.Equal(new StyleColors("#00F0FF", "#ABCDEF", "#FCEE0A"), s.CyberpunkColors);
    }

    [Fact]
    public void Dragging_bad_above_good_pushes_good()
    {
        var s = OverlaySettings.Defaults.WithBadThreshold(80);
        Assert.Equal(80, s.BadThreshold);
        Assert.Equal(81, s.GoodThreshold);
    }

    [Fact]
    public void Dragging_good_below_bad_pushes_bad()
    {
        var s = OverlaySettings.Defaults.WithGoodThreshold(20);
        Assert.Equal(20, s.GoodThreshold);
        Assert.Equal(19, s.BadThreshold);
    }

    [Fact]
    public void Reset_keeps_language_style_and_other_style_colors()
    {
        var custom = new StyleColors("#111111", "#222222", "#333333");
        var s = (OverlaySettings.Defaults with
        {
            Language = AppLanguage.English,
            Style = OverlayStyle.Classic,
            Scale = 2,
            ClassicColors = custom,
            CyberpunkColors = custom
        }).ResetToDefaults();
        Assert.Equal(AppLanguage.English, s.Language);
        Assert.Equal(OverlayStyle.Classic, s.Style);
        Assert.Equal(1.0, s.Scale);
        Assert.Equal(StyleColors.DefaultFor(OverlayStyle.Classic), s.ClassicColors);
        Assert.Equal(custom, s.CyberpunkColors);
    }

    [Fact]
    public void Reset_keeps_colors_of_other_three_styles()
    {
        var c = new StyleColors("#111111", "#222222", "#333333");
        var s = (OverlaySettings.Defaults with
        {
            Style = OverlayStyle.NeonPurple,
            ClassicColors = c, CyberpunkColors = c, NeonPurpleColors = c, TerminalGreenColors = c
        }).ResetToDefaults();
        Assert.Equal(StyleColors.DefaultFor(OverlayStyle.NeonPurple), s.NeonPurpleColors);
        Assert.Equal(c, s.ClassicColors);
        Assert.Equal(c, s.CyberpunkColors);
        Assert.Equal(c, s.TerminalGreenColors);
    }

    [Fact]
    public void WithColors_round_trips_for_every_style_and_leaves_others_untouched()
    {
        var c = new StyleColors("#010203", "#040506", "#070809");
        foreach (var style in Enum.GetValues<OverlayStyle>())
        {
            var s = OverlaySettings.Defaults.WithColors(style, c);
            Assert.Equal(c, s.ColorsFor(style));
            foreach (var other in Enum.GetValues<OverlayStyle>().Where(o => o != style))
                Assert.Equal(OverlaySettings.Defaults.ColorsFor(other), s.ColorsFor(other));
        }
    }

    [Fact]
    public void Unknown_style_colors_throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlaySettings.Defaults.ColorsFor((OverlayStyle)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlaySettings.Defaults.WithColors((OverlayStyle)99, new StyleColors("#000000", "#000000", "#000000")));
    }

    [Theory]
    [InlineData("#abcdef", "#ABCDEF")]
    [InlineData("00f0ff", "#00F0FF")]
    public void Hex_normalizes(string input, string expected)
    {
        Assert.True(HexColor.TryNormalize(input, out var n));
        Assert.Equal(expected, n);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData(null)]
    public void Hex_rejects(string? input) => Assert.False(HexColor.TryNormalize(input, out _));

    [Fact]
    public void Stat_defaults()
    {
        var d = OverlaySettings.Defaults;
        Assert.True(d.ShowOnePercentLow && d.ShowGpuLoad && d.ShowGpuTemp && d.ShowCpuLoad && d.ShowCpuTemp && d.ShowRam);
        Assert.True(d.LoggingEnabled);
        Assert.False(d.PawnIoOffered);
    }

    [Fact]
    public void Reset_restores_stats_but_keeps_logging_and_pawnio()
    {
        var s = (OverlaySettings.Defaults with { ShowRam = false, ShowGpuTemp = false, LoggingEnabled = false, PawnIoOffered = true }).ResetToDefaults();
        Assert.True(s.ShowRam);
        Assert.True(s.ShowGpuTemp);
        Assert.False(s.LoggingEnabled);
        Assert.True(s.PawnIoOffered);
    }

    [Fact]
    public void New_gpu_switches_default_off()
    {
        var d = OverlaySettings.Defaults;
        Assert.False(d.ShowGpuMemory || d.ShowGpuClock || d.ShowGpuPower || d.ShowGpuFan);
    }

    [Fact]
    public void Alert_defaults_match_spec()
    {
        var d = OverlaySettings.Defaults;
        Assert.Equal(85, d.GpuHotThreshold);
        Assert.Equal(90, d.CpuHotThreshold);
        Assert.Equal(10, d.HotAlertSeconds);
        Assert.True(d.HotAlertBlink);
        Assert.False(d.HotAlertSound);
    }

    [Fact]
    public void Normalize_clamps_alert_values()
    {
        var s = (OverlaySettings.Defaults with { GpuHotThreshold = 10, CpuHotThreshold = 200, HotAlertSeconds = -5 }).Normalize();
        Assert.Equal(50, s.GpuHotThreshold);
        Assert.Equal(110, s.CpuHotThreshold);
        Assert.Equal(0, s.HotAlertSeconds);
        Assert.Equal(60, (OverlaySettings.Defaults with { HotAlertSeconds = 99 }).Normalize().HotAlertSeconds);
    }

    [Fact]
    public void Reset_restores_alert_settings()
    {
        var s = (OverlaySettings.Defaults with { GpuHotThreshold = 70, CpuHotThreshold = 70, HotAlertSeconds = 30, HotAlertBlink = false, HotAlertSound = true }).ResetToDefaults();
        Assert.Equal(85, s.GpuHotThreshold);
        Assert.Equal(90, s.CpuHotThreshold);
        Assert.Equal(10, s.HotAlertSeconds);
        Assert.True(s.HotAlertBlink);
        Assert.False(s.HotAlertSound);
    }

    [Fact]
    public void Reset_turns_new_gpu_switches_off()
    {
        var s = (OverlaySettings.Defaults with { ShowGpuMemory = true, ShowGpuClock = true, ShowGpuPower = true, ShowGpuFan = true }).ResetToDefaults();
        Assert.False(s.ShowGpuMemory || s.ShowGpuClock || s.ShowGpuPower || s.ShowGpuFan);
    }

    [Fact]
    public void With_corner_resets_offsets()
    {
        var s = (OverlaySettings.Defaults with { OffsetX = 300, OffsetY = 50 }).WithCorner(OverlayCorner.BottomLeft);
        Assert.Equal(OverlayCorner.BottomLeft, s.Corner);
        Assert.Equal(OverlaySettings.DefaultOffset, s.OffsetX);
        Assert.Equal(OverlaySettings.DefaultOffset, s.OffsetY);
    }

    [Fact]
    public void Layout_and_offsets_defaults()
    {
        Assert.Equal(OverlayLayout.Horizontal, OverlaySettings.Defaults.Layout);
        Assert.Equal(8, OverlaySettings.Defaults.OffsetX);
        Assert.Equal(8, OverlaySettings.Defaults.OffsetY);
    }

    [Fact]
    public void Normalize_clamps_offsets()
    {
        var s = (OverlaySettings.Defaults with { OffsetX = -5, OffsetY = 9999 }).Normalize();
        Assert.Equal(0, s.OffsetX);
        Assert.Equal(4000, s.OffsetY);
        Assert.Equal(8, (OverlaySettings.Defaults with { OffsetX = double.NaN }).Normalize().OffsetX);
    }
}
