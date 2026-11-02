using FpsOverlay.Core;

namespace FpsOverlay.Core.Tests;

public class PreviewFpsTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1.5, 82)]
    [InlineData(3, 144)]
    [InlineData(4.5, 82)]
    [InlineData(6, 20)]
    [InlineData(9, 144)]
    public void Preview_triangle(double t, double expected) =>
        Assert.Equal(expected, PreviewFps.At(t), 3);
}
