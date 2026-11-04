using FpsOverlay.Core.Update;

namespace FpsOverlay.Core.Tests;

public class UpdateHandoverTests
{
    const string Installed = @"C:\Program Files\FpsOverlay\FpsOverlay.exe";

    [Fact]
    public void Waits_only_for_installed_fps_overlay()
    {
        Assert.True(UpdateHandover.IsOldInstance("FpsOverlay", Installed, Installed));
        Assert.True(UpdateHandover.IsOldInstance("fpsoverlay", @"c:\program files\fpsoverlay\FPSOVERLAY.EXE", Installed));
        Assert.False(UpdateHandover.IsOldInstance("notepad", Installed, Installed));
        Assert.False(UpdateHandover.IsOldInstance("FpsOverlay", @"C:\Users\x\Downloads\FpsOverlay.exe", Installed));
        Assert.False(UpdateHandover.IsOldInstance("FpsOverlay", null, Installed));
        Assert.False(UpdateHandover.IsOldInstance(null, Installed, Installed));
        Assert.False(UpdateHandover.IsOldInstance("FpsOverlay", "", ""));
    }

    [Fact]
    public void Stale_temp_exe_is_older_than_one_hour()
    {
        var now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateHandover.IsStaleTempExe(@"C:\x\Temp\FpsOverlay_update.abc.exe", now.AddHours(-2), now));
        Assert.True(UpdateHandover.IsStaleTempExe(@"C:\x\Temp\PawnIO_setup.abc.EXE", now.AddMinutes(-61), now));
        Assert.False(UpdateHandover.IsStaleTempExe(@"C:\x\Temp\FpsOverlay_update.abc.exe", now.AddMinutes(-30), now));
        Assert.False(UpdateHandover.IsStaleTempExe(@"C:\x\Temp\notes.txt", now.AddDays(-3), now));
    }
}
