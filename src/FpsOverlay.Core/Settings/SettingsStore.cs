using System.Text.Json;
using FpsOverlay.Core.GameOnly;
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
                Layout = ReadEnum(root, nameof(OverlaySettings.Layout), d.Layout),
                OffsetX = ReadDouble(root, nameof(OverlaySettings.OffsetX), d.OffsetX),
                OffsetY = ReadDouble(root, nameof(OverlaySettings.OffsetY), d.OffsetY),
                Scale = ReadDouble(root, nameof(OverlaySettings.Scale), d.Scale),
                BackgroundOpacity = ReadDouble(root, nameof(OverlaySettings.BackgroundOpacity), d.BackgroundOpacity),
                ColorByFps = ReadBool(root, nameof(OverlaySettings.ColorByFps), d.ColorByFps),
                GoodThreshold = ReadInt(root, nameof(OverlaySettings.GoodThreshold), d.GoodThreshold),
                BadThreshold = ReadInt(root, nameof(OverlaySettings.BadThreshold), d.BadThreshold),
                Language = ReadEnum(root, nameof(OverlaySettings.Language), d.Language),
                ShowOnePercentLow = ReadBool(root, nameof(OverlaySettings.ShowOnePercentLow), d.ShowOnePercentLow),
                ShowGpuLoad = ReadBool(root, nameof(OverlaySettings.ShowGpuLoad), d.ShowGpuLoad),
                ShowGpuTemp = ReadBool(root, nameof(OverlaySettings.ShowGpuTemp), d.ShowGpuTemp),
                ShowGpuMemory = ReadBool(root, nameof(OverlaySettings.ShowGpuMemory), d.ShowGpuMemory),
                ShowGpuClock = ReadBool(root, nameof(OverlaySettings.ShowGpuClock), d.ShowGpuClock),
                ShowGpuPower = ReadBool(root, nameof(OverlaySettings.ShowGpuPower), d.ShowGpuPower),
                ShowGpuFan = ReadBool(root, nameof(OverlaySettings.ShowGpuFan), d.ShowGpuFan),
                ShowCpuLoad = ReadBool(root, nameof(OverlaySettings.ShowCpuLoad), d.ShowCpuLoad),
                ShowCpuTemp = ReadBool(root, nameof(OverlaySettings.ShowCpuTemp), d.ShowCpuTemp),
                ShowRam = ReadBool(root, nameof(OverlaySettings.ShowRam), d.ShowRam),
                GpuHotThreshold = ReadInt(root, nameof(OverlaySettings.GpuHotThreshold), d.GpuHotThreshold),
                CpuHotThreshold = ReadInt(root, nameof(OverlaySettings.CpuHotThreshold), d.CpuHotThreshold),
                HotAlertSeconds = ReadInt(root, nameof(OverlaySettings.HotAlertSeconds), d.HotAlertSeconds),
                HotAlertBlink = ReadBool(root, nameof(OverlaySettings.HotAlertBlink), d.HotAlertBlink),
                HotAlertSound = ReadBool(root, nameof(OverlaySettings.HotAlertSound), d.HotAlertSound),
                LoggingEnabled = ReadBool(root, nameof(OverlaySettings.LoggingEnabled), d.LoggingEnabled),
                PawnIoOffered = ReadBool(root, nameof(OverlaySettings.PawnIoOffered), d.PawnIoOffered),
                SkippedVersion = TryGet(root, nameof(OverlaySettings.SkippedVersion), JsonValueKind.String, out var skipped) ? skipped.GetString() : d.SkippedVersion,
                ShowOnlyInGame = ReadBool(root, nameof(OverlaySettings.ShowOnlyInGame), d.ShowOnlyInGame),
                AlwaysShowApps = ReadAppNames(root, nameof(OverlaySettings.AlwaysShowApps)),
                NeverShowApps = ReadAppNames(root, nameof(OverlaySettings.NeverShowApps)),
                ClassicColors = ReadColors(root, nameof(OverlaySettings.ClassicColors), d.ClassicColors),
                CyberpunkColors = ReadColors(root, nameof(OverlaySettings.CyberpunkColors), d.CyberpunkColors),
                NeonPurpleColors = ReadColors(root, nameof(OverlaySettings.NeonPurpleColors), d.NeonPurpleColors),
                TerminalGreenColors = ReadColors(root, nameof(OverlaySettings.TerminalGreenColors), d.TerminalGreenColors),
            }.Normalize();
        }
        catch (Exception)   // file hỏng, bị khoá, đường dẫn lạ...: lỗi gì cũng về mặc định
        {
            return OverlaySettings.Defaults;
        }
    }

    public static void Save(string path, OverlaySettings settings)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, WriteOptions));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch (Exception) { }   // không để lại file .tmp mồ côi
            throw;
        }
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
        TryGet(obj, name, JsonValueKind.Number, out var v) && v.TryGetDouble(out var d) && double.IsFinite(d) ? d : fallback;

    private static int ReadInt(JsonElement obj, string name, int fallback)
    {
        if (!TryGet(obj, name, JsonValueKind.Number, out var v)) return fallback;
        if (v.TryGetInt32(out var i)) return i;
        return v.TryGetDouble(out var d) && double.IsFinite(d) ? (int)Math.Clamp(Math.Round(d), int.MinValue, int.MaxValue) : fallback;
    }

    private static bool ReadBool(JsonElement obj, string name, bool fallback) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : fallback;

    // Mảng chuỗi; phần tử không phải chuỗi bị bỏ, kiểu sai thì rỗng.
    private static AppNames ReadAppNames(JsonElement obj, string name) =>
        TryGet(obj, name, JsonValueKind.Array, out var arr)
            ? AppNames.From(arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()))
            : AppNames.Empty;

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
