using System.Globalization;
using System.Text;

namespace FpsOverlay.Core.Logging;

/// <summary>Định dạng file log CSV của một buổi chơi. Luôn dùng dấu chấm thập phân, không theo văn hoá máy.</summary>
public static class SessionLog
{
    public const string Header = "Time,Fps,OnePercentLow,GpuLoad,GpuTemp,CpuLoad,CpuTemp,RamUsedGb,GpuMemUsedGb,GpuClockMhz,GpuPowerW,GpuFanRpm";

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Một dòng mỗi giây; giá trị thiếu thì ô trống.</summary>
    public static string FormatRow(DateTimeOffset time, double? fps, double? onePercentLow, SensorSnapshot s) =>
        string.Join(',',
            time.ToString("yyyy-MM-ddTHH:mm:sszzz", Inv),
            Cell(fps, "F1"), Cell(onePercentLow, "F1"),
            Cell(s.GpuLoad, "F0"), Cell(s.GpuTemp, "F0"),
            Cell(s.CpuLoad, "F0"), Cell(s.CpuTemp, "F0"),
            Cell(s.RamUsedGb, "F2"),
            Cell(s.GpuMemoryUsedGb, "F2"), Cell(s.GpuClockMhz, "F0"), Cell(s.GpuPowerW, "F0"), Cell(s.GpuFanRpm, "F0"));

    static string Cell(double? value, string format) => value?.ToString(format, Inv) ?? "";

    /// <summary>
    /// FPS của một giây log = số khung mới / thời gian trôi qua. Không có khung nào thì trống (không tính là 0 FPS);
    /// game không ở phía trước (<paramref name="gameInFront"/> = false, vd. alt-tab) cũng trống dù game vẫn vẽ ở nền.
    /// </summary>
    public static double? SecondFps(int frames, double elapsedSeconds, bool gameInFront) =>
        gameInFront && frames > 0 && elapsedSeconds > 0 ? frames / elapsedSeconds : null;

    /// <summary>Tên file không đuôi: <c>&lt;tên game&gt;_yyyy-MM-dd_HH-mm</c>; ký tự cấm và khoảng trắng thành "_", tên rỗng thành "Game".</summary>
    public static string FileStem(string processName, DateTime start)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new StringBuilder(processName.Length);
        foreach (var ch in processName)
            name.Append(char.IsWhiteSpace(ch) || Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        if (name.Length == 0) name.Append("Game");
        return $"{name}_{start.ToString("yyyy-MM-dd_HH-mm", Inv)}";
    }

    /// <summary>Đường dẫn chưa có file: thêm <c>_2</c>, <c>_3</c>... vào tên khi trùng. <paramref name="extension"/> gồm cả dấu chấm.</summary>
    public static string UniquePath(string directory, string stem, string extension, Func<string, bool> exists)
    {
        var path = Path.Combine(directory, stem + extension);
        for (int n = 2; exists(path); n++)
            path = Path.Combine(directory, $"{stem}_{n}{extension}");
        return path;
    }

    /// <summary>File tóm tắt nằm cạnh file CSV: <c>&lt;cùng tên&gt;_summary.txt</c>.</summary>
    public static string SummaryPath(string csvPath) =>
        Path.Combine(Path.GetDirectoryName(csvPath) ?? "", Path.GetFileNameWithoutExtension(csvPath) + "_summary.txt");}