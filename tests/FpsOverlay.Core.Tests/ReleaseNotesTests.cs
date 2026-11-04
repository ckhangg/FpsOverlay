using FpsOverlay.Core.Update;

namespace FpsOverlay.Core.Tests;

public class ReleaseNotesTests
{
    [Fact]
    public void Removes_bold_markers_and_inline_code_ticks()
    {
        Assert.Equal("Install: now", ReleaseNotes.ToPlainText("**Install:** now"));
        Assert.Equal("Install: now", ReleaseNotes.ToPlainText("__Install:__ now"));
        Assert.Equal("SHA-256: abc123", ReleaseNotes.ToPlainText("SHA-256: `abc123`"));
    }

    [Fact]
    public void Removes_code_fences_but_keeps_inner_text_verbatim()
    {
        var notes = "Run:\n\n```\ncurl -o \"%TEMP%\\x.exe\" https://a/b && start \"\" x__y.exe\n```\n\nDone.";
        Assert.Equal("Run:\n\ncurl -o \"%TEMP%\\x.exe\" https://a/b && start \"\" x__y.exe\n\nDone.",
            ReleaseNotes.ToPlainText(notes));
    }

    [Fact]
    public void Fence_with_language_tag_is_removed()
    {
        Assert.Equal("dir", ReleaseNotes.ToPlainText("```cmd\ndir\n```"));
    }

    [Fact]
    public void Removes_leading_heading_markers()
    {
        Assert.Equal("Changes\nFixes\nC# is #1", ReleaseNotes.ToPlainText("## Changes\n### Fixes\nC# is #1"));
    }

    [Fact]
    public void Normalizes_line_endings_and_trims()
    {
        Assert.Equal("a\nb", ReleaseNotes.ToPlainText("\r\n**a**\r\nb\r\n\r\n"));
        Assert.Equal("", ReleaseNotes.ToPlainText(""));
        Assert.Equal("", ReleaseNotes.ToPlainText("```\n```"));
    }

    [Fact]
    public void Release_script_notes_become_readable()
    {
        var notes = "**Cài đặt (Windows 10/11 64-bit, cần quyền Admin) / Install:**\n\n```\ncurl -fL x && start \"\" y\n```\n\n" +
                    "Chạy lệnh trên trong Command Prompt (cmd).\n\nSHA-256: `0123abcd`";
        Assert.Equal("Cài đặt (Windows 10/11 64-bit, cần quyền Admin) / Install:\n\ncurl -fL x && start \"\" y\n\n" +
                     "Chạy lệnh trên trong Command Prompt (cmd).\n\nSHA-256: 0123abcd", ReleaseNotes.ToPlainText(notes));
    }
}
