using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Channels;
using FpsOverlay.Core;
using FpsOverlay.Core.Logging;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>
/// Ghi log FPS tự động theo từng buổi chơi: mỗi giây App gọi <see cref="Tick"/> (kể cả khi overlay đang ẩn).
/// Logic nhận biết buổi nằm ở <see cref="GameSessionTracker"/>; lớp này lo phần dính hệ điều hành
/// (cửa sổ phủ kín màn hình, tiến trình còn sống), lấy frametime, và đẩy dòng log sang luồng nền ghi file.
/// Lỗi IO chỉ ghi vết, không bao giờ làm sập app.
/// </summary>
sealed class SessionLogger : IDisposable
{
    /// <summary>Luồng nền xả file mỗi chừng này; mất điện/sập app thì mất tối đa ngần ấy log.</summary>
    static readonly TimeSpan FlushEvery = TimeSpan.FromSeconds(10);

    /// <summary>Documents\FpsOverlay\Logs của người dùng (app chạy quyền Admin nhưng cùng tài khoản).</summary>
    public static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FpsOverlay", "Logs");

    /// <summary>Buổi đang ghi: tóm tắt, hàng đợi dòng log cho luồng nền, và con trỏ frametime riêng.</summary>
    sealed class ActiveSession
    {
        public readonly GameSession Info;
        public readonly SessionSummary Summary = new();
        public readonly Channel<string> Rows = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        // Nội dung file tóm tắt, dựng trên luồng UI khi buổi kết thúc rồi giao cho luồng nền ghi.
        public readonly TaskCompletionSource<string> SummaryText = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly Task Writer;
        public double Cursor = double.NegativeInfinity;   // mọi frametime chỉ được lấy đúng một lần
        public double LastTickAt;
        public double LastFrameAt;                        // lần cuối game còn vẽ: để tính thời lượng, bỏ đoạn chờ alt-tab

        public ActiveSession(GameSession info, string stem, double now)
        {
            Info = info;
            LastTickAt = LastFrameAt = now;
            var rows = Rows.Reader;
            var summaryText = SummaryText.Task;
            Writer = Task.Run(() => RunWriter(stem, rows, summaryText));
        }
    }

    readonly List<Task> _writers = [];   // luồng ghi chưa xong, để Dispose chờ
    GameSessionTracker _tracker = new();
    readonly List<double> _frameTimes = [];
    ActiveSession? _active;
    AppLanguage _language = AppLanguage.Vietnamese;   // ngôn ngữ của lần Tick gần nhất, dùng cho tóm tắt khi Dispose
    (int Pid, string? Name) _nameCache;

    /// <param name="now">Đồng hồ giây đơn điệu của App.</param>
    /// <param name="enabled">Đang bật ghi log (và PresentMon đang chạy); tắt thì kết thúc buổi hiện tại.</param>
    /// <param name="candidates">Tiến trình có thể đang vẽ ở phía trước, xem <see cref="ForegroundProcess.GetCandidates"/>.</param>
    public void Tick(double now, bool enabled, FpsCalculator calculator, FrameTimeHistory history,
        IReadOnlyList<int> candidates, SensorSnapshot sensors, AppLanguage language)
    {
        _language = language;
        if (!enabled)
        {
            EndSession();
            _tracker = new GameSessionTracker();   // bật lại thì đếm 10 giây từ đầu
            return;
        }

        // Tiến trình đang đo = tiến trình đầu tiên trong danh sách có FPS.
        int? pid = null;
        foreach (var candidate in candidates)
            if (calculator.GetFps(candidate) is not null) { pid = candidate; break; }

        bool covers = pid is not null && ForegroundCoversMonitor();
        // Tên chỉ cần khi sắp bắt đầu một buổi mới.
        string? name = pid is { } p && covers && p != _active?.Info.ProcessId ? ProcessNameOf(p) : null;
        bool alive = _active is null || IsAlive(_active.Info.ProcessId);

        foreach (var change in _tracker.Tick(new SessionInput(now, pid, name, covers, pid is not null, alive)))
        {
            if (change.Kind == SessionChangeKind.Ended)
                EndSession();
            else
                StartSession(change.Session, history, now);
        }

        if (_active is { } active)
            WriteSecond(active, now, history, sensors);
    }

    void StartSession(GameSession session, FrameTimeHistory history, double now)
    {
        // Chỉ ghép tên; chọn đường dẫn không trùng và mọi thao tác file làm ở luồng nền.
        var stem = SessionLog.FileStem(session.ProcessName, DateTime.Now);
        var active = _active = new ActiveSession(session, stem, now);
        _writers.RemoveAll(t => t.IsCompleted);
        _writers.Add(active.Writer);

        // Khung của 10 giây chờ trước khi buổi bắt đầu chưa tính vào buổi: lấy bỏ để con trỏ nhảy tới hiện tại.
        history.CopyFrameTimes(session.ProcessId, ref active.Cursor, _frameTimes);
        _frameTimes.Clear();
    }

    // Một dòng cho giây vừa qua: FPS = số khung mới / thời gian trôi qua kể từ lần Tick trước.
    void WriteSecond(ActiveSession a, double now, FrameTimeHistory history, SensorSnapshot sensors)
    {
        double elapsed = now - a.LastTickAt;
        if (elapsed <= 0) return;   // tick bắt đầu buổi
        a.LastTickAt = now;

        _frameTimes.Clear();
        int frames = history.CopyFrameTimes(a.Info.ProcessId, ref a.Cursor, _frameTimes);
        double? fps = frames > 0 ? frames / elapsed : null;   // không có khung nào thì để trống, không tính là 0 FPS
        if (frames > 0) a.LastFrameAt = now;

        a.Summary.AddSecond(fps, sensors);
        a.Summary.AddFrameTimes(_frameTimes);
        a.Rows.Writer.TryWrite(SessionLog.FormatRow(DateTimeOffset.Now, fps, history.OnePercentLow(a.Info.ProcessId), sensors));
    }

    // Đóng hàng đợi và giao nội dung tóm tắt cho luồng nền; không chờ, không đụng file ở luồng UI.
    void EndSession()
    {
        if (_active is not { } a) return;
        _active = null;

        var duration = TimeSpan.FromSeconds(Math.Max(0, a.LastFrameAt - a.Info.StartedAt));
        a.SummaryText.TrySetResult(a.Summary.Format(_language, duration));
        a.Rows.Writer.TryComplete();
    }
    // Chạy trên luồng nền. Gặp lỗi IO thì bỏ file này nhưng vẫn rút hết hàng đợi để không phình bộ nhớ.
    static async Task RunWriter(string stem, ChannelReader<string> rows, Task<string> summaryText)
    {
        string? path = null;
        StreamWriter? writer = null;
        try
        {
            Directory.CreateDirectory(LogDirectory);
            // Không trùng cả file CSV lẫn file tóm tắt của buổi cũ.
            path = SessionLog.UniquePath(LogDirectory, stem, ".csv", p => File.Exists(p) || File.Exists(SessionLog.SummaryPath(p)));
            writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            writer.WriteLine(SessionLog.Header);
            var sinceFlush = Stopwatch.StartNew();
            await foreach (var row in rows.ReadAllAsync())
            {
                writer.WriteLine(row);
                if (sinceFlush.Elapsed < FlushEvery) continue;
                writer.Flush();
                sinceFlush.Restart();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không ghi được log {stem}: {ex.Message}");
            try { await foreach (var _ in rows.ReadAllAsync()) { } } catch { }
        }
        finally
        {
            try { writer?.Dispose(); }   // xả nốt phần còn lại
            catch (Exception ex) { Trace.TraceWarning($"Không đóng được log {stem}: {ex.Message}"); }
        }

        if (path is null) return;
        try
        {
            File.WriteAllText(SessionLog.SummaryPath(path), await summaryText, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không ghi được file tóm tắt {stem}: {ex.Message}");
        }
    }
    /// <summary>Thoát app: kết thúc buổi đang ghi rồi chờ (có hạn) các luồng ghi xả file và viết tóm tắt.</summary>
    public void Dispose()
    {
        EndSession();
        try { Task.WaitAll(_writers.ToArray(), TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { Trace.TraceWarning($"Luồng ghi log lỗi: {ex.Message}"); }
    }

    // ---------- Hệ điều hành ----------

    static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)   // không còn tiến trình nào mang pid này
        {
            return false;
        }
        catch (Exception)   // không đọc được trạng thái thì coi như còn sống, đừng cắt buổi oan
        {
            return true;
        }
    }

    string? ProcessNameOf(int pid)
    {
        if (_nameCache.Pid == pid) return _nameCache.Name;
        string? name = null;
        try
        {
            using var process = Process.GetProcessById(pid);
            name = process.ProcessName;
        }
        catch (Exception) { }
        _nameCache = (pid, name);
        return name;
    }

    /// <summary>Cửa sổ phía trước phủ kín màn hình chứa nó (game toàn màn hình hoặc borderless); màn hình nền không tính.</summary>
    static bool ForegroundCoversMonitor()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == NativeMethods.GetShellWindow())
            return false;

        var className = new StringBuilder(64);
        NativeMethods.GetClassName(hwnd, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW")
            return false;

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetWindowRect(hwnd, out var window) || !NativeMethods.GetMonitorInfo(monitor, ref info))
            return false;

        var m = info.rcMonitor;
        return window.Left <= m.Left && window.Top <= m.Top && window.Right >= m.Right && window.Bottom >= m.Bottom;
    }
}