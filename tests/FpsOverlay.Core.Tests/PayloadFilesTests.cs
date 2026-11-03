using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using FpsOverlay.Core.Install;

namespace FpsOverlay.Core.Tests;

// Thay file trong thư mục cài từ Payload.zip: chạy trên thư mục tạm, không đụng Program Files.
public sealed class PayloadFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FpsOverlayTests-" + Guid.NewGuid().ToString("N"));
    private string Install => Path.Combine(_root, "FpsOverlay");
    private static readonly string[] Required = ["FpsOverlay.exe", "PresentMon.exe"];

    public PayloadFilesTests() => Directory.CreateDirectory(Install);

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private static MemoryStream Zip(params (string Name, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                if (name.EndsWith('/')) continue;   // mục thư mục
                using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
                w.Write(content);
            }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream ValidZip(params (string Name, string Content)[] extra) =>
        Zip([("FpsOverlay.exe", "new exe"), ("PresentMon.exe", "pm"), .. extra]);

    private string In(string relative) => Path.Combine(Install, relative);

    private void Write(string relative, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(In(relative))!);
        File.WriteAllText(In(relative), content);
    }

    private void Apply(Stream zip) => PayloadFiles.Apply(Install, zip, "abc123", Required);

    private bool NoStaging() => Directory.GetDirectories(Install, PayloadFiles.StagingPrefix + "*").Length == 0;

    [Fact]
    public void Fresh_install_places_files_and_writes_hash_last()
    {
        Apply(ValidZip(("cs/FpsOverlay.resources.dll", "cs"), ("wpfgfx_cor3.dll", "native")));

        Assert.Equal("new exe", File.ReadAllText(In("FpsOverlay.exe")));
        Assert.Equal("native", File.ReadAllText(In("wpfgfx_cor3.dll")));
        Assert.Equal("cs", File.ReadAllText(In(@"cs\FpsOverlay.resources.dll")));
        Assert.Equal("abc123", PayloadFiles.ReadHash(Install));
        Assert.True(NoStaging());
    }

    [Fact]
    public void Update_from_single_file_install_replaces_and_deletes_obsolete_but_keeps_temp()
    {
        Write("FpsOverlay.exe", "old single-file exe");
        Write("PresentMon.exe", "old pm");
        Write("old.dll", "obsolete");
        Write(@"gone\x.dll", "obsolete");
        Write(@"Temp\PawnIO_setup.exe", "keep");
        Write("readonly.txt", "obsolete");
        File.SetAttributes(In("readonly.txt"), FileAttributes.ReadOnly);

        Apply(ValidZip(("LICENSE.txt", "mit")));

        Assert.Equal("new exe", File.ReadAllText(In("FpsOverlay.exe")));
        Assert.Equal("pm", File.ReadAllText(In("PresentMon.exe")));
        Assert.Equal("mit", File.ReadAllText(In("LICENSE.txt")));
        Assert.False(File.Exists(In("old.dll")));
        Assert.False(File.Exists(In("readonly.txt")));
        Assert.False(Directory.Exists(In("gone")));
        Assert.Equal("keep", File.ReadAllText(In(@"Temp\PawnIO_setup.exe")));
        Assert.Equal("abc123", PayloadFiles.ReadHash(Install));
    }

    [Fact]
    public void Leftover_staging_from_an_interrupted_install_is_removed()
    {
        Write(PayloadFiles.StagingPrefix + "dead" + @"\FpsOverlay.exe", "half");

        Apply(ValidZip());

        Assert.True(NoStaging());
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("sub/../../evil.txt")]
    [InlineData("/evil.txt")]
    [InlineData("C:/evil.txt")]
    public void Entry_escaping_the_staging_folder_is_rejected_before_anything_is_moved(string name)
    {
        Write("FpsOverlay.exe", "old exe");
        File.WriteAllText(In(PayloadFiles.HashFileName), "old");

        Assert.Throws<InvalidDataException>(() => Apply(ValidZip((name, "evil"))));

        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetPathRoot(_root)!, "evil.txt")));
        Assert.Equal("old exe", File.ReadAllText(In("FpsOverlay.exe")));
        Assert.Null(PayloadFiles.ReadHash(Install));   // dấu cũ đã xoá: lần sau vẫn cập nhật
        Assert.True(NoStaging());
    }

    [Theory]
    [InlineData("Temp/x.exe")]
    [InlineData("payload.sha256")]
    [InlineData(".staging-x/y.dll")]
    [InlineData("a.txt:stream")]
    public void Reserved_or_invalid_entry_names_are_rejected(string name)
    {
        Assert.Throws<InvalidDataException>(() => Apply(ValidZip((name, "x"))));
        Assert.True(NoStaging());
    }

    [Fact]
    public void Duplicate_entries_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Apply(ValidZip(("a.dll", "1"), ("A.DLL", "2"))));
    }

    [Fact]
    public void Missing_required_file_fails_without_writing_hash()
    {
        Assert.Throws<InvalidDataException>(() => Apply(Zip(("FpsOverlay.exe", "exe"))));

        Assert.False(File.Exists(In("FpsOverlay.exe")));
        Assert.Null(PayloadFiles.ReadHash(Install));
        Assert.True(NoStaging());
    }

    [Fact]
    public void Link_inside_install_folder_is_refused_and_its_target_untouched()
    {
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "precious.txt"), "keep");
        var link = In("link");
        using (var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{link}\" \"{outside}\"")
               { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!)
            p.WaitForExit();
        Assert.True(new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint));

        Assert.Throws<IOException>(() => Apply(ValidZip()));

        Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "precious.txt")));
        Assert.True(NoStaging());
        Directory.Delete(link);   // gỡ junction trước khi Dispose xoá thư mục
    }

    [Fact]
    public void Running_exe_inside_install_folder_is_not_deleted()
    {
        Write(@"sub\carrier.exe", "carrier");

        PayloadFiles.Apply(Install, ValidZip(), "h", Required, keepPath: In(@"sub\carrier.exe"));

        Assert.True(File.Exists(In(@"sub\carrier.exe")));
    }

    [Fact]
    public void ReadHash_is_null_when_missing_and_trims_whitespace()
    {
        Assert.Null(PayloadFiles.ReadHash(Install));
        File.WriteAllText(In(PayloadFiles.HashFileName), "  ABC\r\n");
        Assert.Equal("ABC", PayloadFiles.ReadHash(Install));
    }
}
