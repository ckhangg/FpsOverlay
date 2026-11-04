using FpsOverlay.Core;

namespace FpsOverlay.Core.Tests;

public class AsciiBarTests
{
    [Fact]
    public void Sixty_percent_is_six_cells() => Assert.Equal("[######----]", AsciiBar.Render(60, 0, 100));

    [Theory]
    [InlineData(0, "[----------]")]
    [InlineData(100, "[##########]")]
    [InlineData(4, "[----------]")]     // 0.4 ô → 0
    [InlineData(5, "[#---------]")]     // 0.5 ô → làm tròn nửa lên
    [InlineData(95, "[##########]")]
    public void Rounds_to_nearest_cell(double value, string expected) => Assert.Equal(expected, AsciiBar.Render(value, 0, 100));

    [Fact]
    public void Uses_the_range_not_zero() => Assert.Equal("[#####-----]", AsciiBar.Render(175, 50, 300));

    [Theory]
    [InlineData(-10)]
    [InlineData(double.NaN)]
    public void Below_range_or_nan_is_empty(double value) => Assert.Equal("[----------]", AsciiBar.Render(value, 0, 100));

    [Fact]
    public void Above_range_is_full() => Assert.Equal("[##########]", AsciiBar.Render(500, 0, 100));

    [Fact]
    public void Empty_range_is_all_dashes() => Assert.Equal("[----------]", AsciiBar.Render(50, 50, 50));

    [Fact]
    public void Cell_count_is_configurable() => Assert.Equal("[##--]", AsciiBar.Render(50, 0, 100, 4));
}
