using FpsOverlay.Core.Update;

namespace FpsOverlay.Core.Tests;

public class UpdatePolicyTests
{
    static ReleaseInfo Rel(string v) =>
        new(Version.Parse(v), "v" + v, "", "https://x/p", "https://x/e", "https://x/h");

    [Fact]
    public void Offers_only_newer()
    {
        var cur = new Version(1, 4, 0);
        Assert.True(UpdatePolicy.ShouldOffer(cur, Rel("1.4.1"), null, false));
        Assert.False(UpdatePolicy.ShouldOffer(cur, Rel("1.4.0"), null, false));
        Assert.False(UpdatePolicy.ShouldOffer(cur, Rel("1.3.9"), null, false));
        Assert.False(UpdatePolicy.ShouldOffer(cur, null, null, true));
    }

    [Fact]
    public void Skipped_version_suppressed_only_when_automatic()
    {
        var cur = new Version(1, 4, 0);
        Assert.False(UpdatePolicy.ShouldOffer(cur, Rel("1.4.1"), "1.4.1", manual: false));
        Assert.True(UpdatePolicy.ShouldOffer(cur, Rel("1.4.1"), "1.4.1", manual: true));
        Assert.True(UpdatePolicy.ShouldOffer(cur, Rel("1.4.2"), "1.4.1", manual: false));
    }

    [Fact]
    public void Sha256_file_parsing()
    {
        var hex = new string('A', 32) + new string('b', 32);
        Assert.Equal(hex.ToLowerInvariant(), UpdatePolicy.ParseSha256File(hex + " *FpsOverlay.exe\n"));
        Assert.Null(UpdatePolicy.ParseSha256File("nope"));
    }
}
