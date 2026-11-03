namespace FpsOverlay.Core.Tests;

public class CommandLineTests
{
    [Fact]
    public void Parses_flags()
    {
        Assert.True(CommandLine.Parse(["--Autostart"]).Autostart);
        Assert.True(CommandLine.Parse(["--UNINSTALL"]).Uninstall);
        Assert.True(CommandLine.Parse(["--no-install"]).NoInstall);
        Assert.True(CommandLine.Parse(["--pawnio-offered"]).PawnIoOffered);
        Assert.Equal(1234, CommandLine.Parse(["--update-after", "1234"]).UpdateAfterPid);
        Assert.Null(CommandLine.Parse(["--update-after", "x"]).UpdateAfterPid);
        Assert.Null(CommandLine.Parse(["--update-after", "0"]).UpdateAfterPid);
        Assert.Null(CommandLine.Parse(["--update-after"]).UpdateAfterPid);
        Assert.Equal(new CommandLine(false, false, false, null, false), CommandLine.Parse([]));
    }
}
