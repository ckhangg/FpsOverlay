using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace FpsOverlay.Themes;

/// <summary>
/// Giãn chữ (như <c>letter-spacing</c> của CSS, WPF không có sẵn) bằng cách chèn khoảng trắng mảnh U+200A giữa các ký tự.
/// Chỉ dùng để vẽ trong template (vd. tiêu đề Neon); nội dung gốc của control giữ nguyên nên trình đọc màn hình vẫn đọc đúng.
/// Tách theo cụm ký tự (text element) nên chữ có dấu tổ hợp không bị tách khỏi dấu.
/// ConverterParameter: số khoảng mảnh chèn giữa hai ký tự (mặc định 1).
/// </summary>
public sealed class LetterSpacing : IValueConverter
{
    const char HairSpace = ' ';

    public static readonly LetterSpacing Converter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || text.Length < 2) return value;
        var count = parameter is string p && int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? Math.Clamp(n, 0, 4) : 1;
        if (count == 0) return text;

        var gap = new string(HairSpace, count);
        var result = new StringBuilder(text.Length * (count + 1));
        var elements = StringInfo.GetTextElementEnumerator(text.Normalize(NormalizationForm.FormC));
        var first = true;
        while (elements.MoveNext())
        {
            if (!first) result.Append(gap);
            result.Append(elements.GetTextElement());
            first = false;
        }
        return result.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
