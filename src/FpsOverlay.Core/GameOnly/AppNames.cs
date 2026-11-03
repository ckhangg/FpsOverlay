using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FpsOverlay.Core.GameOnly;

/// <summary>
/// Danh sách tên file exe (vd "eldenring.exe"). Bất biến, so sánh không phân biệt hoa thường;
/// mỗi tên đã cắt khoảng trắng, chỉ còn phần tên file, không rỗng, không trùng.
/// </summary>
[JsonConverter(typeof(AppNamesJsonConverter))]
public sealed class AppNames : IEquatable<AppNames>, IReadOnlyList<string>
{
    public static AppNames Empty { get; } = new([]);

    private readonly string[] _items;

    private AppNames(string[] items) => _items = items;

    public static AppNames From(IEnumerable<string?> names)
    {
        var list = new List<string>();
        foreach (var raw in names)
        {
            var name = Normalize(raw);
            if (name is null || IndexOf(list, name) >= 0) continue;
            list.Add(name);
        }
        return list.Count == 0 ? Empty : new AppNames([.. list]);
    }

    public int Count => _items.Length;

    public string this[int index] => _items[index];

    public bool Contains(string? exeName)
    {
        if (_items.Length == 0) return false;   // danh sách rỗng: không chuẩn hoá chuỗi mỗi nhịp dò
        var name = Normalize(exeName);
        return name is not null && IndexOf(_items, name) >= 0;
    }

    public AppNames With(string exeName) => From([.. _items, exeName]);

    public AppNames Without(string exeName)
    {
        var name = Normalize(exeName);
        return name is null ? this : From(_items.Where(n => !n.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }

    public bool Equals(AppNames? other)
    {
        if (other is null || other._items.Length != _items.Length) return false;
        for (var i = 0; i < _items.Length; i++)
            if (!string.Equals(_items[i], other._items[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as AppNames);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item, StringComparer.OrdinalIgnoreCase);
        return hash.ToHashCode();
    }

    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var name = Path.GetFileName(raw.Trim()).Trim();
        return name.Length == 0 ? null : name;
    }

    private static int IndexOf(IReadOnlyList<string> items, string name)
    {
        for (var i = 0; i < items.Count; i++)
            if (items[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private sealed class AppNamesJsonConverter : JsonConverter<AppNames>
    {
        public override AppNames Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                return Empty;
            }
            var names = new List<string?>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String) names.Add(reader.GetString());
                else reader.Skip();
            }
            return From(names);
        }

        public override void Write(Utf8JsonWriter writer, AppNames value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var name in value) writer.WriteStringValue(name);
            writer.WriteEndArray();
        }
    }
}
