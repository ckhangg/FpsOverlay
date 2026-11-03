using System.Text.Json;
using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FpsOverlayTests-" + Guid.NewGuid().ToString("N"));

    private string PathIn(string name = "settings.json") => Path.Combine(_dir, name);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults() =>
        Assert.Equal(OverlaySettings.Defaults, SettingsStore.Load(PathIn()));

    [Fact]
    public void Corrupt_json_gives_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), "{ not json");
        Assert.Equal(OverlaySettings.Defaults, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Round_trips()
    {
        var original = (OverlaySettings.Defaults with
        {
            Style = OverlayStyle.Classic,
            Corner = OverlayCorner.BottomRight,
            Scale = 1.75,
            BackgroundOpacity = 0.4,
            ColorByFps = false,
            GoodThreshold = 90,
            BadThreshold = 45,
            Language = AppLanguage.English,
            ClassicColors = new StyleColors("#123456", "#ABCDEF", "#010203")
        });
        SettingsStore.Save(PathIn(), original);
        Assert.Equal(original, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Layout_and_offsets_round_trip()
    {
        var original = OverlaySettings.Defaults with { Layout = OverlayLayout.Vertical, OffsetX = 123.5, OffsetY = 40 };
        SettingsStore.Save(PathIn(), original);
        Assert.Equal(original, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Store_round_trips_new_styles()
    {
        var original = OverlaySettings.Defaults with
        {
            Style = OverlayStyle.TerminalGreen,
            NeonPurpleColors = new StyleColors("#123456", "#ABCDEF", "#010203"),
            TerminalGreenColors = new StyleColors("#0A0B0C", "#1A1B1C", "#2A2B2C")
        };
        SettingsStore.Save(PathIn(), original);
        Assert.Equal(original, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Unknown_enum_and_missing_fields_fall_back()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), "{\"Style\":\"Neon\",\"Scale\":2}");
        var s = SettingsStore.Load(PathIn());
        Assert.Equal(OverlayStyle.Cyberpunk, s.Style);
        Assert.Equal(2.0, s.Scale);
        Assert.Equal(OverlaySettings.Defaults with { Scale = 2.0 }, s);
    }

    [Fact]
    public void Failed_save_throws_and_leaves_no_temp_file()
    {
        // Đích là một thư mục nên bước Move thất bại.
        Directory.CreateDirectory(PathIn());
        Assert.ThrowsAny<Exception>(() => SettingsStore.Save(PathIn(), OverlaySettings.Defaults));
        Assert.False(File.Exists(PathIn() + ".tmp"));
    }

    [Fact]
    public void Path_that_is_a_directory_loads_defaults()
    {
        Directory.CreateDirectory(PathIn());
        Assert.Equal(OverlaySettings.Defaults, SettingsStore.Load(PathIn()));
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{\"Scale\":\"big\",\"GoodThreshold\":1e400,\"ClassicColors\":5}")]
    public void Unusable_content_loads_defaults(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), json);
        Assert.Equal(OverlaySettings.Defaults, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Save_creates_directory()
    {
        var path = Path.Combine(_dir, "sub", "settings.json");
        SettingsStore.Save(path, OverlaySettings.Defaults);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Old_file_without_stat_fields_gets_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), "{ \"Scale\": 2 }");
        var s = SettingsStore.Load(PathIn());
        Assert.Equal(2, s.Scale);
        Assert.True(s.ShowOnePercentLow && s.ShowGpuLoad && s.ShowGpuTemp && s.ShowCpuLoad && s.ShowCpuTemp && s.ShowRam);
        Assert.True(s.LoggingEnabled);
        Assert.False(s.PawnIoOffered);
    }

    [Fact]
    public void Stat_fields_round_trip()
    {
        var original = OverlaySettings.Defaults with
        {
            ShowOnePercentLow = false, ShowGpuLoad = false, ShowGpuTemp = false, ShowCpuLoad = false,
            ShowCpuTemp = false, ShowRam = false, LoggingEnabled = false, PawnIoOffered = true,
        };
        SettingsStore.Save(PathIn(), original);
        Assert.Equal(original, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Old_file_without_new_fields_loads_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), "{\"Style\":\"Classic\",\"ClassicColors\":{\"Label\":\"#123456\",\"Value\":\"#FFFFFF\",\"Fps\":\"#39FF14\"}}");
        var s = SettingsStore.Load(PathIn());
        Assert.Equal(OverlayStyle.Classic, s.Style);
        Assert.Equal("#123456", s.ClassicColors.Label);
        Assert.Equal(OverlaySettings.Defaults with { Style = OverlayStyle.Classic, ClassicColors = s.ClassicColors }, s);
    }

    [Fact]
    public void Unknown_style_falls_back_to_default()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), "{\"Style\":\"Rainbow\"}");
        Assert.Equal(OverlayStyle.Cyberpunk, SettingsStore.Load(PathIn()).Style);
    }

    [Fact]
    public void Store_round_trips_alert_settings()
    {
        var original = OverlaySettings.Defaults with { GpuHotThreshold = 80, CpuHotThreshold = 95, HotAlertSeconds = 25, HotAlertBlink = false, HotAlertSound = true };
        SettingsStore.Save(PathIn(), original);
        Assert.Equal(original, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Store_round_trips_new_switches()
    {
        var original = OverlaySettings.Defaults with { ShowGpuMemory = true, ShowGpuClock = true, ShowGpuPower = true, ShowGpuFan = true };
        SettingsStore.Save(PathIn(), original);
        Assert.Equal(original, SettingsStore.Load(PathIn()));
    }

    [Fact]
    public void Old_file_gets_game_only_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), "{\"Style\":\"Classic\"}");
        var s = SettingsStore.Load(PathIn());
        Assert.Equal(OverlayStyle.Classic, s.Style);
        Assert.True(s.ShowOnlyInGame);
        Assert.Empty(s.AlwaysShowApps);
        Assert.Empty(s.NeverShowApps);
    }

    [Fact]
    public void Store_round_trips_app_lists()
    {
        var original = (OverlaySettings.Defaults with { ShowOnlyInGame = false })
            .WithAlwaysShow("celeste.exe").WithNeverShow("chrome.exe");
        SettingsStore.Save(PathIn(), original);
        Assert.Equal(original, SettingsStore.Load(PathIn()));

        using var doc = JsonDocument.Parse(File.ReadAllText(PathIn()));
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("AlwaysShowApps").ValueKind);
        Assert.Equal("celeste.exe", doc.RootElement.GetProperty("AlwaysShowApps")[0].GetString());
    }

    [Fact]
    public void Settings_with_same_lists_are_equal()
    {
        SettingsStore.Save(PathIn(), OverlaySettings.Defaults.WithAlwaysShow("a.exe").WithNeverShow("b.exe"));
        Assert.Equal(SettingsStore.Load(PathIn()), SettingsStore.Load(PathIn()));
        Assert.Equal(SettingsStore.Load(PathIn()).GetHashCode(), SettingsStore.Load(PathIn()).GetHashCode());
    }

    [Fact]
    public void Broken_app_lists_load_as_empty_and_bad_items_are_ignored()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn(), "{\"AlwaysShowApps\":\"oops\",\"NeverShowApps\":[1,\"x.exe\",null,{\"a\":1},\"\"]}");
        var s = SettingsStore.Load(PathIn());
        Assert.Empty(s.AlwaysShowApps);
        Assert.Equal(["x.exe"], s.NeverShowApps);
    }
}
