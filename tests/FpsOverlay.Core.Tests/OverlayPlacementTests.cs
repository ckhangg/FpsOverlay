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
        Assert.Equal((x, y), OverlayPlacement.Compute(Primary, 200, 40, corner, 8, 8));

    [Fact]
    public void Placement_on_secondary_monitor_left_of_primary()
    {
        var secondary = new PixelRect(-2560, 0, 0, 1440);
        Assert.Equal((-8 - 200, 8), OverlayPlacement.Compute(secondary, 200, 40, OverlayCorner.TopRight, 8, 8));
    }

    [Fact]
    public void Window_larger_than_monitor_is_clamped_to_left_and_top_edge()
    {
        Assert.Equal((0, 0), OverlayPlacement.Compute(Primary, 3000, 2000, OverlayCorner.BottomRight, 8, 8));
        var secondary = new PixelRect(-2560, 0, 0, 1440);
        Assert.Equal((-2560, 0), OverlayPlacement.Compute(secondary, 3000, 2000, OverlayCorner.BottomRight, 8, 8));
    }

    [Fact]
    public void Offsets_apply_from_chosen_corner() =>
        Assert.Equal((1680, 940), OverlayPlacement.Compute(Primary, 200, 40, OverlayCorner.BottomRight, 40, 100));

    [Fact]
    public void Offset_larger_than_monitor_is_clamped_inside() =>
        Assert.Equal((1920 - 200, 1080 - 40), OverlayPlacement.Compute(Primary, 200, 40, OverlayCorner.TopLeft, 3000, 3000));

    [Theory]
    [InlineData(100, 100, OverlayCorner.TopLeft, 100, 100)]
    [InlineData(1600, 100, OverlayCorner.TopRight, 120, 100)]
    [InlineData(100, 1000, OverlayCorner.BottomLeft, 100, 40)]
    [InlineData(1700, 1000, OverlayCorner.BottomRight, 20, 40)]
    public void From_window_picks_nearest_corner(int left, int top, OverlayCorner corner, int ox, int oy)
    {
        var r = OverlayPlacement.FromWindow(Primary, new PixelRect(left, top, left + 200, top + 40));
        Assert.Equal((corner, ox, oy), r);
    }

    [Fact]
    public void From_window_then_compute_round_trips()
    {
        var secondary = new PixelRect(-2560, 0, 0, 1440);
        var window = new PixelRect(-700, 1300, -500, 1340);
        var (corner, ox, oy) = OverlayPlacement.FromWindow(secondary, window);
        Assert.Equal(OverlayCorner.BottomRight, corner);
        Assert.Equal((window.Left, window.Top), OverlayPlacement.Compute(secondary, 200, 40, corner, ox, oy));
    }
}
