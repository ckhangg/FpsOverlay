using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using FpsOverlay.Core;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

public partial class App : Application
{
    readonly FpsCalculator _calculator = new();
    readonly Stopwatch _previewClock = Stopwatch.StartNew();
    SettingsService? _settings;
    PresentMonRunner? _runner;
    OverlayWindow? _overlay;
    TrayIcon? _tray;

    Mutex? _singleInstance;

    /// <summary>Khi bật, overlay chạy số FPS mẫu thay vì FPS thật (cửa sổ cài đặt bật/tắt).</summary>
    public bool IsPreviewing { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hai bản chạy cùng lúc sẽ tranh nhau một phiên PresentMon và làm bản kia dừng.
        _singleInstance = new Mutex(true, @"Local\FpsOverlay.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("FPS Overlay đang chạy rồi (xem biểu tượng ở khay hệ thống).", "FPS Overlay",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        _settings = new SettingsService(SettingsStore.DefaultPath);
        _overlay = new OverlayWindow();
        _overlay.Apply(_settings.Current);
        _settings.Changed += _overlay.Apply;
        _tray = new TrayIcon();
        _tray.ToggleRequested += ToggleOverlay;
        _tray.ExitRequested += Shutdown;

        _runner = new PresentMonRunner();
        _runner.Exited += stderr => Dispatcher.BeginInvoke(() => Fail(
            "PresentMon đã dừng bất ngờ nên không đo được FPS.\n" +
            "Hãy chắc rằng app đang chạy với quyền Administrator." +
            (stderr.Length > 0 ? $"\n\nChi tiết:\n{stderr}" : "")));

        try
        {
            _runner.Start(_calculator.Add);
        }
        catch (Exception ex)
        {
            Fail($"Không khởi động được PresentMon.\n" +
                 $"File PresentMon.exe phải nằm cùng thư mục với FpsOverlay.exe.\n\nChi tiết: {ex.Message}");
            return;
        }

        _overlay.Show();
        CompositionTarget.Rendering += OnRendering;
    }

    // Vẽ lại con số mỗi khung hình của WPF: cập nhật nhanh nhất có thể.
    void OnRendering(object? sender, EventArgs e)
    {
        if (_overlay is not { IsVisible: true } || _settings is null)
            return;
        double? fps;
        if (IsPreviewing)
            fps = PreviewFps.At(_previewClock.Elapsed.TotalSeconds);
        else
            fps = ForegroundProcess.GetId() is { } id ? _calculator.GetFps(id) : null;
        _overlay.SetFps(fps, _settings.Current);
    }

    void ToggleOverlay()
    {
        if (_overlay is null) return;
        if (_overlay.IsVisible) _overlay.Hide();
        else _overlay.Show();
        _tray?.SetOverlayVisible(_overlay.IsVisible);
    }

    void Fail(string message)
    {
        CompositionTarget.Rendering -= OnRendering;
        _overlay?.Hide();   // không để con số cũ treo trên game
        // DefaultDesktopOnly: hộp thoại nổi lên trên cả game borderless
        MessageBox.Show(message, "FPS Overlay", MessageBoxButton.OK, MessageBoxImage.Error,
            MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _tray?.Dispose();
        _runner?.Dispose();
        _settings?.Dispose();   // ghi nốt cài đặt còn đang hẹn
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
