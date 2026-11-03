using System.Globalization;

namespace FpsOverlay.Core;

/// <summary>Định dạng số cảm biến cho overlay. Làm tròn nửa lên, văn hoá bất biến; không đọc được → "--".</summary>
public static class StatFormat
{
    private const string None = "--";

    // Dựng sẵn để cùng một giá trị luôn ra cùng một chuỗi (overlay so sánh theo tham chiếu, không cấp phát).
    private static readonly string[] Percents = Build(100, "%");
    private static readonly string[] Temperatures = Build(150, "°");

    public static string Percent(double? value) => Lookup(value, Percents, "%");

    public static string Temperature(double? value) => Lookup(value, Temperatures, "°");

    public static string Gigabytes(double? value)
    {
        if (value is not { } v || !double.IsFinite(v)) return None;
        return Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture) + "G";
    }

    public static string Megahertz(double? value, bool withUnit) => Whole(value, withUnit ? "MHz" : "");

    public static string Watts(double? value, bool withUnit) => Whole(value, withUnit ? "W" : "");

    public static string Rpm(double? value, bool withUnit) => Whole(value, withUnit ? "RPM" : "");

    private static string Whole(double? value, string suffix)
    {
        if (value is not { } v || !double.IsFinite(v)) return None;
        return Math.Round(v, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + suffix;
    }

    private static string Lookup(double? value, string[] table, string suffix)
    {
        if (value is not { } v || !double.IsFinite(v)) return None;
        var r = Math.Round(v, MidpointRounding.AwayFromZero);
        if (r >= 0 && r < table.Length) return table[(int)r];
        return r.ToString("0", CultureInfo.InvariantCulture) + suffix;   // ngoài khoảng dựng sẵn: hiếm, chấp nhận cấp phát
    }

    private static string[] Build(int max, string suffix)
    {
        var table = new string[max + 1];
        for (int i = 0; i <= max; i++) table[i] = i.ToString(CultureInfo.InvariantCulture) + suffix;
        return table;
    }
}

/// <summary>Quy tắc coi là nóng (hiện màu đỏ); ngưỡng do người dùng đặt trong cài đặt.</summary>
public static class HeatLevels
{
    /// <summary>So trên giá trị đã làm tròn như hiển thị (84,6 hiện "85°" thì đã nóng).</summary>
    public static bool IsHot(double? temp, double limit) =>
        temp is { } t && double.IsFinite(t) && Math.Round(t, MidpointRounding.AwayFromZero) >= limit;
}
