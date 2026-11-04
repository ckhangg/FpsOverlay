using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FpsOverlay.Core;

namespace FpsOverlay.Themes;

/// <summary>
/// Thanh trượt dạng chữ của chủ đề Terminal: MultiBinding (Value, Minimum, Maximum) → phần ô <c>######----</c> của
/// <see cref="AsciiBar"/>, bỏ hai ngoặc (template vẽ ngoặc riêng để vùng bấm khớp đúng các ô).
/// </summary>
public sealed class AsciiBarConverter : IMultiValueConverter
{
    public static readonly AsciiBarConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var bar = values is [double value, double min, double max] ? AsciiBar.Render(value, min, max) : AsciiBar.Render(0, 0, 0);
        return bar[1..^1];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Kéo thanh trượt theo vị trí chuột trên một phần tử trong template (thanh dạng chữ không có núm để kéo):
/// bấm đặt giá trị theo vị trí, giữ chuột kéo tiếp thì giá trị chạy theo, kể cả khi ra ngoài dải (kẹp ở hai đầu).
/// Đặt <c>TextSlider.Area</c> = tên phần tử trong template của Slider (vd. "Cells"). Phím mũi tên vẫn do Slider xử lý.
/// </summary>
public static class TextSlider
{
    public static readonly DependencyProperty AreaProperty = DependencyProperty.RegisterAttached(
        "Area", typeof(string), typeof(TextSlider), new PropertyMetadata(null, OnAreaChanged));

    public static string? GetArea(Slider slider) => (string?)slider.GetValue(AreaProperty);
    public static void SetArea(Slider slider, string? value) => slider.SetValue(AreaProperty, value);

    static void OnAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider) return;
        // Gỡ trước rồi gắn lại: đổi chủ đề đặt lại style nhiều lần không gắn trùng.
        slider.PreviewMouseLeftButtonDown -= OnDown;
        slider.PreviewMouseMove -= OnMove;
        slider.PreviewMouseLeftButtonUp -= OnUp;
        slider.LostMouseCapture -= OnLostCapture;
        if (e.NewValue is not string) return;
        slider.PreviewMouseLeftButtonDown += OnDown;
        slider.PreviewMouseMove += OnMove;
        slider.PreviewMouseLeftButtonUp += OnUp;
        slider.LostMouseCapture += OnLostCapture;
    }

    // Đang kéo: giữ chuột trên Slider (CaptureMouse) để kéo ra ngoài dải vẫn nhận được.
    static readonly DependencyProperty DraggingProperty =
        DependencyProperty.RegisterAttached("Dragging", typeof(bool), typeof(TextSlider), new PropertyMetadata(false));

    static FrameworkElement? AreaOf(Slider slider) =>
        GetArea(slider) is { } name ? slider.Template?.FindName(name, slider) as FrameworkElement : null;

    static void OnDown(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (AreaOf(slider) is not { } area || !area.IsMouseOver) return;
        slider.Focus();
        if (!slider.CaptureMouse()) return;
        slider.SetValue(DraggingProperty, true);
        SetFromPoint(slider, area, e);
        e.Handled = true;
    }

    static void OnMove(object sender, MouseEventArgs e)
    {
        var slider = (Slider)sender;
        if (!(bool)slider.GetValue(DraggingProperty) || AreaOf(slider) is not { } area) return;
        SetFromPoint(slider, area, e);
        e.Handled = true;
    }

    static void OnUp(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (!(bool)slider.GetValue(DraggingProperty)) return;
        slider.ReleaseMouseCapture();   // → OnLostCapture
        e.Handled = true;
    }

    static void OnLostCapture(object sender, MouseEventArgs e) => ((Slider)sender).SetValue(DraggingProperty, false);

    // Vị trí ngang trên dải → giá trị, làm tròn theo bước TickFrequency khi IsSnapToTickEnabled (như Slider gốc).
    static void SetFromPoint(Slider slider, FrameworkElement area, MouseEventArgs e)
    {
        if (area.ActualWidth <= 0) return;
        var fraction = Math.Clamp(e.GetPosition(area).X / area.ActualWidth, 0, 1);
        var value = slider.Minimum + fraction * (slider.Maximum - slider.Minimum);
        if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
            value = slider.Minimum + Math.Round((value - slider.Minimum) / slider.TickFrequency) * slider.TickFrequency;
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
    }
}
