using FpsOverlay.Core.Logging;
using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public class SessionSummaryTests
{
    static SensorSnapshot Temps(double? gpu, double? cpu) => new(null, gpu, null, cpu, null);

    static SessionSummary Filled()
    {
        var s = new SessionSummary();
        s.AddSecond(100, Temps(70, 60));
        s.AddSecond(60, Temps(78, null));
        s.AddSecond(null, Temps(75, 66));
        // 98 khung 10 ms + 2 khung 100 ms: trung bình 100 / 1.18 s, 1% low = 1 / 0.1 s
        var frameTimes = Enumerable.Repeat(0.01, 98).Concat([0.1, 0.1]).ToList();
        s.AddFrameTimes(frameTimes);
        return s;
    }

    [Fact]
    public void Vietnamese_summary_has_every_line()
    {
        var text = Filled().Format(AppLanguage.Vietnamese, new TimeSpan(1, 2, 3));
        Assert.Contains("Thời lượng: 01:02:03", text);
        Assert.Contains("FPS trung bình: 84.7", text);
        Assert.Contains("1% low cả buổi: 10.0", text);
        Assert.Contains("FPS thấp nhất: 60", text);
        Assert.Contains("FPS cao nhất: 100", text);
        Assert.Contains("Nhiệt độ GPU cao nhất: 78°C", text);
        Assert.Contains("Nhiệt độ CPU cao nhất: 66°C", text);
    }

    [Fact]
    public void English_summary_has_every_line()
    {
        var text = Filled().Format(AppLanguage.English, TimeSpan.FromSeconds(75));
        Assert.Contains("Duration: 00:01:15", text);
        Assert.Contains("Average FPS: 84.7", text);
        Assert.Contains("1% low (session): 10.0", text);
        Assert.Contains("Lowest FPS: 60", text);
        Assert.Contains("Highest FPS: 100", text);
        Assert.Contains("Peak GPU temp: 78°C", text);
        Assert.Contains("Peak CPU temp: 66°C", text);
    }

    [Fact]
    public void Duration_over_a_day_counts_hours()
    {
        Assert.Contains("Duration: 25:00:00", new SessionSummary().Format(AppLanguage.English, TimeSpan.FromHours(25)));
    }

    [Fact]
    public void No_data_shows_dashes()
    {
        var text = new SessionSummary().Format(AppLanguage.English, TimeSpan.Zero);
        Assert.Contains("Average FPS: --", text);
        Assert.Contains("1% low (session): --", text);
        Assert.Contains("Lowest FPS: --", text);
        Assert.Contains("Highest FPS: --", text);
        Assert.Contains("Peak GPU temp: --", text);
        Assert.Contains("Peak CPU temp: --", text);
    }
}