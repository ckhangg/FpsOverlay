using System.Globalization;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Logging;

/// <summary>Gom số liệu cả buổi để viết file tóm tắt.</summary>
public sealed class SessionSummary
{
    readonly List<double> _frameTimes = [];
    double _frameTimeSum;
    double? _minFps, _maxFps, _maxGpuTemp, _maxCpuTemp;

    /// <summary>Số liệu của một giây: FPS của giây đó (null nếu không có khung) và cảm biến.</summary>
    public void AddSecond(double? fps, SensorSnapshot s)
    {
        if (fps is { } f)
        {
            _minFps = _minFps is { } min ? Math.Min(min, f) : f;
            _maxFps = _maxFps is { } max ? Math.Max(max, f) : f;
        }
        if (s.GpuTemp is { } gpu) _maxGpuTemp = _maxGpuTemp is { } g ? Math.Max(g, gpu) : gpu;
        if (s.CpuTemp is { } cpu) _maxCpuTemp = _maxCpuTemp is { } c ? Math.Max(c, cpu) : cpu;
    }

    public void AddFrameTimes(IReadOnlyList<double> frameTimes)
    {
        for (int i = 0; i < frameTimes.Count; i++)
        {
            _frameTimes.Add(frameTimes[i]);
            _frameTimeSum += frameTimes[i];
        }
    }

    public string Format(AppLanguage language, TimeSpan duration)
    {
        var inv = CultureInfo.InvariantCulture;
        string T(StringKey key) => Loc.Get(key, language);
        static string Num(double? v, string format) => v?.ToString(format, CultureInfo.InvariantCulture) ?? "--";
        string Temp(double? v) => v is { } t ? t.ToString("F0", inv) + "°C" : "--";

        // FPS trung bình = tổng khung / tổng thời gian khung (không phải trung bình các giây).
        double? average = _frameTimes.Count > 0 && _frameTimeSum > 0 ? _frameTimes.Count / _frameTimeSum : null;
        var time = $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";

        return string.Join(Environment.NewLine,
            $"{T(StringKey.SummaryDuration)}: {time}",
            $"{T(StringKey.SummaryAvgFps)}: {Num(average, "F1")}",
            $"{T(StringKey.SummaryOnePercentLow)}: {Num(FrameTimeHistory.OnePercentLowOf(_frameTimes), "F1")}",
            $"{T(StringKey.SummaryMinFps)}: {Num(_minFps, "F0")}",
            $"{T(StringKey.SummaryMaxFps)}: {Num(_maxFps, "F0")}",
            $"{T(StringKey.SummaryGpuTempMax)}: {Temp(_maxGpuTemp)}",
            $"{T(StringKey.SummaryCpuTempMax)}: {Temp(_maxCpuTemp)}") + Environment.NewLine;
    }
}