using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public class OverlayPlacementTests
{
    static readonly PixelRect Primary = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(OverlayCorner.TopLeft, 8, 8)]
    [InlineData(OverlayCorner.TopRight, 1920 - 8 - 200, 8)]
    [InlineData(OverlayCorner.BottomLeft, 8, 1080 - 8 - 40)]
    [InlineData(OverlayCorner.BottomRight, 1920 - 8 - 200, 1080 - 8 - 40)]
    public void Four_corners_on_primary_monitor(OverlayCorner corner, int x, int y) =>
        Assert.Equal((x, y), OverlayPlacement.Compute(Primary, 200, 40, corner, 8));

    [Fact]
    public void Placement_on_secondary_monitor_left_of_primary()
    {
        var secondary = new PixelRect(-2560, 0, 0, 1440);
        Assert.Equal((-8 - 200, 8), OverlayPlacement.Compute(secondary, 200, 40, OverlayCorner.TopRight, 8));
    }

    [Fact]
    public void Window_larger_than_monitor_is_clamped_to_left_and_top_edge()
    {
        Assert.Equal((0, 0), OverlayPlacement.Compute(Primary, 3000, 2000, OverlayCorner.BottomRight, 8));
        var secondary = new PixelRect(-2560, 0, 0, 1440);
        Assert.Equal((-2560, 0), OverlayPlacement.Compute(secondary, 3000, 2000, OverlayCorner.BottomRight, 8));
    }
}
