using System.Diagnostics;
using System.Windows.Threading;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>
/// Giữ cài đặt hiện tại, báo khi đổi và ghi file trễ 500 ms (kéo thanh trượt liên tục chỉ ghi một lần).
/// Dùng trên luồng UI.
/// </summary>
sealed class SettingsService : IDisposable
{
    static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);
    /// <summary>Ghi lỗi (file đang bị khoá...) thì thử lại sau khoảng này, cho tới khi được.</summary>
    static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    readonly string _path;
    readonly DispatcherTimer _saveTimer;
    bool _dirty;

    public SettingsService(string path)
    {
        _path = path;
        Current = SettingsStore.Load(path);
        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    public OverlaySettings Current { get; private set; }

    /// <summary>Phát trên luồng UI mỗi khi <see cref="Current"/> đổi.</summary>
    public event Action<OverlaySettings>? Changed;

    public void Update(Func<OverlaySettings, OverlaySettings> change)
    {
        var next = change(Current).Normalize();
        if (next == Current)
            return;

        Current = next;

        // Hẹn ghi trước khi báo: một chỗ nghe Changed ném lỗi cũng không làm mất lần ghi. Mỗi lần đổi mới dời hẹn.
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Interval = SaveDelay;
        _saveTimer.Start();

        Changed?.Invoke(next);
    }

    void SaveNow()
    {
        _saveTimer.Stop();
        try
        {
            SettingsStore.Save(_path, Current);
            _dirty = false;
        }
        catch (Exception ex)
        {
            // Gọi từ timer: ném ra là sập app. App vẫn chạy với cài đặt trong bộ nhớ và thử ghi lại sau.
            Trace.WriteLine($"FpsOverlay: không ghi được cài đặt vào '{_path}': {ex.Message}");
            _saveTimer.Interval = RetryDelay;
            _saveTimer.Start();
        }
    }

    public void Dispose()
    {
        if (_dirty)
            SaveNow();
        _saveTimer.Stop();   // lần thoát app này không thử lại nữa
    }
}
