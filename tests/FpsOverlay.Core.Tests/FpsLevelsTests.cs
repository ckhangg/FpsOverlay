using FpsOverlay.Core;

namespace FpsOverlay.Core.Tests;

public class FpsLevelsTests
{
    [Theory]
    [InlineData(null, FpsLevel.None)]
    [InlineData(60.0, FpsLevel.Good)]
    [InlineData(59.6, FpsLevel.Good)]
    [InlineData(59.4, FpsLevel.Medium)]
    [InlineData(30.0, FpsLevel.Medium)]
    [InlineData(29.4, FpsLevel.Bad)]
    public void Classify(double? fps, FpsLevel expected) =>
        Assert.Equal(expected, FpsLevels.Classify(fps, 60, 30));
}
