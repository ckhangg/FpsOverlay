using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FpsOverlay.Core.Tests;

/// <summary>
/// Các file chủ đề trong src/FpsOverlay/Themes phải định nghĩa cùng một tập khoá <c>Ui.*</c> (đổi chủ đề lúc chạy chỉ
/// thay một dictionary, thiếu khoá thì control mất kiểu), và các cửa sổ chỉ được dùng khoá có trong tập đó, gắn động.
/// Đọc thẳng file XAML trong mã nguồn (project test không tham chiếu project WPF).
/// </summary>
public class ThemeKeysTests
{
    static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    static readonly string[] WindowFiles = ["SettingsWindow.xaml", "InstallWindow.xaml", "UpdateWindow.xaml"];

    // Khoá mà code/cửa sổ cần ở mọi chủ đề, kể cả khi chưa cửa sổ nào dùng.
    static readonly string[] RequiredKeys =
        ["Ui.WindowTemplate", "Ui.WindowChrome", "Ui.ExpandGlyph", "Ui.CollapseGlyph", "Ui.OnText", "Ui.OffText"];

    static readonly Regex ResourceRef = new(@"\{(?<kind>StaticResource|DynamicResource)\s+(?<key>[^}\s]+)\s*\}");
    static readonly Regex CodeKey = new(@"""(?<key>Ui\.[A-Za-z0-9]+)""");

    static string AppDirectory
    {
        get
        {
            // Đi ngược từ thư mục bin tới gốc repo (thư mục có .git hoặc FpsOverlay.slnx).
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"))
                    || File.Exists(Path.Combine(dir.FullName, "FpsOverlay.slnx")))
                    return Path.Combine(dir.FullName, "src", "FpsOverlay");
            }
            throw new DirectoryNotFoundException("Không tìm thấy gốc repo từ " + AppContext.BaseDirectory);
        }
    }

    static string[] ThemeFiles()
    {
        var files = Directory.GetFiles(Path.Combine(AppDirectory, "Themes"), "*.xaml");
        Assert.NotEmpty(files);
        return files;
    }

    // Khoá ở cấp gốc của một ResourceDictionary (style, brush, chuỗi, ...).
    static HashSet<string> TopLevelKeys(XElement dictionary) =>
        dictionary.Elements().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>().ToHashSet();

    static HashSet<string> ThemeKeys(string file) => TopLevelKeys(XDocument.Load(file).Root!);

    // Khoá riêng của App.xaml (ngoài file chủ đề): khung cửa sổ cố định dùng chung cho mọi chủ đề.
    static HashSet<string> AppKeys()
    {
        var doc = XDocument.Load(Path.Combine(AppDirectory, "App.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = doc.Root!.Element(p + "Application.Resources")!.Element(p + "ResourceDictionary")!;
        return TopLevelKeys(dictionary);
    }

    [Fact]
    public void All_theme_files_define_the_same_keys()
    {
        var files = ThemeFiles();
        var expected = ThemeKeys(files[0]);
        foreach (var key in expected)
            Assert.True(key.StartsWith("Ui.", StringComparison.Ordinal), $"{Path.GetFileName(files[0])}: khoá '{key}' không phải Ui.*");
        foreach (var key in RequiredKeys)
            Assert.True(expected.Contains(key), $"{Path.GetFileName(files[0])} thiếu khoá {key}");

        foreach (var file in files.Skip(1))
        {
            var keys = ThemeKeys(file);
            var missing = expected.Except(keys).Order().ToList();
            var extra = keys.Except(expected).Order().ToList();
            Assert.True(missing.Count == 0 && extra.Count == 0,
                $"{Path.GetFileName(file)} khác {Path.GetFileName(files[0])}: thiếu [{string.Join(", ", missing)}], thừa [{string.Join(", ", extra)}]");
        }
    }

    [Fact]
    public void Windows_use_only_defined_keys()
    {
        var defined = ThemeKeys(ThemeFiles()[0]);
        defined.UnionWith(AppKeys());

        var checkedFiles = 0;
        foreach (var name in WindowFiles)
        {
            var xaml = Path.Combine(AppDirectory, name);
            if (!File.Exists(xaml)) continue;   // UpdateWindow chỉ có từ khi có tính năng cập nhật
            checkedFiles++;
            foreach (Match m in ResourceRef.Matches(File.ReadAllText(xaml)))
            {
                var key = m.Groups["key"].Value;
                Assert.True(defined.Contains(key), $"{name}: khoá '{key}' không có trong file chủ đề / App.xaml");
                // Gắn tĩnh thì đổi chủ đề lúc chạy không đổi theo.
                Assert.True(m.Groups["kind"].Value == "DynamicResource", $"{name}: '{key}' phải dùng DynamicResource");
            }

            // Control dựng bằng code (SetResourceReference/TryFindResource) cũng chỉ dùng khoá đã định nghĩa.
            var code = xaml + ".cs";
            if (File.Exists(code))
                foreach (Match m in CodeKey.Matches(File.ReadAllText(code)))
                    Assert.True(defined.Contains(m.Groups["key"].Value), $"{name}.cs: khoá '{m.Groups["key"].Value}' không có");
        }
        Assert.True(checkedFiles >= 2);
    }

    [Fact]
    public void Theme_styles_use_dynamic_resources()
    {
        // Trong file chủ đề chỉ BasedOn được gắn tĩnh (WPF không cho BasedOn động); còn lại gắn động để khoá đặt ở
        // cấp Application (vd. Ui.OnText theo ngôn ngữ) đè được lên giá trị trong file.
        foreach (var file in ThemeFiles())
            foreach (var line in File.ReadLines(file))
                foreach (Match m in ResourceRef.Matches(line))
                    if (m.Groups["kind"].Value == "StaticResource")
                        Assert.True(line.Contains("BasedOn=\"" + m.Value + "\"", StringComparison.Ordinal),
                            $"{Path.GetFileName(file)}: '{m.Value}' phải là DynamicResource");
    }
}
