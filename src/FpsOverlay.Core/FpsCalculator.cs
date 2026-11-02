using System.Globalization;

namespace FpsOverlay.Core;

/// <summary>
/// Tính FPS của từng tiến trình trên cửa sổ trượt <see cref="WindowSeconds"/> giây gần nhất.
/// "Bây giờ" là thời điểm khung hình mới nhất PresentMon báo (mọi tiến trình), để cùng một hệ thời gian.
/// An toàn khi <see cref="Add"/> gọi từ luồng đọc và <see cref="GetFps"/> từ luồng UI.
/// </summary>
public sealed class FpsCalculator
{
    public const double WindowSeconds = 0.5;

    readonly Queue<FrameSample> _frames = new();
    readonly object _lock = new();
    double _latestTime = double.MinValue;

    public void Add(FrameSample frame)
    {
        lock (_lock)
        {
            _frames.Enqueue(frame);
            _latestTime = Math.Max(_latestTime, frame.TimeSeconds);

            var cutoff = _latestTime - WindowSeconds;
            while (_frames.Count > 0 && _frames.Peek().TimeSeconds < cutoff)
                _frames.Dequeue();
        }
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
            return (count - 1) / (last - first);
        }
    }

    public static string Format(double? fps)
        => fps is { } value ? value.ToString("0", CultureInfo.InvariantCulture) : "--";
}
