using System.Text;

namespace FpsOverlay.Core.Update;

/// <summary>Ghi chú phát hành trên GitHub viết bằng Markdown; hộp thoại cập nhật chỉ hiện chữ thường (thuần logic).</summary>
public static class ReleaseNotes
{
    /// <summary>
    /// Bỏ Markdown đơn giản để hiện: dòng rào <c>```</c> bị xoá, chữ bên trong giữ nguyên (lệnh có thể chứa <c>__</c>, <c>#</c>);
    /// ngoài rào thì bỏ <c>**</c>, <c>__</c>, dấu <c>`</c> và dấu <c>#</c> đầu dòng tiêu đề (<c># </c> … <c>###### </c>).
    /// Xuống dòng về <c>\n</c>, cắt khoảng trắng đầu/cuối.
    /// </summary>
    public static string ToPlainText(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new StringBuilder(markdown.Length);
        bool inFence = false;
        bool first = true;
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }
            if (!first) result.Append('\n');
            first = false;
            result.Append(inFence ? line : Inline(StripHeading(line)));
        }
        return result.ToString().Trim();
    }

    // "## Tiêu đề" → "Tiêu đề"; "#1" hay "C# là" không phải tiêu đề (cần 1–6 dấu # rồi khoảng trắng, ở đầu dòng).
    static string StripHeading(string line)
    {
        var text = line.TrimStart();
        int hashes = 0;
        while (hashes < text.Length && text[hashes] == '#') hashes++;
        if (hashes is < 1 or > 6 || hashes >= text.Length || !char.IsWhiteSpace(text[hashes])) return line;
        return text[hashes..].TrimStart();
    }

    static string Inline(string line) => line.Replace("**", "").Replace("__", "").Replace("`", "");
}
