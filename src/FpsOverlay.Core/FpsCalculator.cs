using System.Diagnostics;
using System.Globalization;

namespace FpsOverlay.Core;

/// <summary>
/// Tính FPS của từng tiến trình trên cửa sổ trượt <see cref="WindowSeconds"/> giây gần nhất.
/// "Bây giờ" là thời điểm khung hình mới nhất PresentMon báo (mọi tiến trình), cộng thời gian đã trôi qua
/// kể từ lúc nhận khung hình đó, để con số không bị treo khi không còn tiến trình nào vẽ.
/// An toàn khi <see cref="Add"/> gọi từ luồng đọc và <see cref="GetFps"/> từ luồng UI.
/// </summary>
public sealed class FpsCalculator
{
    public const double WindowSeconds = 0.5;

    /// <summary>
    /// Độ trễ cho phép trước khi coi khung hình là cũ. Windows giao dữ liệu cho PresentMon theo đợt,
    /// cách nhau tới ~1,07 giây (đo thực tế), nên giá trị này phải lớn hơn khoảng đó, nếu không con số sẽ chớp "--".
    /// </summary>
    public const double LatencySlackSeconds = 1.5;

    readonly Queue<FrameSample> _frames = new();
    readonly object _lock = new();
    readonly Func<double> _clock;
    double _latestTime = double.MinValue;
    double _clockAtLatest;

    /// <param name="clock">Đồng hồ tính bằng giây; mặc định là <see cref="Stopwatch"/>. Test truyền đồng hồ giả.</param>
    public FpsCalculator(Func<double>? clock = null)
    {
        _clock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    }

    public void Add(FrameSample frame)
    {
        lock (_lock)
        {
            _frames.Enqueue(frame);
            _latestTime = Math.Max(_latestTime, frame.TimeSeconds);
            _clockAtLatest = _clock();

            var cutoff = _latestTime - WindowSeconds;
            while (_frames.Count > 0 && _frames.Peek().TimeSeconds < cutoff)
                _frames.Dequeue();
        }
    }

    /// <summary>Số giây kể từ khung hình mới nhất (của mọi tiến trình), hoặc null nếu chưa nhận khung nào.</summary>
    public double? SecondsSinceAnyFrame()
    {
        lock (_lock)
            return _latestTime == double.MinValue ? null : _clock() - _clockAtLatest;
    }

    /// <summary>FPS của tiến trình, hoặc null nếu nó có ít hơn 2 khung hình trong cửa sổ trượt.</summary>
    public double? GetFps(int processId)
    {
        lock (_lock)
        {
            var cutoff = _latestTime - WindowSeconds;
            int count = 0;
            double first = 0, last = 0;

            foreach (var f in _frames)
            {
                if (f.ProcessId != processId || f.TimeSeconds < cutoff)
                    continue;
                if (count == 0)
                    first = f.TimeSeconds;
                last = f.TimeSeconds;
                count++;
            }

            if (count < 2 || last <= first)
                return null;

            var now = _latestTime + (_clock() - _clockAtLatest);
            if (last < now - WindowSeconds - LatencySlackSeconds)
                return null;
            return (count - 1) / (last - first);
        }
    }

    /// <summary>FPS của tiến trình đầu tiên trong danh sách đang vẽ hình, hoặc null nếu không tiến trình nào vẽ.</summary>
    public double? GetFps(IReadOnlyList<int> processIds)
    {
        for (int i = 0; i < processIds.Count; i++)
            if (GetFps(processIds[i]) is { } fps)
                return fps;
        return null;
    }

    // Chuỗi dựng sẵn cho các giá trị hay gặp: Format chạy mỗi khung hình, không cấp phát chuỗi mới.
    static readonly string[] Texts = Enumerable.Range(0, 1000).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();

    /// <summary>Số làm tròn (nửa làm tròn lên, khớp <see cref="FpsLevels.Classify"/>), hoặc "--".</summary>
    public static string Format(double? fps)
    {
        if (fps is not { } value)
            return "--";
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded >= 0 && rounded < Texts.Length
            ? Texts[(int)rounded]
            : rounded.ToString("0", CultureInfo.InvariantCulture);
    }
}
