using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public class LocTests
{
    [Fact]
    public void Every_key_has_both_languages()
    {
        foreach (var key in Enum.GetValues<StringKey>())
            foreach (var lang in Enum.GetValues<AppLanguage>())
                Assert.False(string.IsNullOrWhiteSpace(Loc.Get(key, lang)), $"{key}/{lang}");
    }

    [Fact]
    public void Spot_check()
    {
        Assert.Equal("Ẩn FPS", Loc.Get(StringKey.TrayHide, AppLanguage.Vietnamese));
        Assert.Equal("Hide FPS", Loc.Get(StringKey.TrayHide, AppLanguage.English));
    }
}
