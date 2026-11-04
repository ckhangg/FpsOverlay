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
    /// Nạp chủ đề của <paramref name="style"/> thay cho chủ đề đang dùng. Phong cách chưa có file chủ đề (hoặc file nạp lỗi) thì dùng Cyberpunk.
    /// Gọi lại với cùng phong cách thì không làm gì (cài đặt đổi rất thường, vd. kéo thanh trượt).
    /// </summary>
    public static void Apply(OverlayStyle style)
    {
        if (_applied == style) return;
        var resources = Application.Current.Resources;
        var theme = Load(style.ToString());

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

    // Nạp file chủ đề của phong cách; thiếu file hay file hỏng (lỗi XAML, ...) thì ghi Trace và dùng Cyberpunk, không để
    // việc đổi phong cách làm sập app. Cyberpunk lỗi thì là lỗi build (App.xaml đã nạp nó từ đầu): để exception nổi lên.
    static ResourceDictionary Load(string name)
    {
        if (name != Fallback)
        {
            try
            {
                return LoadFile(name);
            }
            catch (IOException ex)
            {
                // Phong cách chưa có file chủ đề (resource không tồn tại).
                Trace.TraceInformation($"Chưa có chủ đề {name}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Không nạp được chủ đề {name}, dùng {Fallback}: {ex}");
            }
        }
        return LoadFile(Fallback);
    }

    static ResourceDictionary LoadFile(string name) =>
        new() { Source = new Uri($"pack://application:,,,/Themes/{name}.xaml", UriKind.Absolute) };

    // Dictionary chủ đề là dictionary nạp từ thư mục Themes/ (App.xaml merge sẵn Cyberpunk).
    static int IndexOfTheme(System.Collections.ObjectModel.Collection<ResourceDictionary> merged)
    {
        for (int i = 0; i < merged.Count; i++)
            if (merged[i].Source?.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase) == true)
                return i;
        return -1;
    }
}
