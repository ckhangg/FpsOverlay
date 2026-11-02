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
    public void Save_creates_directory()
    {
        var path = Path.Combine(_dir, "sub", "settings.json");
        SettingsStore.Save(path, OverlaySettings.Defaults);
        Assert.True(File.Exists(path));
    }
}
