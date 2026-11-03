namespace FpsOverlay.Core.Tests;

public class AppVersionTests
{
    [Fact] public void Strips_commit_suffix() => Assert.Equal("v1.3.0", AppVersion.Display("1.3.0+7f042db0123"));
    [Fact] public void Plain_version() => Assert.Equal("v1.3.0", AppVersion.Display("1.3.0"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Missing_version_shows_question_mark(string? v) => Assert.Equal("v?", AppVersion.Display(v));
}
