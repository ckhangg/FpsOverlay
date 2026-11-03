using FpsOverlay.Core.Logging;

namespace FpsOverlay.Core.Tests;

public class GameSessionTrackerTests
{
    // Một tick mỗi giây; mặc định là game 1 đang chạy toàn màn hình và có FPS.
    static IReadOnlyList<SessionChange> Tick(GameSessionTracker t, double now, int? pid = 1, bool covers = true,
        bool hasFps = true, bool alive = true, string? name = "game") =>
        t.Tick(new SessionInput(now, pid, name, covers, hasFps, alive));

    static IReadOnlyList<SessionChange> Away(GameSessionTracker t, double now) =>
        Tick(t, now, pid: null, covers: false, hasFps: false);

    // Đưa tracker tới lúc buổi của game 1 bắt đầu (giây 11).
    static GameSessionTracker Started()
    {
        var t = new GameSessionTracker();
        for (int s = 0; s <= 10; s++) Tick(t, s);
        var changes = Tick(t, 11);
        Assert.Equal(SessionChangeKind.Started, Assert.Single(changes).Kind);
        return t;
    }

    [Fact]
    public void Starts_after_ten_seconds_of_fullscreen_fps()
    {
        var t = new GameSessionTracker();
        for (int s = 0; s <= 10; s++)
        {
            Assert.Empty(Tick(t, s));
            Assert.Null(t.Current);
        }
        var change = Assert.Single(Tick(t, 11));
        Assert.Equal(SessionChangeKind.Started, change.Kind);
        Assert.Equal(new GameSession(1, "game", 11), change.Session);
        Assert.Equal(change.Session, t.Current);
    }

    [Fact]
    public void Window_not_covering_monitor_never_starts()
    {
        var t = new GameSessionTracker();
        for (int s = 0; s <= 120; s++)
            Assert.Empty(Tick(t, s, covers: false));
        Assert.Null(t.Current);
    }

    [Fact]
    public void Losing_fps_before_ten_seconds_resets_the_count()
    {
        var t = new GameSessionTracker();
        for (int s = 0; s <= 5; s++) Tick(t, s);
        Tick(t, 6, hasFps: false);
        for (int s = 7; s <= 17; s++) Assert.Empty(Tick(t, s));   // không reset thì đã bắt đầu ở giây 11
        Assert.Equal(SessionChangeKind.Started, Assert.Single(Tick(t, 18)).Kind);
    }

    [Fact]
    public void Alt_tab_under_sixty_seconds_keeps_session()
    {
        var t = Started();
        for (int s = 12; s <= 69; s++) Assert.Empty(Away(t, s));
        Assert.Empty(Tick(t, 70));
        Assert.NotNull(t.Current);
    }

    [Fact]
    public void Away_over_sixty_seconds_ends_session()
    {
        var t = Started();
        for (int s = 12; s <= 71; s++) Assert.Empty(Away(t, s));
        var change = Assert.Single(Away(t, 72));
        Assert.Equal(SessionChangeKind.Ended, change.Kind);
        Assert.Equal(1, change.Session.ProcessId);
        Assert.Null(t.Current);
    }

    [Fact]
    public void Process_exit_ends_session_immediately()
    {
        var t = Started();
        var change = Assert.Single(Tick(t, 12, pid: null, covers: false, hasFps: false, alive: false));
        Assert.Equal(SessionChangeKind.Ended, change.Kind);
        Assert.Null(t.Current);
    }

    [Fact]
    public void Other_game_qualifying_during_grace_replaces_session()
    {
        var t = Started();
        for (int s = 12; s <= 20; s++) Away(t, s);
        for (int s = 21; s <= 31; s++) Assert.Empty(Tick(t, s, pid: 2, name: "other"));
        var changes = Tick(t, 32, pid: 2, name: "other");
        Assert.Equal(2, changes.Count);
        Assert.Equal(SessionChangeKind.Ended, changes[0].Kind);
        Assert.Equal(1, changes[0].Session.ProcessId);
        Assert.Equal(SessionChangeKind.Started, changes[1].Kind);
        Assert.Equal(2, changes[1].Session.ProcessId);
        Assert.Equal(2, t.Current!.ProcessId);
    }
}