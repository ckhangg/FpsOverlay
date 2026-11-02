using System.Windows;
using System.Windows.Media;
using FpsOverlay.Core;

namespace FpsOverlay;

public partial class App : Application
{
    readonly FpsCalculator _calculator = new();
    PresentMonRunner? _runner;
    OverlayWindow? _overlay;
    TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _overlay = new OverlayWindow();
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
        if (_overlay is not { IsVisible: true })
            return;
        var pid = ForegroundProcess.GetId();
        _overlay.SetText(FpsCalculator.Format(pid is { } id ? _calculator.GetFps(id) : null));
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
        MessageBox.Show(message, "FPS Overlay", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _tray?.Dispose();
        _runner?.Dispose();
        base.OnExit(e);
    }
}
