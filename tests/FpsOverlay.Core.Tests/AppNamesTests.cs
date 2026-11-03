using System.Text.Json;
using FpsOverlay.Core.GameOnly;

namespace FpsOverlay.Core.Tests;

public class AppNamesTests
{
    [Fact]
    public void Names_compare_case_insensitively_and_keep_first_spelling()
    {
        var names = AppNames.From(["EldenRing.exe", "eldenring.exe"]);
        Assert.Single(names);
        Assert.Equal("EldenRing.exe", names[0]);
        Assert.True(names.Contains("ELDENRING.EXE"));
    }

    [Fact]
    public void From_trims_strips_paths_and_drops_blanks()
    {
        var names = AppNames.From([@"  C:\Games\a.exe ", "", "   ", null, "b.exe"]);
        Assert.Equal(["a.exe", "b.exe"], names);
        Assert.False(names.Contains(null));
        Assert.False(names.Contains(""));
    }

    [Fact]
    public void With_and_Without_are_case_insensitive()
    {
        var names = AppNames.Empty.With("a.exe").With("A.EXE").With("b.exe");
        Assert.Equal(["a.exe", "b.exe"], names);
        Assert.Equal(["b.exe"], names.Without("A.exe"));
    }

    [Fact]
    public void Equality_is_by_content_and_order()
    {
        Assert.Equal(AppNames.From(["a.exe", "b.exe"]), AppNames.From(["A.EXE", "B.exe"]));
        Assert.Equal(AppNames.From(["a.exe"]).GetHashCode(), AppNames.From(["A.EXE"]).GetHashCode());
        Assert.NotEqual(AppNames.From(["a.exe", "b.exe"]), AppNames.From(["b.exe", "a.exe"]));
        Assert.Equal(AppNames.Empty, AppNames.From([]));
    }

    [Fact]
    public void Json_is_a_string_array()
    {
        var json = JsonSerializer.Serialize(AppNames.From(["a.exe", "b.exe"]));
        Assert.Equal("[\"a.exe\",\"b.exe\"]", json);
        Assert.Equal(AppNames.From(["a.exe", "b.exe"]), JsonSerializer.Deserialize<AppNames>(json));
    }
}
