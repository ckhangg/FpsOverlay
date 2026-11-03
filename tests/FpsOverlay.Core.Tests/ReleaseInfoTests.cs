using FpsOverlay.Core.Update;

namespace FpsOverlay.Core.Tests;

public class ReleaseInfoTests
{
    const string Page = "https://github.com/ckhangg/FpsOverlay/releases/tag/v1.4.0";
    const string Exe = "https://github.com/ckhangg/FpsOverlay/releases/download/v1.4.0/FpsOverlay.exe";
    const string Hash = "https://github.com/ckhangg/FpsOverlay/releases/download/v1.4.0/FpsOverlay.exe.sha256";

    static string Json(string tag = "v1.4.0", string exeUrl = Exe, bool withHash = true) =>
        $$"""
        {"tag_name":"{{tag}}","body":"Notes","html_url":"{{Page}}","assets":[
          {"name":"FpsOverlay.exe","browser_download_url":"{{exeUrl}}"}
          {{(withHash ? $$""",{"name":"FpsOverlay.exe.sha256","browser_download_url":"{{Hash}}"}""" : "")}}
        ]}
        """;

    [Fact]
    public void Parses_release()
    {
        var r = ReleaseInfo.Parse(Json())!;
        Assert.Equal(new Version(1, 4, 0), r.Version);
        Assert.Equal("v1.4.0", r.Tag);
        Assert.Equal("Notes", r.Notes);
        Assert.Equal(Page, r.PageUrl);
        Assert.Equal(Exe, r.ExeUrl);
        Assert.Equal(Hash, r.HashUrl);
    }

    [Fact]
    public void Missing_asset_means_no_update() => Assert.Null(ReleaseInfo.Parse(Json(withHash: false)));

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("")]
    public void Broken_json_is_no_update(string json) => Assert.Null(ReleaseInfo.Parse(json));

    [Fact]
    public void Http_urls_are_rejected() =>
        Assert.Null(ReleaseInfo.Parse(Json(exeUrl: Exe.Replace("https://", "http://"))));

    [Theory]
    [InlineData("v1.4.0", "1.4.0")]
    [InlineData("1.4.0-beta", "1.4.0")]
    [InlineData("V2.0.1+abc", "2.0.1")]
    public void Odd_tags_parse_or_are_ignored(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), ReleaseInfo.Parse(Json(tag))!.Version);

    [Fact]
    public void Tag_without_three_numbers_is_ignored()
    {
        Assert.Null(UpdatePolicy.ParseVersion("v1.4"));
        Assert.Null(ReleaseInfo.Parse(Json("v1.4")));
    }
}
