using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core;

/// <summary>Hình chữ nhật theo pixel vật lý (toạ độ màn hình ảo, có thể âm).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom);

/// <summary>Tính vị trí cửa sổ overlay trong một màn hình.</summary>
public static class OverlayPlacement
{
    /// <summary>Góc trên-trái của cửa sổ để nó nằm ở <paramref name="corner"/> của màn hình, cách mép <paramref name="marginPx"/>; kẹp để không ra ngoài màn hình.</summary>
    public static (int X, int Y) Compute(PixelRect monitor, int windowWidth, int windowHeight, OverlayCorner corner, int marginPx)
    {
        bool right = corner is OverlayCorner.TopRight or OverlayCorner.BottomRight;
        bool bottom = corner is OverlayCorner.BottomLeft or OverlayCorner.BottomRight;
        int x = right ? monitor.Right - marginPx - windowWidth : monitor.Left + marginPx;
        int y = bottom ? monitor.Bottom - marginPx - windowHeight : monitor.Top + marginPx;
        // Cửa sổ lớn hơn màn hình thì dính mép trái/trên.
        return (Math.Max(monitor.Left, x), Math.Max(monitor.Top, y));
    }
}
