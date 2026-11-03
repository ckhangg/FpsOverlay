using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FpsOverlay.Core;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

public partial class App : Application
{
    // Mốc thời gian PresentMon tính từ lúc nó chạy nên mỗi lần chạy lại phải dùng bộ mới (xem StartPresentMon).
    FpsCalculator _calculator = new();
    FrameTimeHistory _history = new();
    readonly SessionLogger _logger = new();
    readonly Stopwatch _previewClock = Stopwatch.StartNew();
    readonly AutostartService _autostart = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    // Thời điểm (giây theo _clock) PresentMon được chạy/chạy lại; null = đang chủ động dừng, không cảnh báo.
    double? _presentMonStartedAt;
    bool _stalled;
    DispatcherTimer? _watchdog;
    DispatcherTimer? _statsTimer;
    DispatcherTimer? _logTimer;
    SensorService? _sensors;
    SettingsService? _settings;
    PresentMonRunner? _runner;
    OverlayWindow? _overlay;
    TrayIcon? _tray;
    SettingsWindow? _settingsWindow;
    // Hộp báo lỗi đang mở mà bấm tiếp ở khay thì bỏ qua, không chạy schtasks và hiện hộp thứ hai.
    bool _settingAutostart;

    Mutex? _singleInstance;

    /// <summary>Overlay chạy số FPS mẫu thay vì FPS thật, chỉ khi cửa sổ cài đặt đang mở và là cửa sổ đang dùng.</summary>
    public bool IsPreviewing => _settingsWindow is { IsActive: true };

    AppLanguage Language => _settings?.Current.Language ?? AppLanguage.Vietnamese;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hai bản chạy cùng lúc sẽ tranh nhau một phiên PresentMon và làm bản kia dừng.
        _singleInstance = new Mutex(true, @"Local\FpsOverlay.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            // Chỉ đọc ngôn ngữ, không mở SettingsService (bản này không được ghi file cài đặt).
            var language = SettingsStore.Load(SettingsStore.DefaultPath).Language;
            MessageBox.Show(Loc.Get(StringKey.AlreadyRunning, language), "FPS Overlay",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var settings = _settings = new SettingsService(SettingsStore.DefaultPath);
        var overlay = _overlay = new OverlayWindow();
        overlay.Apply(settings.Current);
        settings.Changed += overlay.Apply;
        settings.Changed += _ => UpdateStats();   // bật/tắt mục thì hiện ngay, không chờ nhịp một giây
        settings.Changed += _ => ApplyResourceState();   // bật/tắt ghi log lúc đang ẩn thì áp dụng ngay
        _sensors = new SensorService();
        _sensors.Start();   // luồng nền đọc mỗi giây; dừng khi ẩn overlay mà không ghi log (ApplyResourceState)
        var tray = _tray = new TrayIcon();
        tray.Apply(settings.Current, overlayVisible: true);
        tray.ToggleRequested += ToggleOverlay;
        tray.SettingsRequested += OpenSettings;
        tray.CornerRequested += corner => settings.Update(s => s with { Corner = corner });
        tray.AutostartRequested += SetAutostart;
        tray.ExitRequested += Shutdown;
        // Đổi ngôn ngữ hay góc (kể cả từ cửa sổ cài đặt) thì menu khay đổi theo ngay.
        settings.Changed += s =>
        {
            tray.Apply(s, overlay.IsVisible);
            if (_stalled) tray.SetWarning(Loc.Get(StringKey.PresentMonNoData, s.Language));   // đổi chữ theo ngôn ngữ mới
        };
        _autostart.Changed += tray.SetAutostart;
        // Đọc lại mỗi lần mở menu: tác vụ có thể bị xoá/sửa trong Task Scheduler lúc app đang chạy.
        tray.MenuOpening += () => _ = _autostart.RefreshAsync();
        _ = _autostart.RefreshAsync();

        if (!StartPresentMon())
            return;
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _watchdog.Tick += (_, _) => CheckPresentMon();
        _watchdog.Start();
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statsTimer.Tick += (_, _) => UpdateStats();
        _statsTimer.Start();
        // Nhịp ghi log chạy riêng vì nhịp trên bỏ qua khi overlay ẩn mà log vẫn phải chạy.
        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _logTimer.Tick += (_, _) => LogTick();
        _logTimer.Start();

        _overlay.Show();
        CompositionTarget.Rendering += OnRendering;
    }

    // Chạy PresentMon mới (bộ tính FPS và lịch sử frametime làm mới theo). Trả về false nếu không chạy được (đã báo lỗi và thoát).
    bool StartPresentMon()
    {
        // Mỗi runner ghi vào bộ riêng của nó (một closure mỗi lần chạy): runner cũ không thể đẩy khung vào bộ mới.
        var calculator = new FpsCalculator();
        var history = new FrameTimeHistory();
        var runner = new PresentMonRunner();
        // Dispose chủ động không phát Exited (runner tự bỏ qua khi đã Dispose), nên chỉ lần thoát bất ngờ mới tới đây.
        runner.Exited += stderr => Dispatcher.BeginInvoke(() => Fail(
            Loc.Get(StringKey.PresentMonStopped, Language) +
            (stderr.Length > 0 ? $"\n\n{Loc.Get(StringKey.Details, Language)}:\n{stderr}" : "")));

        try
        {
            runner.Start(frame =>
            {
                calculator.Add(frame);
                history.Add(frame);
            });
        }
        catch (Exception ex)
        {
            runner.Dispose();
            Fail($"{Loc.Get(StringKey.PresentMonStartFailed, Language)}\n\n{Loc.Get(StringKey.Details, Language)}: {ex.Message}");
            return false;
        }

        _runner = runner;
        _calculator = calculator;
        _history = history;
        _presentMonStartedAt = _clock.Elapsed.TotalSeconds;
        return true;
    }

    // Dừng PresentMon có chủ ý: không còn dữ liệu là bình thường nên tắt cảnh báo "không gửi dữ liệu".
    void StopPresentMon()
    {
        _runner?.Dispose();
        _runner = null;
        _presentMonStartedAt = null;
        CheckPresentMon();
    }

    // Overlay đang hiện hoặc đang ghi log thì cần PresentMon và cảm biến; ẩn mà không ghi log thì dừng cả hai cho đỡ tốn máy.
    void ApplyResourceState()
    {
        if (_overlay is null || _settings is null || _sensors is null) return;
        if (_overlay.IsVisible || _settings.Current.LoggingEnabled)
        {
            _sensors.Start();
            if (_runner is null) StartPresentMon();
        }
        else
        {
            StopPresentMon();
            _sensors.Stop();
        }
    }

    // Mỗi giây, kể cả khi overlay ẩn: đẩy trạng thái hiện tại cho bộ ghi log.
    void LogTick()
    {
        if (_settings is null || _sensors is null) return;
        _logger.Tick(_clock.Elapsed.TotalSeconds, _settings.Current.LoggingEnabled && _runner is not null,
            _calculator, _history, ForegroundProcess.GetCandidates(), _sensors.Latest, Language);
    }

    // Mỗi giây kiểm tra PresentMon có đang gửi dữ liệu không; chỉ đụng vào khay khi trạng thái đổi.
    void CheckPresentMon()
    {
        bool stalled = _presentMonStartedAt is { } startedAt &&
            PresentMonHealth.IsStalled(_clock.Elapsed.TotalSeconds - startedAt, _calculator.SecondsSinceAnyFrame());
        if (stalled == _stalled) return;
        _stalled = stalled;
        _tray?.SetWarning(stalled ? Loc.Get(StringKey.PresentMonNoData, Language) : null);
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
            fps = _calculator.GetFps(ForegroundProcess.GetCandidates());
        _overlay.SetFps(fps, _settings.Current);
    }

    // Mỗi giây (và khi cài đặt đổi): 1% low + cảm biến lên overlay. Xem trước thì dùng số mẫu.
    void UpdateStats()
    {
        _settingsWindow?.RefreshSensorNote();   // PawnIO có thể vừa được cài khi cửa sổ cài đặt đang mở
        if (_overlay is not { IsVisible: true } || _settings is null || _sensors is null)
            return;
        double? onePercentLow;
        SensorSnapshot sensors;
        if (IsPreviewing)
        {
            onePercentLow = PreviewFps.At(_previewClock.Elapsed.TotalSeconds) * 0.8;
            sensors = new SensorSnapshot(GpuLoad: 67, GpuTemp: 71, CpuLoad: 34, CpuTemp: 65, RamUsedGb: 9.2);
        }
        else
        {
            onePercentLow = _history.OnePercentLow(ForegroundProcess.GetCandidates());
            sensors = _sensors.Latest;
        }
        _overlay.SetStats(onePercentLow, sensors, _settings.Current);
    }

    void ToggleOverlay()
    {
        if (_overlay is null || _settings is null) return;
        // Ẩn thì ngừng đăng ký vẽ mỗi khung hình; hiện lại thì đăng ký một lần (luôn gỡ trước để không bao giờ trùng).
        CompositionTarget.Rendering -= OnRendering;
        if (_overlay.IsVisible) _overlay.Hide();
        else
        {
            _overlay.Show();
            CompositionTarget.Rendering += OnRendering;
        }
        _tray?.Apply(_settings.Current, _overlay.IsVisible);
        ApplyResourceState();
    }

    // Chỉ một cửa sổ cài đặt; mở lại thì đưa cửa sổ đang mở lên trước.
    void OpenSettings()
    {
        if (_settings is null) return;
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        _ = _autostart.RefreshAsync();
        _settingsWindow = new SettingsWindow(_settings, _autostart, SetAutostart, () => _sensors?.CpuTemperatureAvailable ?? true);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;   // đóng bằng cách nào cũng về FPS thật
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    void SetAutostart(bool enable)
    {
        if (_settingAutostart) return;
        _settingAutostart = true;
        try
        {
            if (_autostart.TrySet(enable, out var error))
                return;
            var message = Loc.Get(StringKey.AutostartFailed, Language) +
                          (error.Length > 0 ? $"\n\n{Loc.Get(StringKey.Details, Language)}: {error}" : "");
            MessageBox.Show(message, "FPS Overlay", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _settingAutostart = false;
        }
    }

    void Fail(string message)
    {
        _watchdog?.Stop();
        _statsTimer?.Stop();
        _logTimer?.Stop();
        CompositionTarget.Rendering -= OnRendering;
        _overlay?.Hide();   // không để con số cũ treo trên game
        // DefaultDesktopOnly: hộp thoại nổi lên trên cả game borderless
        MessageBox.Show(message, "FPS Overlay", MessageBoxButton.OK, MessageBoxImage.Error,
            MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watchdog?.Stop();
        _statsTimer?.Stop();
        _logTimer?.Stop();
        CompositionTarget.Rendering -= OnRendering;
        _logger.Dispose();   // ghi nốt log và tóm tắt trước khi dừng PresentMon
        _tray?.Dispose();
        _sensors?.Dispose();
        _runner?.Dispose();
        _settings?.Dispose();   // ghi nốt cài đặt còn đang hẹn
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
