using System.Text.Json;
using System.Text.Json.Serialization;

namespace FpsOverlay.Core.Settings;

/// <summary>Đọc/ghi cài đặt dạng JSON. Không bao giờ ném lỗi khi đọc: file thiếu hoặc hỏng thì dùng mặc định.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FpsOverlay", "settings.json");

    public static OverlaySettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return OverlaySettings.Defaults;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return OverlaySettings.Defaults;

            var d = OverlaySettings.Defaults;
            return new OverlaySettings
            {
                Style = ReadEnum(root, nameof(OverlaySettings.Style), d.Style),
                Corner = ReadEnum(root, nameof(OverlaySettings.Corner), d.Corner),
                Scale = ReadDouble(root, nameof(OverlaySettings.Scale), d.Scale),
                BackgroundOpacity = ReadDouble(root, nameof(OverlaySettings.BackgroundOpacity), d.BackgroundOpacity),
                ColorByFps = ReadBool(root, nameof(OverlaySettings.ColorByFps), d.ColorByFps),
                GoodThreshold = ReadInt(root, nameof(OverlaySettings.GoodThreshold), d.GoodThreshold),
                BadThreshold = ReadInt(root, nameof(OverlaySettings.BadThreshold), d.BadThreshold),
                Language = ReadEnum(root, nameof(OverlaySettings.Language), d.Language),
                ClassicColors = ReadColors(root, nameof(OverlaySettings.ClassicColors), d.ClassicColors),
                CyberpunkColors = ReadColors(root, nameof(OverlaySettings.CyberpunkColors), d.CyberpunkColors),
            }.Normalize();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return OverlaySettings.Defaults;
        }
    }

    public static void Save(string path, OverlaySettings settings)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, WriteOptions));
        File.Move(temp, path, overwrite: true);
    }

    private static bool TryGet(JsonElement obj, string name, JsonValueKind kind, out JsonElement value) =>
        obj.TryGetProperty(name, out value) && value.ValueKind == kind;

    private static T ReadEnum<T>(JsonElement obj, string name, T fallback) where T : struct, Enum =>
        TryGet(obj, name, JsonValueKind.String, out var v)
        && Enum.TryParse<T>(v.GetString(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : fallback;

    private static double ReadDouble(JsonElement obj, string name, double fallback) =>
        TryGet(obj, name, JsonValueKind.Number, out var v) && v.TryGetDouble(out var d) ? d : fallback;

    private static int ReadInt(JsonElement obj, string name, int fallback)
    {
        if (!TryGet(obj, name, JsonValueKind.Number, out var v)) return fallback;
        if (v.TryGetInt32(out var i)) return i;
        return v.TryGetDouble(out var d) ? (int)Math.Clamp(Math.Round(d), int.MinValue, int.MaxValue) : fallback;
    }

    private static bool ReadBool(JsonElement obj, string name, bool fallback) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : fallback;

    private static StyleColors ReadColors(JsonElement obj, string name, StyleColors fallback)
    {
        if (!TryGet(obj, name, JsonValueKind.Object, out var c)) return fallback;
        return new StyleColors(
            ReadString(c, nameof(StyleColors.Label), fallback.Label),
            ReadString(c, nameof(StyleColors.Value), fallback.Value),
            ReadString(c, nameof(StyleColors.Fps), fallback.Fps));
    }

    private static string ReadString(JsonElement obj, string name, string fallback) =>
        TryGet(obj, name, JsonValueKind.String, out var v) ? v.GetString() ?? fallback : fallback;
}
