namespace FpsOverlay.Core.Tests;

public class FrameTimeHistoryTests
{
    static double Feed(FrameTimeHistory h, int pid, double start, double interval, int count)
    {
        double t = start;
        for (int i = 0; i < count; i++)
        {
            h.Add(new(pid, t));
            t += interval;
        }
        return t; // thời điểm khung kế tiếp
    }

    [Fact]
    public void Steady_60fps_one_percent_low_is_60()
    {
        var h = new FrameTimeHistory();
        Feed(h, 1, 0, 1 / 60.0, 600);
        Assert.Equal(60, h.OnePercentLow(1)!.Value, 1);
    }

    [Fact]
    public void Few_stutters_pull_one_percent_low_down()
    {
        var h = new FrameTimeHistory();
        var t = Feed(h, 1, 0, 1 / 60.0, 594);
        Feed(h, 1, t - 1 / 60.0 + 0.1, 0.1, 6);
        Assert.Equal(10, h.OnePercentLow(1)!.Value, 1);
    }

    [Fact]
    public void Less_than_two_seconds_is_null()
    {
        var h = new FrameTimeHistory();
        Feed(h, 1, 0, 1 / 60.0, 100);
        Assert.Null(h.OnePercentLow(1));
    }

    [Fact]
    public void Old_frames_leave_the_window()
    {
        var h = new FrameTimeHistory();
        var t = Feed(h, 1, 0, 1 / 30.0, 150);   // 5 giây ở 30 fps
        Feed(h, 1, t, 1 / 120.0, 1200);          // rồi 10 giây ở 120 fps
        Assert.Equal(120, h.OnePercentLow(1)!.Value, 1);
    }

    [Fact]
    public void Other_process_frames_do_not_mix()
    {
        var h = new FrameTimeHistory();
        Feed(h, 2, 0, 0.1, 100);
        Feed(h, 1, 0, 1 / 60.0, 600);
        Assert.Equal(60, h.OnePercentLow(1)!.Value, 1);
    }

    [Fact]
    public void Candidates_use_first_with_data()
    {
        var h = new FrameTimeHistory();
        Feed(h, 1, 0, 1 / 60.0, 600);
        Assert.Equal(60, h.OnePercentLow([7, 1])!.Value, 1);
    }

    [Fact]
    public void Copy_frame_times_after_time()
    {
        var h = new FrameTimeHistory();
        Feed(h, 1, 0, 0.5, 9); // khung ở 0, 0.5, ... 4.0
        var list = new List<double>();
        Assert.Equal(4, h.CopyFrameTimes(1, 2.0, list));
        Assert.All(list, ft => Assert.Equal(0.5, ft, 6));
        Assert.Equal(4.0, h.LatestTimeSeconds, 6);
    }

    [Fact]
    public void Percentile_nearest_rank()
    {
        // n = 100: chỉ số ceil(0,99·100) − 1 = 98, nên cần 2 khung chậm nhất mới kéo 1% low xuống.
        Assert.Equal(1 / 0.1, FrameTimeHistory.OnePercentLowOf([.. Enumerable.Repeat(0.01, 98), 0.1, 0.1])!.Value, 3);
        Assert.Equal(1 / 0.01, FrameTimeHistory.OnePercentLowOf([.. Enumerable.Repeat(0.01, 99), 0.1])!.Value, 3);
    }

    [Fact]
    public void Percentile_of_empty_is_null() =>
        Assert.Null(FrameTimeHistory.OnePercentLowOf([]));
}
