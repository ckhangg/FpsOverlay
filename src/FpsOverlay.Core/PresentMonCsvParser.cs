using System.Globalization;

namespace FpsOverlay.Core;

/// <summary>Đọc CSV mà PresentMon xuất ra (chế độ --v1_metrics) thành các <see cref="FrameSample"/>.</summary>
public sealed class PresentMonCsvParser
{
    const string ProcessIdColumn = "ProcessID";
    const string TimeColumn = "TimeInSeconds";

    int _processIdIndex = -1;
    int _timeIndex = -1;

    /// <summary>Tìm vị trí các cột cần dùng theo tên. Trả false nếu thiếu cột.</summary>
    public bool TryReadHeader(string line)
    {
        var columns = line.Split(',');
        _processIdIndex = IndexOf(columns, ProcessIdColumn);
        _timeIndex = IndexOf(columns, TimeColumn);
        return _processIdIndex >= 0 && _timeIndex >= 0;
    }

    /// <summary>Đọc một dòng dữ liệu. Trả false nếu chưa có header hoặc dòng không hợp lệ.</summary>
    public bool TryParse(string line, out FrameSample sample)
    {
        sample = default;
        if (_processIdIndex < 0 || _timeIndex < 0)
            return false;

        var fields = line.Split(',');
        if (fields.Length <= Math.Max(_processIdIndex, _timeIndex))
            return false;

        if (!int.TryParse(fields[_processIdIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) ||
            !double.TryParse(fields[_timeIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
            return false;

        sample = new FrameSample(pid, time);
        return true;
    }

    /// <summary>Dòng đầu là header, mỗi dòng hợp lệ sau đó được đưa vào <paramref name="onFrame"/>.</summary>
    public static async Task ReadFramesAsync(TextReader reader, Action<FrameSample> onFrame, CancellationToken ct)
    {
        var parser = new PresentMonCsvParser();
        var headerRead = false;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!headerRead)
            {
                headerRead = parser.TryReadHeader(line);
                continue;
            }

            if (parser.TryParse(line, out var sample))
                onFrame(sample);
        }
    }

    static int IndexOf(string[] columns, string name)
        => Array.FindIndex(columns, c => string.Equals(c.Trim(), name, StringComparison.OrdinalIgnoreCase));
}
