using FpsOverlay.Core.Install;

namespace FpsOverlay.Core.Tests;

public class InstallPathsTests
{
    [Theory]
    [InlineData(@"C:\Program Files", @"C:\Program Files\FpsOverlay")]
    [InlineData(@"C:\Program Files\", @"C:\Program Files\FpsOverlay")]
    [InlineData(@"D:\Apps", @"D:\Apps\FpsOverlay")]
    public void Fully_qualified_program_files_gives_install_directory(string programFiles, string expected)
    {
        Assert.True(InstallPaths.TryGetInstallDirectory(programFiles, out var dir));
        Assert.Equal(expected, dir);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Program Files")]
    [InlineData(@"C:Program Files")]
    [InlineData(@"\Program Files")]
    [InlineData(@"C:\")]
    public void Empty_relative_or_drive_root_is_rejected(string? programFiles)
    {
        Assert.False(InstallPaths.TryGetInstallDirectory(programFiles, out var dir));
        Assert.Equal("", dir);
    }
}
