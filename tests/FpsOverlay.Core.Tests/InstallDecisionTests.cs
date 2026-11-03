using FpsOverlay.Core.Install;

namespace FpsOverlay.Core.Tests;

public class InstallDecisionTests
{
    static readonly Version V100 = new(1, 0, 0);
    static readonly Version V110 = new(1, 1, 0);

    [Fact]
    public void Not_installed_installs()
    {
        Assert.Equal(InstallAction.Install, InstallDecision.Decide(V100, "aa", null, null));
    }

    [Fact]
    public void Newer_running_copy_updates()
    {
        Assert.Equal(InstallAction.Update, InstallDecision.Decide(V110, "aa", V100, "aa"));
    }

    [Fact]
    public void Older_running_copy_runs_installed()
    {
        Assert.Equal(InstallAction.RunInstalled, InstallDecision.Decide(V100, "aa", V110, "bb"));
    }

    [Fact]
    public void Same_version_different_hash_updates()
    {
        Assert.Equal(InstallAction.Update, InstallDecision.Decide(V100, "aa", V100, "bb"));
    }

    [Fact]
    public void Same_version_same_hash_ignoring_case_runs_installed()
    {
        Assert.Equal(InstallAction.RunInstalled, InstallDecision.Decide(V100, "abcdef", V100, "ABCDEF"));
    }

    [Fact]
    public void Same_version_unreadable_installed_hash_updates()
    {
        Assert.Equal(InstallAction.Update, InstallDecision.Decide(V100, "aa", V100, null));
    }
}
