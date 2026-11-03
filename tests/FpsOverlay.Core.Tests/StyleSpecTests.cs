using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public sealed class StyleSpecTests
{
    [Fact]
    public void Classic_matches_previous_constants()
    {
        var s = StyleSpecs.For(OverlayStyle.Classic);
        Assert.Equal(new StyleColors("#FF8C1A", "#FFFFFF", "#39FF14"), s.DefaultColors);
        Assert.Equal(("#39FF14", "#FFD400", "#FF3B30"), (s.Good, s.Medium, s.Bad));
        Assert.Equal("#000000", s.Background);
        Assert.Equal((" · ", ""), (s.Separator, s.Prompt));
        Assert.Equal(["#FF8C1A", "#FFFFFF", "#39FF14", "#FFD400", "#FF3B30", "#00BFFF", "#FF66CC", "#A0A0A0", "#00FF99", "#FFAA00"], s.Swatches);
    }

    [Fact]
    public void Cyberpunk_matches_previous_constants()
    {
        var s = StyleSpecs.For(OverlayStyle.Cyberpunk);
        Assert.Equal(new StyleColors("#00F0FF", "#FFFFFF", "#FCEE0A"), s.DefaultColors);
        Assert.Equal(("#00F0FF", "#FCEE0A", "#FF003C"), (s.Good, s.Medium, s.Bad));
        Assert.Equal("#0A0A0F", s.Background);
        Assert.Equal(10, s.Swatches.Count);
    }

    [Fact]
    public void Every_style_has_a_spec_with_ten_valid_swatches()
    {
        foreach (var style in Enum.GetValues<OverlayStyle>())
        {
            var s = StyleSpecs.For(style);
            Assert.Equal(10, s.Swatches.Count);
            var all = new[] { s.DefaultColors.Label, s.DefaultColors.Value, s.DefaultColors.Fps, s.Good, s.Medium, s.Bad, s.Background }
                .Concat(s.Swatches);
            foreach (var c in all)
                Assert.True(HexColor.TryNormalize(c, out _), $"{style}: {c}");
        }
    }
}
