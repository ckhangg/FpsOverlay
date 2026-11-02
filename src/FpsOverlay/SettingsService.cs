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

    readonly string _path;
    readonly DispatcherTimer _saveTimer;

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
        Changed?.Invoke(next);

        // Mỗi lần đổi mới dời hẹn ghi.
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    void SaveNow()
    {
        _saveTimer.Stop();
        try
        {
            SettingsStore.Save(_path, Current);
        }
        catch (Exception ex)
        {
            // Ghi cài đặt chỉ là "cố gắng": lỗi gì cũng bỏ qua (gọi từ timer, ném ra là sập app),
            // app vẫn chạy với cài đặt trong bộ nhớ.
            Trace.WriteLine($"FpsOverlay: không ghi được cài đặt vào '{_path}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_saveTimer.IsEnabled)
            SaveNow();
    }
}
