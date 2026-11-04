namespace FpsOverlay.Core.Tests;

public class Win32CommandLineTests
{
    [Fact]
    public void Exe_is_always_quoted_and_simple_arguments_are_not()
    {
        Assert.Equal("\"C:\\Program Files\\FpsOverlay\\Temp\\FpsOverlay_update.1.exe\" --update-after 1234",
            Win32CommandLine.Build(@"C:\Program Files\FpsOverlay\Temp\FpsOverlay_update.1.exe", ["--update-after", "1234"]));
        Assert.Equal("\"C:\\a.exe\"", Win32CommandLine.Build(@"C:\a.exe", []));
    }

    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData(@"C:\dir\", @"C:\dir\")]
    [InlineData(@"C:\my dir\", "\"C:\\my dir\\\\\"")]
    [InlineData("a\\\"b", "\"a\\\\\\\"b\"")]
    public void Arguments_follow_CommandLineToArgvW_rules(string argument, string expected) =>
        Assert.Equal(expected, Win32CommandLine.Quote(argument));

    [Fact]
    public void Environment_block_is_sorted_and_double_null_terminated()
    {
        var block = Win32CommandLine.EnvironmentBlock(new Dictionary<string, string?>
        {
            ["Path"] = @"C:\Windows",
            ["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = @"C:\Program Files\FpsOverlay\Temp\extract",
            ["alpha"] = "1",
            ["Skipped"] = null,
        });
        Assert.Equal("alpha=1\0DOTNET_BUNDLE_EXTRACT_BASE_DIR=C:\\Program Files\\FpsOverlay\\Temp\\extract\0Path=C:\\Windows\0\0", block);
    }

    [Fact]
    public void Empty_environment_block_is_two_nulls() =>
        Assert.Equal("\0\0", Win32CommandLine.EnvironmentBlock(new Dictionary<string, string?>()));
}
