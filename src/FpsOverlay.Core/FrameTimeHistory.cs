namespace FpsOverlay.Core;

/// <summary>
/// Lưu frametime (khoảng cách giữa hai khung liên tiếp của cùng tiến trình) trong cửa sổ trượt
/// <c>windowSeconds</c> giây gần nhất, tính theo thời điểm khung mới nhất của mọi tiến trình, để tính 1% low.
/// An toàn khi <see cref="Add"/> gọi từ luồng đọc và các hàm còn lại từ luồng khác.
/// </summary>
public sealed class FrameTimeHistory
{
    /// <summary>Cần ít nhất chừng này giây dữ liệu thì 1% low mới đáng tin.</summary>
    public const double MinSpanSeconds = 2.0;

    readonly struct Entry(double time, double frameTime)
    {
        public readonly double Time = time;
        public readonly double FrameTime = frameTime;
    }

    sealed class PidState
    {
        public readonly Queue<Entry> Entries = new();
        public double LastTime = double.MinValue;
    }

    readonly Dictionary<int, PidState> _pids = new();
    readonly object _lock = new();
    readonly double _windowSeconds;
    double _latestTime = double.MinValue;
    double _lastSweepTime = double.MinValue;
    double[] _sortBuffer = new double[1024]; // dùng lại giữa các lần gọi để không cấp phát

    public FrameTimeHistory(double windowSeconds = 10)
    {
        _windowSeconds = windowSeconds;
    }

    /// <summary>Thời điểm khung mới nhất đã nhận (mọi tiến trình); <see cref="double.MinValue"/> nếu chưa có.</summary>
    public double LatestTimeSeconds
    {
        get { lock (_lock) return _latestTime; }
    }

    public void Add(FrameSample frame)
    {
        lock (_lock)
        {
            _latestTime = Math.Max(_latestTime, frame.TimeSeconds);

            if (!_pids.TryGetValue(frame.ProcessId, out var state))
                _pids[frame.ProcessId] = state = new PidState();

            // Khung đầu tiên của tiến trình chưa có khung trước nên chưa có frametime.
            if (state.LastTime != double.MinValue)
                state.Entries.Enqueue(new Entry(frame.TimeSeconds, frame.TimeSeconds - state.LastTime));
            state.LastTime = frame.TimeSeconds;

            Trim(state);

            // Quét định kỳ (mỗi > 1 giây thời gian khung): bỏ tiến trình đã ngừng vẽ quá một cửa sổ,
            // để bộ nhớ không phình theo số tiến trình đã qua và pid dùng lại không dính khoảng nghỉ cũ.
            if (_latestTime - _lastSweepTime > 1.0)
            {
                _lastSweepTime = _latestTime;
                var cutoff = _latestTime - _windowSeconds;
                List<int>? stale = null;
                foreach (var kv in _pids)
                    if (kv.Value.LastTime < cutoff)
                        (stale ??= new()).Add(kv.Key);
                if (stale != null)
                    foreach (var pid in stale)
                        _pids.Remove(pid);
            }
        }
    }

    // Gọi khi đã giữ khoá.
    void Trim(PidState state)
    {
        var cutoff = _latestTime - _windowSeconds;
        while (state.Entries.Count > 0 && state.Entries.Peek().Time < cutoff)
            state.Entries.Dequeue();
    }

    /// <summary>1% low (fps) của tiến trình, hoặc null nếu dữ liệu còn trong cửa sổ trải dài dưới 2 giây.</summary>
    public double? OnePercentLow(int processId)
    {
        lock (_lock)
        {
            if (!_pids.TryGetValue(processId, out var state))
                return null;
            Trim(state);

            int n = state.Entries.Count;
            if (n == 0)
                return null;

            double first = 0, last = 0;
            if (_sortBuffer.Length < n)
                _sortBuffer = new double[Math.Max(n, _sortBuffer.Length * 2)];

            int i = 0;
            foreach (var e in state.Entries)
            {
                if (i == 0)
                    first = e.Time;
                last = e.Time;
                _sortBuffer[i++] = e.FrameTime;
            }

            if (last - first < MinSpanSeconds)
                return null;

            Array.Sort(_sortBuffer, 0, n);
            return 1 / _sortBuffer[Rank(n)];
        }
    }

    /// <summary>1% low của tiến trình đầu tiên trong danh sách có kết quả, hoặc null.</summary>
    public double? OnePercentLow(IReadOnlyList<int> processIds)
    {
        for (int i = 0; i < processIds.Count; i++)
            if (OnePercentLow(processIds[i]) is { } low)
                return low;
        return null;
    }

    /// <summary>
    /// Thêm vào <paramref name="into"/> các frametime của tiến trình có thời điểm khung &gt; <paramref name="cursor"/>,
    /// rồi đặt <paramref name="cursor"/> bằng thời điểm khung cuối cùng đã chép (giữ nguyên nếu không có). Trả về số đã thêm.
    /// Làm trọn trong một lần giữ khoá nên gọi định kỳ với cùng một biến cursor (khởi đầu <see cref="double.NegativeInfinity"/>)
    /// thì mỗi frametime được lấy đúng một lần.
    /// </summary>
    public int CopyFrameTimes(int processId, ref double cursor, List<double> into)
    {
        lock (_lock)
        {
            if (!_pids.TryGetValue(processId, out var state))
                return 0;
            int added = 0;
            double newCursor = cursor;
            foreach (var e in state.Entries)
            {
                if (e.Time <= cursor)
                    continue;
                into.Add(e.FrameTime);
                newCursor = e.Time;
                added++;
            }
            cursor = newCursor;
            return added;
        }
    }

    /// <summary>Phân vị 99 kiểu nearest-rank của frametime, đổi ra fps; null nếu rỗng.</summary>
    public static double? OnePercentLowOf(IReadOnlyList<double> frameTimes)
    {
        int n = frameTimes.Count;
        if (n == 0)
            return null;
        var sorted = new double[n];
        for (int i = 0; i < n; i++)
            sorted[i] = frameTimes[i];
        Array.Sort(sorted);
        return 1 / sorted[Rank(n)];
    }

    // Chỉ số nearest-rank của phân vị 99 trong mảng đã sắp tăng dần: ceil(0.99·n) − 1.
    static int Rank(int n) => (int)Math.Ceiling(0.99 * n) - 1;
}
