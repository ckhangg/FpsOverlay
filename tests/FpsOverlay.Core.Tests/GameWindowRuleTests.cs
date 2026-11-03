using FpsOverlay.Core.GameOnly;

namespace FpsOverlay.Core.Tests;

public class GameWindowRuleTests
{
    private const string SteamElden = @"D:\SteamLibrary\steamapps\common\ELDEN RING\Game\eldenring.exe";

    [Theory]
    [InlineData(@"D:\SteamLibrary\steamapps\common\ELDEN RING\Game\eldenring.exe", true)]
    [InlineData(@"C:\XboxGames\Forza Horizon 5\Content\ForzaHorizon5.exe", true)]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.WindowsNotepad_11.0\Notepad\Notepad.exe", false)]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", false)]
    public void Game_folders(string path, bool expected) =>
        Assert.Equal(expected, GameWindowRule.IsGameFolder(path));

    [Fact]
    public void Unknown_path_is_not_a_game_folder()
    {
        Assert.False(GameWindowRule.IsGameFolder(null));
        Assert.False(GameWindowRule.IsGameFolder(""));
    }

    [Fact]
    public void Covers_monitor_is_game() =>
        Assert.True(GameWindowRule.IsGame(true, [@"C:\x\vlc.exe"], AppNames.Empty, AppNames.Empty));

    [Fact]
    public void Always_list_makes_windowed_app_a_game() =>
        Assert.True(GameWindowRule.IsGame(false, [@"C:\Games\celeste.exe"], AppNames.From(["Celeste.exe"]), AppNames.Empty));

    [Fact]
    public void Never_list_beats_fullscreen_and_folder() =>
        Assert.False(GameWindowRule.IsGame(true, [SteamElden], AppNames.Empty, AppNames.From(["eldenring.exe"])));

    [Fact]
    public void Frame_host_candidates_are_all_checked() =>
        Assert.True(GameWindowRule.IsGame(false,
            [@"C:\Windows\System32\ApplicationFrameHost.exe", @"C:\XboxGames\G\Content\g.exe"],
            AppNames.Empty, AppNames.Empty));

    [Fact]
    public void Own_window_in_front_keeps_previous_decision()
    {
        Assert.True(GameWindowRule.ShouldShow(false, false, false, true, ForegroundKind.Own, false, previous: true));
        Assert.False(GameWindowRule.ShouldShow(false, false, false, true, ForegroundKind.Own, false, previous: false));
    }

    [Fact]
    public void Desktop_hides_when_game_only() =>
        Assert.False(GameWindowRule.ShouldShow(false, false, false, true, ForegroundKind.Desktop, false, true));

    [Fact]
    public void None_keeps_previous_when_game_only()
    {
        Assert.True(GameWindowRule.ShouldShow(false, false, false, true, ForegroundKind.None, false, previous: true));
        Assert.False(GameWindowRule.ShouldShow(false, false, false, true, ForegroundKind.None, false, previous: false));
    }

    [Fact]
    public void Other_follows_is_game()
    {
        Assert.True(GameWindowRule.ShouldShow(false, false, false, true, ForegroundKind.Other, true, false));
        Assert.False(GameWindowRule.ShouldShow(false, false, false, true, ForegroundKind.Other, false, true));
    }

    [Fact]
    public void Option_off_always_shows() =>
        Assert.True(GameWindowRule.ShouldShow(false, false, false, false, ForegroundKind.Desktop, false, false));

    [Fact]
    public void Manual_hide_beats_game() =>
        Assert.False(GameWindowRule.ShouldShow(true, true, true, false, ForegroundKind.Other, true, true));

    [Fact]
    public void Moving_shows_even_when_not_game() =>
        Assert.True(GameWindowRule.ShouldShow(false, true, false, true, ForegroundKind.Desktop, false, false));

    [Fact]
    public void Previewing_shows() =>
        Assert.True(GameWindowRule.ShouldShow(false, false, true, true, ForegroundKind.Desktop, false, false));
}
