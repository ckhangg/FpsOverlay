using System.Diagnostics;
using System.IO;
using System.Windows;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>
/// Đổi chủ đề của các cửa sổ (cài đặt, cài app, ...) lúc chạy: mỗi phong cách overlay có một file
/// <c>Themes/&lt;Tên phong cách&gt;.xaml</c> định nghĩa cùng tập khoá <c>Ui.*</c>; cửa sổ gắn khoá bằng DynamicResource
/// nên chỉ cần thay đúng một merged dictionary trong <see cref="Application.Resources"/>.
/// Chỉ gọi trên luồng UI.
/// </summary>
static class ThemeManager
{
    const string Fallback = nameof(OverlayStyle.Cyberpunk);

    static OverlayStyle? _applied;
    static AppLanguage? _language;

    /// <summary>
    /// Nạp chủ đề của <paramref name="style"/> thay cho chủ đề đang dùng. Phong cách chưa có file chủ đề thì dùng Cyberpunk.
    /// Gọi lại với cùng phong cách thì không làm gì (cài đặt đổi rất thường, vd. kéo thanh trượt).
    /// </summary>
    public static void Apply(OverlayStyle style)
    {
        if (_applied == style) return;
        var resources = Application.Current.Resources;
        var theme = Load(style.ToString()) ?? Load(Fallback)!;

        var merged = resources.MergedDictionaries;
        var index = IndexOfTheme(merged);
        if (index < 0) merged.Add(theme);
        else merged[index] = theme;   // thay tại chỗ: giữ thứ tự merge, cửa sổ đang mở đổi ngay
        _applied = style;
    }

    /// <summary>Đặt chữ công tắc theo ngôn ngữ (BẬT/TẮT, ON/OFF) ở cấp Application, đè lên giá trị trong file chủ đề.</summary>
    public static void ApplyLanguage(AppLanguage language)
    {
        if (_language == language) return;
        var resources = Application.Current.Resources;
        var english = language == AppLanguage.English;
        resources["Ui.OnText"] = english ? "ON" : "BẬT";
        resources["Ui.OffText"] = english ? "OFF" : "TẮT";
        _language = language;
    }

    static ResourceDictionary? Load(string name)
    {
        try
        {
            return new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{name}.xaml", UriKind.Absolute) };
        }
        catch (IOException ex)
        {
            // Phong cách chưa có file chủ đề (resource không tồn tại): để người gọi dùng chủ đề mặc định.
            if (name != Fallback) Trace.TraceInformation($"Chưa có chủ đề {name}: {ex.Message}");
            else Trace.TraceWarning($"Không nạp được chủ đề {name}: {ex.Message}");
            return null;
        }
    }

    // Dictionary chủ đề là dictionary nạp từ thư mục Themes/ (App.xaml merge sẵn Cyberpunk).
    static int IndexOfTheme(System.Collections.ObjectModel.Collection<ResourceDictionary> merged)
    {
        for (int i = 0; i < merged.Count; i++)
            if (merged[i].Source?.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase) == true)
                return i;
        return -1;
    }
}
