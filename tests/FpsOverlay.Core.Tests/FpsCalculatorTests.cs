namespace FpsOverlay.Core.Tests;

public class FpsCalculatorTests
{
    static void Feed(FpsCalculator c, int pid, double start, double interval, int count)
    {
        for (int i = 0; i < count; i++)
            c.Add(new(pid, start + i * interval));
    }

    [Fact]
    public void Steady_60fps()
    {
        var c = new FpsCalculator();
        Feed(c, 1, 0, 1 / 60.0, 60);
        Assert.Equal(60, c.GetFps(1)!.Value, 1);
    }

    [Fact]
    public void Only_counts_requested_process()
    {
        var c = new FpsCalculator();
        Feed(c, 1, 0, 1 / 60.0, 60);
        Feed(c, 2, 0, 1 / 144.0, 144);
        Assert.Equal(60, c.GetFps(1)!.Value, 1);
        Assert.Equal(144, c.GetFps(2)!.Value, 1);
    }

    [Fact]
    public void Unknown_process_is_null() => Assert.Null(new FpsCalculator().GetFps(99));

    [Fact]
    public void Process_that_stopped_presenting_becomes_null()   // Review Focus 4
    {
        var c = new FpsCalculator();
        Feed(c, 1, 0, 1 / 60.0, 60);       // game tới t≈1.0
        Feed(c, 2, 1.0, 1 / 60.0, 60);     // desktop/app khác tới t≈2.0
        Assert.Null(c.GetFps(1));
    }

    [Fact]
    public void Stale_frames_become_null_when_nothing_presents_anymore()   // game treo / alt-tab, không còn ai vẽ
    {
        double clock = 100;
        var c = new FpsCalculator(() => clock);
        Feed(c, 1, 0, 1 / 60.0, 60);
        clock += 3.0;   // 3 giây trôi qua, không có khung hình mới từ bất kỳ tiến trình nào
        Assert.Null(c.GetFps(1));
    }

    [Fact]
    public void Gap_between_PresentMon_batches_does_not_blank_the_number()
    {
        // Đo thực tế: PresentMon nhận dữ liệu từ Windows theo đợt, cách nhau tới ~1,07 giây.
        double clock = 100;
        var c = new FpsCalculator(() => clock);
        Feed(c, 1, 0, 1 / 60.0, 60);
        clock += 1.1;
        Assert.Equal(60, c.GetFps(1)!.Value, 1);
    }

    [Fact]
    public void Short_pipe_delay_does_not_blank_the_number()
    {
        double clock = 100;
        var c = new FpsCalculator(() => clock);
        Feed(c, 1, 0, 1 / 60.0, 60);
        clock += 0.1;   // trễ nhỏ do PresentMon/ống dẫn dữ liệu
        Assert.Equal(60, c.GetFps(1)!.Value, 1);
    }

    [Fact]
    public void Candidates_use_first_process_that_presents()
    {
        // Game Store cũ: cửa sổ phía trước thuộc ApplicationFrameHost (pid 1), game thật (pid 2) vẽ bên trong.
        var c = new FpsCalculator();
        Feed(c, 2, 0, 1 / 90.0, 90);
        Assert.Equal(90, c.GetFps([1, 2])!.Value, 1);
    }

    [Fact]
    public void Candidates_prefer_the_foreground_process()
    {
        var c = new FpsCalculator();
        Feed(c, 1, 0, 1 / 60.0, 60);
        Feed(c, 2, 0, 1 / 144.0, 144);
        Assert.Equal(60, c.GetFps([1, 2])!.Value, 1);
    }

    [Fact]
    public void No_candidates_is_null() => Assert.Null(new FpsCalculator().GetFps([]));

    [Fact]
    public void Format_values()
    {
        Assert.Equal("--", FpsCalculator.Format(null));
        Assert.Equal("144", FpsCalculator.Format(143.6));
        Assert.Equal("0", FpsCalculator.Format(0.2));
        Assert.Equal("1234", FpsCalculator.Format(1234.4));
    }

    [Theory]
    [InlineData(59.5, "60", FpsLevel.Good)]
    [InlineData(29.5, "30", FpsLevel.Medium)]
    [InlineData(59.49, "59", FpsLevel.Medium)]
    public void Format_rounds_like_level_classification(double fps, string text, FpsLevel level)
    {
        // Số hiện ra và màu của nó phải khớp nhau ở đúng mốc (59,5 hiện "60" thì phải là màu tốt).
        Assert.Equal(text, FpsCalculator.Format(fps));
        Assert.Equal(level, FpsLevels.Classify(fps, 60, 30));
    }

    [Fact]
    public void Format_does_not_allocate_for_common_values() =>
        Assert.Same(FpsCalculator.Format(144.2), FpsCalculator.Format(143.8));

    [Fact] public void Seconds_since_any_frame_null_before_first_frame() => Assert.Null(new FpsCalculator().SecondsSinceAnyFrame());

    [Fact]
    public void Seconds_since_any_frame_uses_clock()
    {
        double clock = 100;
        var c = new FpsCalculator(() => clock);
        c.Add(new(1, 0));
        clock += 3;
        Assert.Equal(3, c.SecondsSinceAnyFrame()!.Value, 3);
    }
}
