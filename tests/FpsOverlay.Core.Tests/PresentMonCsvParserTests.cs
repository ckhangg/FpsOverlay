namespace FpsOverlay.Core.Tests;

public class PresentMonCsvParserTests
{
    const string Header = "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents";

    [Fact]
    public void Parses_frame_by_column_name()
    {
        var p = new PresentMonCsvParser();
        Assert.True(p.TryReadHeader(Header));
        Assert.True(p.TryParse("game.exe,1234,0x1,10.5,16.6", out var s));
        Assert.Equal(new FrameSample(1234, 10.5), s);
    }

    [Fact]
    public void Header_without_required_columns_is_rejected()
        => Assert.False(new PresentMonCsvParser().TryReadHeader("Application,Foo"));

    [Fact]
    public void Line_before_header_is_rejected()
        => Assert.False(new PresentMonCsvParser().TryParse("game.exe,1234,0x1,10.5,16.6", out _));

    [Theory]
    [InlineData("game.exe,1234")]            // cụt
    [InlineData("game.exe,NA,0x1,NA,NA")]    // NA
    [InlineData("")]
    public void Bad_lines_are_skipped(string line)
    {
        var p = new PresentMonCsvParser();
        p.TryReadHeader(Header);
        Assert.False(p.TryParse(line, out _));
    }

    [Fact]
    public async Task ReadFramesAsync_emits_valid_frames_only()
    {
        var csv = $"{Header}\na.exe,1,0x1,1.0,16\nbroken\na.exe,1,0x1,1.016,16\n";
        var frames = new List<FrameSample>();
        await PresentMonCsvParser.ReadFramesAsync(new StringReader(csv), frames.Add, default);
        Assert.Equal([new(1, 1.0), new(1, 1.016)], frames);
    }
}
