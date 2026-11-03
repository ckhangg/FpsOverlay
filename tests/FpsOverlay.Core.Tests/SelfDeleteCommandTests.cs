using FpsOverlay.Core.Install;

namespace FpsOverlay.Core.Tests;

public class SelfDeleteCommandTests
{
    const string Sys = @"C:\Windows\System32";
    const string Dir = @"C:\Program Files\FpsOverlay";
    const string Key = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FpsOverlay";

    static string Build() => SelfDeleteCommand.Build(Sys, 4242, "FpsOverlay.exe", Dir, Key);

    [Fact]
    public void Waits_for_this_process_before_deleting()
    {
        var cmd = Build();
        var wait = cmd.IndexOf("\"PID eq 4242\"", StringComparison.Ordinal);
        Assert.True(wait >= 0);
        Assert.Contains("/i \"FpsOverlay.exe\"", cmd);
        Assert.True(wait < cmd.IndexOf("rmdir", StringComparison.Ordinal));
    }

    [Fact]
    public void Deletes_registry_key_only_after_directory_is_gone()
    {
        var cmd = Build();
        var check = cmd.IndexOf($"if not exist \"{Dir}\" (", StringComparison.Ordinal);
        var reg = cmd.IndexOf($"delete \"{Key}\" /f /reg:64", StringComparison.Ordinal);
        Assert.True(check >= 0 && reg > check);
        Assert.True(cmd.IndexOf("rmdir /s /q", StringComparison.Ordinal) < check);
    }

    [Fact]
    public void Calls_every_tool_by_full_system_path()
    {
        var cmd = Build();
        foreach (var tool in new[] { "PING.EXE", "tasklist.exe", "find.exe", "reg.exe" })
            Assert.Contains($"\"{Path.Combine(Sys, tool)}\"", cmd);
        Assert.DoesNotContain(" ping ", cmd, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retries_a_bounded_number_of_rounds()
    {
        Assert.StartsWith($"for /l %i in (1,1,{SelfDeleteCommand.MaxRounds}) do (", Build());
    }

    [Fact]
    public void Quotes_are_balanced()
    {
        Assert.Equal(0, Build().Count(c => c == '"') % 2);
    }
}
