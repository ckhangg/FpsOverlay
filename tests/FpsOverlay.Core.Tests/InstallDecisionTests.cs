using FpsOverlay.Core.Install;

namespace FpsOverlay.Core.Tests;

// So hash của Payload.zip (bản nhiều file sẽ cài), không so hash exe: exe bản cài khác exe bản mang đi.
public class InstallDecisionTests
{
    static readonly Version V100 = new(1, 0, 0);
    static readonly Version V110 = new(1, 1, 0);

    [Fact]
    public void Not_installed_installs()
    {
        Assert.Equal(InstallAction.Install,
            InstallDecision.Decide(V100, runningPayloadHash: "aa", installed: null, installedPayloadHash: null));
    }

    [Fact]
    public void Newer_running_copy_updates()
    {
        Assert.Equal(InstallAction.Update,
            InstallDecision.Decide(V110, runningPayloadHash: "aa", installed: V100, installedPayloadHash: "aa"));
    }

    [Fact]
    public void Older_running_copy_runs_installed()
    {
        Assert.Equal(InstallAction.RunInstalled,
            InstallDecision.Decide(V100, runningPayloadHash: "aa", installed: V110, installedPayloadHash: "bb"));
    }

    [Fact]
    public void Same_version_different_payload_updates()
    {
        Assert.Equal(InstallAction.Update,
            InstallDecision.Decide(V100, runningPayloadHash: "aa", installed: V100, installedPayloadHash: "bb"));
    }

    [Fact]
    public void Same_version_same_payload_ignoring_case_runs_installed()
    {
        Assert.Equal(InstallAction.RunInstalled,
            InstallDecision.Decide(V100, runningPayloadHash: "abcdef", installed: V100, installedPayloadHash: "ABCDEF"));
    }

    [Fact]
    public void Same_version_without_payload_marker_updates()
    {
        // Bản cài cũ kiểu single-file không có payload.sha256 (hoặc không đọc được): coi như khác.
        Assert.Equal(InstallAction.Update,
            InstallDecision.Decide(V100, runningPayloadHash: "aa", installed: V100, installedPayloadHash: null));
    }

    [Fact]
    public void Same_version_empty_payload_marker_updates()
    {
        Assert.Equal(InstallAction.Update,
            InstallDecision.Decide(V100, runningPayloadHash: "aa", installed: V100, installedPayloadHash: ""));
    }
}
