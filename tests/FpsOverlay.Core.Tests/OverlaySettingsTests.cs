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
    public void Reset_turns_new_gpu_switches_off()
    {
        var s = (OverlaySettings.Defaults with { ShowGpuMemory = true, ShowGpuClock = true, ShowGpuPower = true, ShowGpuFan = true }).ResetToDefaults();
        Assert.False(s.ShowGpuMemory || s.ShowGpuClock || s.ShowGpuPower || s.ShowGpuFan);
    }
}
