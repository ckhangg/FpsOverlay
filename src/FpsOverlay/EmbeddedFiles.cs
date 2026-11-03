using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace FpsOverlay;

/// <summary>
/// Các file nhúng trong exe (tên manifest đặt bằng LogicalName trong csproj): PresentMon.exe và giấy phép.
/// ExtractAll bung tất cả vào cùng một thư mục, phẳng (không có thư mục con).
/// </summary>
static class EmbeddedFiles
{
    static readonly Assembly Asm = typeof(EmbeddedFiles).Assembly;

    /// <summary>Tên mọi file nhúng (cũng là tên file khi bung ra).</summary>
    public static IReadOnlyList<string> Names { get; } = Asm.GetManifestResourceNames()
        .Where(n => n == "PresentMon.exe" || n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Chỉ ghi file thiếu hoặc khác bản nhúng (so SHA-256); file khác thì ghi đè qua file tạm.</summary>
    public static void ExtractAll(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var name in Names)
        {
            var target = Path.Combine(directory, name);
            if (File.Exists(target) && FileHash(target) == ResourceHash(name)) continue;

            var temp = Path.Combine(directory, name + ".tmp");
            try
            {
                using (var src = Open(name))
                using (var dst = File.Create(temp))
                    src.CopyTo(dst);
                File.Move(temp, target, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    /// <summary>SHA-256 (hex thường) của file nhúng.</summary>
    public static string ResourceHash(string name)
    {
        using var s = Open(name);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    static string FileHash(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    static Stream Open(string name) =>
        Asm.GetManifestResourceStream(name) ?? throw new FileNotFoundException($"Thiếu tài nguyên nhúng {name}.", name);
}
