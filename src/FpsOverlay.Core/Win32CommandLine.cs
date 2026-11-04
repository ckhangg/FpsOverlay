using System.Text;

namespace FpsOverlay.Core;

/// <summary>
/// Dựng dòng lệnh và khối biến môi trường cho CreateProcessW (thuần logic), khi phải tự gọi CreateProcess thay vì
/// Process.Start (vd. tách khỏi Job Object lúc chạy bản cập nhật).
/// </summary>
public static class Win32CommandLine
{
    /// <summary>Exe luôn trong ngoặc kép (argv[0] không có quy tắc thoát, đường dẫn không chứa <c>"</c>), rồi từng tham số đã thoát.</summary>
    public static string Build(string exePath, IEnumerable<string> arguments)
    {
        var line = new StringBuilder().Append('"').Append(exePath).Append('"');
        foreach (var argument in arguments)
            line.Append(' ').Append(Quote(argument));
        return line.ToString();
    }

    /// <summary>
    /// Thoát một tham số theo quy tắc CommandLineToArgvW: không có khoảng trắng/ngoặc kép thì giữ nguyên; không thì bọc ngoặc
    /// kép, <c>\</c> đứng trước <c>"</c> hay trước ngoặc đóng thì nhân đôi, <c>"</c> thành <c>\"</c>.
    /// </summary>
    public static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0) return argument;
        var result = new StringBuilder(argument.Length + 2).Append('"');
        for (int i = 0; i < argument.Length; i++)
        {
            int backslashes = 0;
            while (i < argument.Length && argument[i] == '\\')
            {
                backslashes++;
                i++;
            }
            if (i == argument.Length)
            {
                result.Append('\\', backslashes * 2);
                break;
            }
            if (argument[i] == '"')
                result.Append('\\', backslashes * 2 + 1).Append('"');
            else
                result.Append('\\', backslashes).Append(argument[i]);
        }
        return result.Append('"').ToString();
    }

    /// <summary>
    /// Khối môi trường Unicode (dùng với CREATE_UNICODE_ENVIRONMENT): <c>tên=giá trị\0</c> xếp theo tên không phân biệt hoa
    /// thường (Windows yêu cầu), kết thúc bằng thêm một <c>\0</c>. Giá trị null bị bỏ.
    /// </summary>
    public static string EnvironmentBlock(IEnumerable<KeyValuePair<string, string?>> variables)
    {
        var block = new StringBuilder();
        foreach (var (name, value) in variables.Where(v => v.Value is not null).OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
            block.Append(name).Append('=').Append(value).Append('\0');
        if (block.Length == 0) block.Append('\0');   // khối rỗng vẫn phải có hai \0
        return block.Append('\0').ToString();
    }
}
