namespace FpsOverlay.Core.Tests;

/// <summary>
/// Icon ứng dụng (src/FpsOverlay/Assets/EzFPS.ico, sinh bằng scripts/make-icon.ps1) phải đủ cỡ, và csproj phải nhúng nó.
/// Project test không phải Windows nên đọc header ico bằng tay, không dùng System.Drawing.
/// </summary>
public class AppIconTests
{
    static string RepoRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"))
                    || File.Exists(Path.Combine(dir.FullName, "FpsOverlay.slnx")))
                    return dir.FullName;
            }
            throw new DirectoryNotFoundException("Không tìm thấy gốc repo từ " + AppContext.BaseDirectory);
        }
    }

    [Fact]
    public void Icon_has_all_sizes()
    {
        var bytes = File.ReadAllBytes(Path.Combine(RepoRoot, "src", "FpsOverlay", "Assets", "EzFPS.ico"));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0)); // reserved
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2)); // type = icon
        var count = BitConverter.ToUInt16(bytes, 4);
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var entry = 6 + i * 16;
            Assert.Equal(bytes[entry], bytes[entry + 1]); // ảnh vuông
            sizes.Add(bytes[entry] == 0 ? 256 : bytes[entry]); // byte 0 nghĩa là 256
        }
        Assert.Equal(new[] { 16, 24, 32, 48, 64, 128, 256 }, sizes.Order().ToArray());
    }

    [Fact(Skip = "Đỏ tới khi Task 2 thêm ApplicationIcon vào csproj")]
    public void Csproj_embeds_application_icon()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot, "src", "FpsOverlay", "FpsOverlay.csproj"));
        Assert.Contains(@"<ApplicationIcon>Assets\EzFPS.ico</ApplicationIcon>", csproj);
    }
}
