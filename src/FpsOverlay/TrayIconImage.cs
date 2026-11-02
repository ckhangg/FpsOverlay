using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace FpsOverlay;

/// <summary>Biểu tượng khay vẽ lúc chạy: chữ "FPS" vàng neon trên nền đen.</summary>
static class TrayIconImage
{
    const int Size = 32;
    static readonly Color Yellow = Color.FromArgb(0xFC, 0xEE, 0x0A);

    /// <summary>Tạo icon mới; người gọi phải Dispose.</summary>
    public static Icon Create()
    {
        using var bitmap = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Black);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            // Chữ to nhất còn vừa bề ngang (để lề 1px mỗi bên).
            var fontSize = 18f;
            var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            while (fontSize > 8 && g.MeasureString("FPS", font, int.MaxValue, format).Width > Size - 2)
            {
                font.Dispose();
                fontSize -= 0.5f;
                font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            using (font)
            using (var brush = new SolidBrush(Yellow))
                g.DrawString("FPS", font, brush, new RectangleF(0, 0, Size, Size), format);
        }

        // GetHicon tạo handle GDI mà Icon.FromHandle không sở hữu: sao ra icon tự giữ handle riêng rồi huỷ handle gốc.
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
