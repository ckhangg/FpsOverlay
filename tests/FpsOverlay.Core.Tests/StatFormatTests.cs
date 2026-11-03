namespace FpsOverlay.Core.Tests;

public class StatFormatTests
{
    [Fact]
    public void Null_is_dashes()
    {
        Assert.Equal("--", StatFormat.Percent(null));
        Assert.Equal("--", StatFormat.Temperature(null));
        Assert.Equal("--", StatFormat.Gigabytes(null));
    }

    [Fact]
    public void Formats_round_half_up()
    {
        Assert.Equal("67%", StatFormat.Percent(66.5));
        Assert.Equal("71°", StatFormat.Temperature(70.6));
        Assert.Equal("9.2G", StatFormat.Gigabytes(9.24));
        Assert.Equal("9.3G", StatFormat.Gigabytes(9.25));
    }

    [Fact]
    public void Out_of_range_still_formats()
    {
        Assert.Equal("120%", StatFormat.Percent(120));
        Assert.Equal("-5°", StatFormat.Temperature(-5));
        Assert.Equal("0.0G", StatFormat.Gigabytes(0));
    }

    [Fact]
    public void Same_rounded_value_is_same_string_instance()
    {
        Assert.Same(StatFormat.Percent(66.6), StatFormat.Percent(67.4));
        Assert.Same(StatFormat.Temperature(70.6), StatFormat.Temperature(71.4));
    }
}

public class HeatLevelsTests
{
    [Fact]
    public void Compares_rounded_value()
    {
        Assert.True(HeatLevels.IsHot(84.6, 85));
        Assert.False(HeatLevels.IsHot(84.4, 85));
        Assert.False(HeatLevels.IsHot(null, 85));
    }

    [Fact]
    public void Units_with_and_without_suffix()
    {
        Assert.Equal("1590MHz", StatFormat.Megahertz(1589.6, true));
        Assert.Equal("1590", StatFormat.Megahertz(1589.6, false));
        Assert.Equal("48W", StatFormat.Watts(47.5, true));
        Assert.Equal("2400RPM", StatFormat.Rpm(2400, true));
        Assert.Equal("--", StatFormat.Rpm(null, true));
    }
}
