using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core;

/// <summary>Hình chữ nhật theo pixel vật lý (toạ độ màn hình ảo, có thể âm).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom);

/// <summary>Tính vị trí cửa sổ overlay trong một màn hình.</summary>
public static class OverlayPlacement
{
    /// <summary>Góc trên-trái của cửa sổ để nó nằm ở <paramref name="corner"/> của màn hình, cách mép ngang/dọc tương ứng <paramref name="offsetXPx"/>/<paramref name="offsetYPx"/>; kẹp để nằm trọn trong màn hình.</summary>
    public static (int X, int Y) Compute(PixelRect monitor, int windowWidth, int windowHeight, OverlayCorner corner, int offsetXPx, int offsetYPx)
    {
        bool right = corner is OverlayCorner.TopRight or OverlayCorner.BottomRight;
        bool bottom = corner is OverlayCorner.BottomLeft or OverlayCorner.BottomRight;
        int x = right ? monitor.Right - offsetXPx - windowWidth : monitor.Left + offsetXPx;
        int y = bottom ? monitor.Bottom - offsetYPx - windowHeight : monitor.Top + offsetYPx;
        // Kẹp mép phải/dưới trước, rồi mép trái/trên: cửa sổ lớn hơn màn hình thì dính mép trái/trên.
        x = Math.Max(monitor.Left, Math.Min(x, monitor.Right - windowWidth));
        y = Math.Max(monitor.Top, Math.Min(y, monitor.Bottom - windowHeight));
        return (x, y);
    }

    /// <summary>Góc gần nhất (theo tâm cửa sổ so với tâm màn hình) và khoảng cách từ mép tương ứng tới cửa sổ (≥ 0).</summary>
    public static (OverlayCorner Corner, int OffsetXPx, int OffsetYPx) FromWindow(PixelRect monitor, PixelRect window)
    {
        // Nhân đôi để so sánh tâm bằng số nguyên.
        bool right = window.Left + window.Right >= monitor.Left + monitor.Right;
        bool bottom = window.Top + window.Bottom >= monitor.Top + monitor.Bottom;
        var corner = (right, bottom) switch
        {
            (false, false) => OverlayCorner.TopLeft,
            (true, false) => OverlayCorner.TopRight,
            (false, true) => OverlayCorner.BottomLeft,
            _ => OverlayCorner.BottomRight,
        };
        int ox = right ? monitor.Right - window.Right : window.Left - monitor.Left;
        int oy = bottom ? monitor.Bottom - window.Bottom : window.Top - monitor.Top;
        return (corner, Math.Max(0, ox), Math.Max(0, oy));
    }
}
