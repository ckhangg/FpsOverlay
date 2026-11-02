using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FpsOverlay.Themes;

/// <summary>
/// Khung nền có góc vát chéo kiểu Cyberpunk (giống <c>clip-path: polygon(...)</c> trong mockup).
/// Vẽ nền và viền theo đa giác, cắt nội dung bên trong theo cùng hình đó.
/// </summary>
public sealed class ChamferBorder : Decorator
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
        nameof(BorderBrush), typeof(Brush), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
        nameof(BorderThickness), typeof(double), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Thickness), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty TopRightProperty = RegisterCut(nameof(TopRight));
    public static readonly DependencyProperty BottomRightProperty = RegisterCut(nameof(BottomRight));
    public static readonly DependencyProperty BottomLeftProperty = RegisterCut(nameof(BottomLeft));

    static DependencyProperty RegisterCut(string name) => DependencyProperty.Register(
        name, typeof(double), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Background { get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public Brush? BorderBrush { get => (Brush?)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public double BorderThickness { get => (double)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Độ dài cạnh vát ở góc trên phải (DIP).</summary>
    public double TopRight { get => (double)GetValue(TopRightProperty); set => SetValue(TopRightProperty, value); }

    /// <summary>Độ dài cạnh vát ở góc dưới phải (DIP).</summary>
    public double BottomRight { get => (double)GetValue(BottomRightProperty); set => SetValue(BottomRightProperty, value); }

    /// <summary>Độ dài cạnh vát ở góc dưới trái (DIP).</summary>
    public double BottomLeft { get => (double)GetValue(BottomLeftProperty); set => SetValue(BottomLeftProperty, value); }

    Size _clipSize;

    protected override Size MeasureOverride(Size constraint)
    {
        var inset = Inset();
        var child = Child;
        if (child is null)
            return new Size(inset.Left + inset.Right, inset.Top + inset.Bottom);
        child.Measure(new Size(
            Math.Max(0, constraint.Width - inset.Left - inset.Right),
            Math.Max(0, constraint.Height - inset.Top - inset.Bottom)));
        return new Size(child.DesiredSize.Width + inset.Left + inset.Right,
                        child.DesiredSize.Height + inset.Top + inset.Bottom);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var inset = Inset();
        Child?.Arrange(new Rect(inset.Left, inset.Top,
            Math.Max(0, arrangeSize.Width - inset.Left - inset.Right),
            Math.Max(0, arrangeSize.Height - inset.Top - inset.Bottom)));

        if (arrangeSize != _clipSize)
        {
            _clipSize = arrangeSize;
            Clip = Outline(new Rect(arrangeSize), 0);
        }
        return arrangeSize;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TopRightProperty || e.Property == BottomRightProperty || e.Property == BottomLeftProperty)
            _clipSize = default;   // dựng lại vùng cắt ở lần arrange sau
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0)
            return;
        var thickness = BorderThickness;
        var brush = BorderBrush;
        if (Background is { } background)
            dc.DrawGeometry(background, null, Outline(new Rect(size), 0));
        if (brush is not null && thickness > 0)
        {
            // Nét viền nằm trọn bên trong khung: lùi vào nửa độ dày.
            var pen = new Pen(brush, thickness) { LineJoin = PenLineJoin.Miter };
            pen.Freeze();
            dc.DrawGeometry(null, pen, Outline(new Rect(size), thickness / 2));
        }
    }

    Thickness Inset()
    {
        var p = Padding;
        var b = BorderThickness;
        return new Thickness(p.Left + b, p.Top + b, p.Right + b, p.Bottom + b);
    }

    Geometry Outline(Rect bounds, double inset)
    {
        bounds.Inflate(-inset, -inset);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return Geometry.Empty;
        // Bớt phần vát đi một lượng tương ứng để cạnh chéo của nét viền song song với vùng cắt.
        double shrink = inset * (2 - Math.Sqrt(2));
        double tr = Cut(TopRight), br = Cut(BottomRight), bl = Cut(BottomLeft);

        double Cut(double c) => c <= 0 ? 0 : Math.Min(Math.Max(0, c - shrink), Math.Min(bounds.Width, bounds.Height));

        double l = bounds.Left, t = bounds.Top, r = bounds.Right, b = bounds.Bottom;
        var figure = new PathFigure { StartPoint = new Point(l, t), IsClosed = true, IsFilled = true };
        var points = new List<Point> { new(r - tr, t) };
        if (tr > 0) points.Add(new Point(r, t + tr));
        points.Add(new Point(r, b - br));
        if (br > 0) points.Add(new Point(r - br, b));
        points.Add(new Point(l + bl, b));
        if (bl > 0) points.Add(new Point(l, b - bl));
        figure.Segments.Add(new PolyLineSegment(points, isStroked: true));
        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        return geometry;
    }
}
