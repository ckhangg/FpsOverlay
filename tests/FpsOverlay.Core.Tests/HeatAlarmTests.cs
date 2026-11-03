using FpsOverlay.Core;

namespace FpsOverlay.Core.Tests;

public class HeatAlarmTests
{
    private const int Limit = 85;
    private const int Hold = 10;

    [Fact]
    public void Hot_becomes_alarming_after_hold()
    {
        var a = new HeatAlarm();
        for (var t = 0; t < 10; t++)
        {
            Assert.False(a.Update(86, Limit, Hold, t));
            Assert.Equal(HeatState.Hot, a.State);
        }
        Assert.True(a.Update(86, Limit, Hold, 10));
        Assert.Equal(HeatState.Alarming, a.State);
        Assert.False(a.Update(86, Limit, Hold, 11));
        Assert.Equal(HeatState.Alarming, a.State);
    }

    [Fact]
    public void Zero_hold_alarms_immediately()
    {
        var a = new HeatAlarm();
        Assert.True(a.Update(86, Limit, 0, 0));
        Assert.Equal(HeatState.Alarming, a.State);
    }

    [Fact]
    public void Dip_below_threshold_restarts_hold()
    {
        var a = new HeatAlarm();
        for (var t = 0; t <= 5; t++) Assert.False(a.Update(86, Limit, Hold, t));
        Assert.False(a.Update(84, Limit, Hold, 6));
        Assert.Equal(HeatState.Normal, a.State);
        for (var t = 7; t <= 16; t++)
        {
            Assert.False(a.Update(86, Limit, Hold, t));
            Assert.NotEqual(HeatState.Alarming, a.State);
        }
        Assert.True(a.Update(86, Limit, Hold, 17));
        Assert.Equal(HeatState.Alarming, a.State);
    }

    [Fact]
    public void Hovering_near_threshold_does_not_restart_episode()
    {
        var a = new HeatAlarm();
        Assert.True(a.Update(86, Limit, 0, 0));
        double t = 1;
        foreach (var temp in new[] { 84.0, 83, 85, 82.6 })
        {
            Assert.False(a.Update(temp, Limit, 0, t++));
            Assert.Equal(HeatState.Alarming, a.State);
        }
    }

    [Fact]
    public void Episode_ends_below_threshold_minus_margin()
    {
        var a = new HeatAlarm();
        a.Update(86, Limit, 0, 0);
        Assert.False(a.Update(81.4, Limit, 0, 1));
        Assert.Equal(HeatState.Normal, a.State);
    }

    [Fact]
    public void Null_reading_resets()
    {
        var a = new HeatAlarm();
        a.Update(86, Limit, 0, 0);
        Assert.False(a.Update(null, Limit, 0, 1));
        Assert.Equal(HeatState.Normal, a.State);
    }

    [Fact]
    public void Reset_then_needs_full_hold_again()
    {
        var a = new HeatAlarm();
        for (var t = 0; t <= 10; t++) a.Update(86, Limit, Hold, t);
        Assert.Equal(HeatState.Alarming, a.State);
        a.Reset();
        Assert.Equal(HeatState.Normal, a.State);
        Assert.False(a.Update(86, Limit, Hold, 11));
        Assert.Equal(HeatState.Hot, a.State);
        Assert.False(a.Update(86, Limit, Hold, 20));
        Assert.True(a.Update(86, Limit, Hold, 21));
    }
}
