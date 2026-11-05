using System.Windows.Media.Imaging;
using System.Windows.Forms;
using Icon = System.Drawing.Icon;

namespace FpsOverlay;

/// <summary>Icon ứng dụng (Assets/EzFPS.ico, nhúng làm WPF Resource): một nguồn cho khay và cửa sổ.</summary>
static class AppIcon
{
    static readonly Uri IconUri = new("pack://application:,,,/Assets/EzFPS.ico", UriKind.Absolute);

    static readonly Lazy<BitmapSource> Window = new(() =>
    {
        // Decoder chọn khung lớn nhất cho BitmapSource; cửa sổ/taskbar tự co lại.
        var decoder = new IconBitmapDecoder(IconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var best = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        best.Freeze();
        return best;
    });

    /// <summary>Icon cửa sổ (đã Freeze).</summary>
    public static BitmapSource WindowIcon => Window.Value;

    /// <summary>Icon khay đúng cỡ SmallIconSize của hệ thống (nét hơn co từ 256); người gọi phải Dispose.</summary>
    public static Icon CreateTrayIcon()
    {
        var info = System.Windows.Application.GetResourceStream(IconUri)
            ?? throw new InvalidOperationException("Thiếu tài nguyên icon: " + IconUri);
        using var stream = info.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
