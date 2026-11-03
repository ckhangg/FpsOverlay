namespace FpsOverlay.Core.Logging;

/// <summary>Trạng thái mỗi giây đưa vào <see cref="GameSessionTracker"/>.</summary>
/// <param name="Now">Đồng hồ giây (đơn điệu tăng).</param>
/// <param name="ProcessId">Tiến trình đang đo FPS ở phía trước (null nếu không có).</param>
/// <param name="CoversMonitor">Cửa sổ phía trước phủ kín màn hình chứa nó.</param>
/// <param name="HasFps">Tiến trình đó đang có FPS.</param>
/// <param name="SessionProcessAlive">Tiến trình của buổi hiện tại còn sống (true nếu chưa có buổi).</param>
public readonly record struct SessionInput(double Now, int? ProcessId, string? ProcessName, bool CoversMonitor, bool HasFps, bool SessionProcessAlive);

public sealed record GameSession(int ProcessId, string ProcessName, double StartedAt);

public enum SessionChangeKind { Started, Ended }

public readonly record struct SessionChange(SessionChangeKind Kind, GameSession Session);

/// <summary>
/// Nhận biết "buổi chơi": một tiến trình phủ kín màn hình và có FPS liên tục hơn <see cref="StartAfterSeconds"/> giây thì bắt đầu buổi.
/// Buổi kết thúc khi tiến trình thoát, hoặc khi nó rời trước (alt-tab) quá <see cref="EndAfterSeconds"/> giây,
/// hoặc khi một game khác đủ điều kiện bắt đầu. Gọi <see cref="Tick"/> mỗi giây.
/// </summary>
public sealed class GameSessionTracker
{
    public const double StartAfterSeconds = 10;
    public const double EndAfterSeconds = 60;

    static readonly SessionChange[] None = [];

    double _lastActive;                              // lần cuối tiến trình của buổi còn đủ điều kiện
    (int Pid, string Name, double Since)? _candidate;   // tiến trình đang đếm 10 giây

    public GameSession? Current { get; private set; }

    public IReadOnlyList<SessionChange> Tick(SessionInput input)
    {
        List<SessionChange>? changes = null;
        void Add(SessionChangeKind kind, GameSession session) => (changes ??= []).Add(new SessionChange(kind, session));

        bool qualifies = input.ProcessId is not null && input.CoversMonitor && input.HasFps;

        if (Current is { } current)
        {
            if (!input.SessionProcessAlive)
                End();
            else if (qualifies && input.ProcessId == current.ProcessId)
                _lastActive = input.Now;
            else if (!qualifies && input.Now - _lastActive > EndAfterSeconds)
                End();
        }

        if (!qualifies || input.ProcessId == Current?.ProcessId)
        {
            _candidate = null;
        }
        else
        {
            int pid = input.ProcessId!.Value;
            if (_candidate is not { } c || c.Pid != pid)
                _candidate = (pid, input.ProcessName ?? "", input.Now);
            else if (input.Now - c.Since > StartAfterSeconds)
            {
                // Game khác đủ điều kiện lúc buổi cũ còn trong thời gian chờ: thay thế (Ended rồi Started).
                if (Current is not null) End();
                Current = new GameSession(pid, c.Name, input.Now);
                _lastActive = input.Now;
                _candidate = null;
                Add(SessionChangeKind.Started, Current);
            }
        }

        return changes ?? (IReadOnlyList<SessionChange>)None;

        void End()
        {
            Add(SessionChangeKind.Ended, Current!);
            Current = null;
        }
    }
}