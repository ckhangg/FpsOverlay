using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace FpsOverlay;

/// <summary>
/// Các file nhúng trong exe (tên manifest đặt bằng LogicalName trong csproj). Bản mang đi và <c>dotnet build</c>: PresentMon.exe
/// và giấy phép (ExtractAll bung phẳng vào một thư mục, chỉ dùng cho --no-install), cộng thêm <see cref="PayloadName"/> ở bản
/// mang đi. Bản cài (nhiều file) không nhúng gì: các file đó nằm sẵn cạnh exe.
/// </summary>
static class EmbeddedFiles
{
    /// <summary>Bản nhiều file để cài (chỉ có trong bản mang đi do <c>dotnet publish</c> tạo). Không bao giờ bung bằng ExtractAll.</summary>
    public const string PayloadName = "Payload.zip";

    static readonly Assembly Asm = typeof(EmbeddedFiles).Assembly;

    /// <summary>Tên mọi file ExtractAll bung (cũng là tên file khi bung ra): PresentMon.exe và *.txt, không có Payload.zip.</summary>
    public static IReadOnlyList<string> Names { get; } = Asm.GetManifestResourceNames()
        .Where(n => n == "PresentMon.exe" || n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        .Where(n => n != PayloadName)
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Exe đang chạy có nhúng Payload.zip không (bản mang đi do dotnet publish tạo).</summary>
    public static bool HasPayload { get; } = Asm.GetManifestResourceInfo(PayloadName) is not null;

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

    /// <summary>Mở luồng đọc file nhúng; thiếu thì ném <see cref="FileNotFoundException"/>.</summary>
    public static Stream Open(string name) =>
        Asm.GetManifestResourceStream(name) ?? throw new FileNotFoundException($"Thiếu tài nguyên nhúng {name}.", name);
}
