using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FpsOverlay.Core;
using FpsOverlay.Core.GameOnly;
using FpsOverlay.Core.Install;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.PawnIo;
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
    // Dò cửa sổ phía trước để chỉ hiện overlay khi đang chơi game (xem UpdateVisibility).
    DispatcherTimer? _gameTimer;
    // Người dùng bấm "Ẩn FPS" ở khay: ẩn bất kể đang chơi gì.
    bool _hiddenByUser;
    // Overlay đang (được quyết định là) hiện; chỉ SetOverlayShown đổi. Lúc khởi động overlay hiện, nhịp dò đầu tiên quyết định.
    bool _shown = true;
    // Tên exe của app (không phải app này/desktop) ở phía trước gần nhất: cho hai mục Luôn hiện / Không bao giờ hiện ở khay.
    string? _lastAppName;
    string? _lastAppPath;
    // Đường dẫn exe các tiến trình của cửa sổ phía trước; dùng lại giữa các nhịp dò.
    readonly List<string?> _foregroundPaths = [];
    SensorService? _sensors;
    SettingsService? _settings;
    PresentMonRunner? _runner;
    OverlayWindow? _overlay;
    // Theo dõi đợt nóng của GPU và CPU để báo động (nháy, tiếng).
    readonly HeatAlarm _gpuAlarm = new(), _cpuAlarm = new();
    TrayIcon? _tray;
    SettingsWindow? _settingsWindow;
    // Hộp báo lỗi đang mở mà bấm tiếp ở khay thì bỏ qua, không chạy schtasks và hiện hộp thứ hai.
    bool _settingAutostart;
    // Lần cài PawnIO đang chạy (từ nút trong cài đặt); null = không cài.
    Task<PawnIoResult>? _pawnIoInstall;

    Mutex? _singleInstance;
    // Thư mục chứa PresentMon.exe đã bung (đặt lúc khởi động, trước StartPresentMon).
    string? _toolDirectory;

    /// <summary>Overlay chạy số FPS mẫu thay vì FPS thật, chỉ khi cửa sổ cài đặt đang mở và là cửa sổ đang dùng.</summary>
    public bool IsPreviewing => _settingsWindow is { IsActive: true };

    AppLanguage Language => _settings?.Current.Language ?? AppLanguage.Vietnamese;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Cài/gỡ chạy trước mutex một bản chạy: bản cài có thể đang chạy (và đang giữ mutex).
        bool HasArg(string name) => e.Args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        if (HasArg("--uninstall"))
        {
            RunUninstall();
            Shutdown();
            return;
        }
        var noInstall = HasArg("--no-install");
        if (!noInstall && !Installer.IsRunningInstalled)
        {
            RunSetup();   // tự Shutdown khi xong hoặc huỷ
            return;
        }

        // Hai bản chạy cùng lúc sẽ tranh nhau một phiên PresentMon và làm bản kia dừng.
        _singleInstance = new Mutex(true, Installer.SingleInstanceMutexName, out var isFirst);
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
        // Bản setup vừa hỏi người dùng về PawnIO (tick hay không): nhớ lại để lần cập nhật sau không hỏi nữa.
        // Cờ này chỉ đến từ bản setup; Task Scheduler chạy bản cài không kèm tham số nào.
        if (HasArg(PawnIoSetup.OfferedArgument) && !settings.Current.PawnIoOffered)
            settings.Update(s => s with { PawnIoOffered = true });
        var overlay = _overlay = new OverlayWindow
        {
            PreviewWindow = () => IsPreviewing ? new System.Windows.Interop.WindowInteropHelper(_settingsWindow!).Handle : null,
        };
        overlay.Apply(settings.Current);
        settings.Changed += overlay.Apply;
        settings.Changed += _ => UpdateStats();   // bật/tắt mục thì hiện ngay, không chờ nhịp một giây
        settings.Changed += _ => ApplyResourceState();   // bật/tắt ghi log lúc đang ẩn thì áp dụng ngay
        settings.Changed += _ => UpdateVisibility();   // đổi danh sách app / chỉ-hiện-khi-chơi thì áp dụng ngay
        _sensors = new SensorService();
        _sensors.Start();   // luồng nền đọc mỗi giây; dừng khi ẩn overlay mà không ghi log (ApplyResourceState)
        var tray = _tray = new TrayIcon();
        tray.Apply(settings.Current, shownByUser: true);
        tray.ToggleRequested += ToggleOverlay;
        tray.SettingsRequested += OpenSettings;
        tray.CornerRequested += corner =>
        {
            if (overlay.IsMoving) overlay.EndMove(save: false);   // không để lần lưu vị trí kéo sau đè lên góc vừa chọn
            settings.Update(s => s.WithCorner(corner));
        };
        // Di chuyển overlay: mục khay bật/tắt (tắt = lưu); vị trí lưu là góc gần nhất + khoảng cách tới mép (DIP).
        // Overlay đang tự ẩn (không chơi game) thì hiện trước để có cái mà kéo.
        tray.MoveRequested += () =>
        {
            if (overlay.IsMoving) overlay.EndMove(save: true);
            else if (!_hiddenByUser)
            {
                if (!_shown) SetOverlayShown(true);
                overlay.BeginMove();
            }
        };
        overlay.MovingChanged += tray.SetMoving;
        overlay.MovingChanged += _ => UpdateVisibility();   // kéo xong ở desktop thì ẩn ngay
        tray.AlwaysShowRequested += () =>
        {
            if (_lastAppName is { } name) settings.Update(s => s.WithAlwaysShow(name));
        };
        tray.NeverShowRequested += () =>
        {
            if (_lastAppName is { } name) settings.Update(s => s.WithNeverShow(name));
        };
        overlay.PositionSaved += (corner, x, y) => settings.Update(s => s with { Corner = corner, OffsetX = x, OffsetY = y });
        tray.AutostartRequested += SetAutostart;
        tray.ExitRequested += Shutdown;
        // Đổi ngôn ngữ hay góc (kể cả từ cửa sổ cài đặt) thì menu khay đổi theo ngay.
        settings.Changed += s =>
        {
            tray.Apply(s, !_hiddenByUser);
            if (_stalled) tray.SetWarning(Loc.Get(StringKey.PresentMonNoData, s.Language));   // đổi chữ theo ngôn ngữ mới
        };
        tray.SetAutostartLocked(!_autostart.CanManage);
        _autostart.Changed += tray.SetAutostart;
        // Đọc lại mỗi lần mở menu: tác vụ có thể bị xoá/sửa trong Task Scheduler lúc app đang chạy.
        tray.MenuOpening += () =>
        {
            _ = _autostart.RefreshAsync();
            tray.SetAppTarget(_lastAppName);
        };
        _ = _autostart.RefreshAsync();

        // Lần cài trước dừng giữa chừng (sau khi chép exe) thì bổ sung lối tắt và mục gỡ cài đặt.
        Installer.RepairEntries();

        // Bản cài (nhiều file): PresentMon nằm sẵn cạnh exe trong payload, chỉ kiểm tra có. --no-install (single-file lúc
        // phát triển): bung bản nhúng vào thư mục chứa exe. Thiếu file hay thư mục chỉ đọc không được làm app chết im lặng.
        try
        {
            if (Installer.IsRunningInstalled)
            {
                _toolDirectory = Installer.InstallDirectory;
                var presentMon = Path.Combine(_toolDirectory, Installer.PresentMonName);
                if (!File.Exists(presentMon))
                    throw new FileNotFoundException($"Missing {presentMon}", presentMon);
            }
            else
            {
                _toolDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;
                EmbeddedFiles.ExtractAll(_toolDirectory);
            }
        }
        catch (Exception ex)
        {
            Fail($"{Loc.Get(StringKey.PresentMonStartFailed, Language)}\n\n{Loc.Get(StringKey.Details, Language)}: {ex.Message}");
            return;
        }

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

        SetOverlayShown(true);
        _gameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _gameTimer.Tick += (_, _) => UpdateVisibility();
        _gameTimer.Start();
    }

    // Chạy từ ngoài thư mục cài: cài mới/cập nhật qua hộp thoại, hoặc chạy luôn bản đã cài (không hỏi). Xong thì thoát.
    void RunSetup()
    {
        // Chỉ đọc (ngôn ngữ, đã hỏi PawnIO chưa), không mở SettingsService: bản này không bao giờ ghi file cài đặt.
        var saved = SettingsStore.Load(SettingsStore.DefaultPath);
        var language = saved.Language;
        // Không mang Payload.zip (bản cài nhiều file chép đi chỗ khác, bản dotnet build): báo rõ, không cài bản hỏng.
        if (!Installer.HasPayload)
        {
            MessageBox.Show(Loc.Get(StringKey.InstallPayloadMissing, language), "FPS Overlay",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }
        InstallAction action;
        try
        {
            Installer.EnsureInstallPathValid();   // không hiện hộp thoại cài với thư mục cài rỗng
            action = Installer.Decide();
            if (action == InstallAction.RunInstalled)
            {
                Installer.LaunchInstalled();
                Shutdown();
                return;
            }
        }
        catch (Exception ex)
        {
            ShowError(StringKey.InstallFailed, ex, language);
            Shutdown();
            return;
        }

        var window = new InstallWindow(action, language, saved.PawnIoOffered);
        window.Closed += (_, _) =>
        {
            if (window.Succeeded)
            {
                // Đã hỏi PawnIO: bản đã cài (chủ của file cài đặt) nhận cờ này và tự ghi PawnIoOffered = true.
                try { Installer.LaunchInstalled(window.PawnIoOffered ? [PawnIoSetup.OfferedArgument] : []); }
                catch (Exception ex) { ShowError(StringKey.InstallFailed, ex, language); }
            }
            Shutdown();
        };
        window.Show();
        window.Activate();
    }

    // --uninstall (lệnh gỡ trong Settings → Apps): hỏi xác nhận rồi gỡ. Bản cài đang chạy thì báo thoát nó trước, không tự giết.
    static void RunUninstall()
    {
        var language = SettingsStore.Load(SettingsStore.DefaultPath).Language;
        if (MessageBox.Show(Loc.Get(StringKey.UninstallConfirm, language), "FPS Overlay",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        if (Installer.IsInstalledCopyRunning())
        {
            MessageBox.Show(Loc.Get(StringKey.InstalledCopyRunning, language), "FPS Overlay",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Exception? failure = null;
        try { Installer.Uninstall(); }
        catch (Exception ex) { failure = ex; }

        // Thông báo trước: hộp thông báo giữ app sống (và khoá file trong thư mục cài).
        if (failure is null)
            MessageBox.Show(Loc.Get(StringKey.UninstallDone, language), "FPS Overlay",
                MessageBoxButton.OK, MessageBoxImage.Information);
        else
            ShowError(StringKey.UninstallFailed, failure, language);

        // Việc cuối cùng trước Shutdown (OnStartup gọi ngay sau): bản cài tự gỡ thì hẹn cmd chờ app thoát rồi xoá thư mục,
        // xoá xong mới xoá mục gỡ cài đặt. Không hẹn được thì mục gỡ còn nguyên, gỡ lại được.
        try { Installer.FinishUninstall(); }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không hẹn xoá được thư mục cài: {ex.Message}");
            if (failure is null) ShowError(StringKey.UninstallFailed, ex, language);
        }
    }

    static void ShowError(StringKey key, Exception ex, AppLanguage language) =>
        MessageBox.Show($"{Loc.Get(key, language)}\n\n{Loc.Get(StringKey.Details, language)}: {ex.Message}", "FPS Overlay",
            MessageBoxButton.OK, MessageBoxImage.Error);

    // Chạy PresentMon mới (bộ tính FPS và lịch sử frametime làm mới theo). Trả về false nếu không chạy được (đã báo lỗi và thoát).
    bool StartPresentMon()
    {
        // Mỗi runner ghi vào bộ riêng của nó (một closure mỗi lần chạy): runner cũ không thể đẩy khung vào bộ mới.
        var calculator = new FpsCalculator();
        var history = new FrameTimeHistory();
        var runner = new PresentMonRunner(_toolDirectory!);
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
            sensors = new SensorSnapshot(GpuLoad: 67, GpuTemp: 71, CpuLoad: 34, CpuTemp: 65, RamUsedGb: 9.2,
                GpuMemoryUsedGb: 3.1, GpuClockMhz: 1590, GpuPowerW: 48, GpuFanRpm: 2400);
        }
        else
        {
            onePercentLow = _history.OnePercentLow(ForegroundProcess.GetCandidates());
            sensors = _sensors.Latest;
        }
        var settings = _settings.Current;
        _overlay.SetStats(onePercentLow, sensors, settings);
        UpdateAlerts(sensors, settings);
    }

    // Xem trước không báo động; ngược lại mỗi đợt nóng đủ lâu báo một lần (tiếng) và số nhiệt nháy tới khi hạ nhiệt.
    void UpdateAlerts(SensorSnapshot sensors, OverlaySettings settings)
    {
        if (_overlay is null) return;
        bool started = false;
        if (IsPreviewing)
        {
            _gpuAlarm.Reset();
            _cpuAlarm.Reset();
        }
        else
        {
            var now = _clock.Elapsed.TotalSeconds;
            if (settings.ShowGpuTemp)
                started |= _gpuAlarm.Update(sensors.GpuTemp, settings.GpuHotThreshold, settings.HotAlertSeconds, now);
            else
                _gpuAlarm.Reset();
            if (settings.ShowCpuTemp)
                started |= _cpuAlarm.Update(sensors.CpuTemp, settings.CpuHotThreshold, settings.HotAlertSeconds, now);
            else
                _cpuAlarm.Reset();
        }
        if (started && settings.HotAlertSound)
            SystemSounds.Exclamation.Play();
        _overlay.SetAlerts(settings.HotAlertBlink && _gpuAlarm.State == HeatState.Alarming,
            settings.HotAlertBlink && _cpuAlarm.State == HeatState.Alarming);
    }

    // Nút Ẩn/Hiện FPS ở khay: chỉ đổi lựa chọn của người dùng; hiện hay ẩn thật do UpdateVisibility quyết định.
    void ToggleOverlay()
    {
        if (_settings is null) return;
        _hiddenByUser = !_hiddenByUser;
        _tray?.Apply(_settings.Current, !_hiddenByUser);
        UpdateVisibility();
    }

    // Mỗi 250 ms (và ngay khi cài đặt, cửa sổ cài đặt hay chế độ di chuyển đổi): xem cửa sổ phía trước có phải game
    // không rồi hiện/ẩn overlay. Không đụng tới đường vẽ mỗi khung hình.
    void UpdateVisibility()
    {
        if (_overlay is null || _settings is null) return;
        var s = _settings.Current;
        var hwnd = NativeMethods.GetForegroundWindow();
        var kind = ForegroundWindowInfo.Kind(hwnd);
        bool isGame = false;
        if (kind == ForegroundKind.Other)
        {
            _foregroundPaths.Clear();
            foreach (var pid in ForegroundProcess.GetCandidates())
                _foregroundPaths.Add(ForegroundWindowInfo.ExePath(pid));
            isGame = GameWindowRule.IsGame(ForegroundWindowInfo.CoversMonitor(hwnd), _foregroundPaths,
                s.AlwaysShowApps, s.NeverShowApps);
            // Tiến trình chính theo thứ tự GetCandidates; bộ nhớ đường dẫn trả về cùng chuỗi nên chỉ tách tên khi đổi app.
            var path = _foregroundPaths.Count > 0 ? _foregroundPaths[0] : null;
            if (!ReferenceEquals(path, _lastAppPath))
            {
                _lastAppPath = path;
                _lastAppName = path is null ? null : Path.GetFileName(path);
            }
        }
        bool show = GameWindowRule.ShouldShow(_hiddenByUser, _overlay.IsMoving, IsPreviewing, s.ShowOnlyInGame,
            kind, isGame, _shown);
        if (show != _shown)
            SetOverlayShown(show);
    }

    // Chỗ duy nhất hiện/ẩn overlay (trừ Fail lúc thoát). Ẩn thì ngừng vẽ mỗi khung hình, lưu nếu đang di chuyển
    // và tắt báo động; hiện thì đăng ký vẽ một lần (luôn gỡ trước để không bao giờ trùng).
    void SetOverlayShown(bool show)
    {
        if (_overlay is null) return;
        _shown = show;   // trước EndMove: MovingChanged gọi lại UpdateVisibility, khi đó thấy đã khớp nên không làm gì
        CompositionTarget.Rendering -= OnRendering;
        if (show)
        {
            _overlay.Show();
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            if (_overlay.IsMoving) _overlay.EndMove(save: true);   // ẩn giữa lúc di chuyển = lưu
            _overlay.Hide();
            _gpuAlarm.Reset();
            _cpuAlarm.Reset();
            _overlay.SetAlerts(false, false);
        }
        ApplyResourceState();
    }

    // Chỉ một cửa sổ cài đặt; mở lại thì đưa cửa sổ đang mở lên trước.
    void OpenSettings()
    {
        if (_settings is null) return;
        if (_overlay is { IsMoving: true }) _overlay.EndMove(save: true);   // mở cài đặt giữa lúc di chuyển = lưu
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        _ = _autostart.RefreshAsync();
        _settingsWindow = new SettingsWindow(_settings, _autostart, SetAutostart, () => _sensors?.CpuTemperatureAvailable ?? true,
            InstallPawnIo, _pawnIoInstall is not null, TestAlert);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;   // đóng bằng cách nào cũng về FPS thật
            UpdateVisibility();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
        UpdateVisibility();   // xem trước: hiện overlay ngay cả khi không chơi game
    }

    // Nút "Thử cảnh báo": hai số nhiệt hiện và nháy 3 giây trên overlay, kèm tiếng nếu bật.
    void TestAlert()
    {
        if (_overlay is not { IsVisible: true } || _settings is null) return;
        _overlay.RunAlertTest(TimeSpan.FromSeconds(3));
        if (_settings.Current.HotAlertSound)
            SystemSounds.Exclamation.Play();
    }

    // Nút "Cài PawnIO" trong cài đặt (bản đã cài hoặc --no-install; App luôn chạy với quyền Admin). Một lần cài một lúc:
    // đang cài thì trả về lần đang chạy. Cài xong thì mở lại thư viện cảm biến để thấy nhiệt độ CPU ngay.
    Task<PawnIoResult> InstallPawnIo() => _pawnIoInstall ??= RunPawnIoInstallAsync();

    async Task<PawnIoResult> RunPawnIoInstallAsync()
    {
        await Task.Yield();   // luôn trả về trước khi chạy: _pawnIoInstall được gán trước khi finally bên dưới xoá nó
        try
        {
            var result = await PawnIoSetup.InstallAsync(CancellationToken.None);
            if (PawnIoResults.IsSuccess(result))
            {
                _sensors?.Reinitialize();
                if (_settings is { Current.PawnIoOffered: false } settings)
                    settings.Update(s => s with { PawnIoOffered = true });
            }
            return result;
        }
        finally
        {
            _pawnIoInstall = null;
        }
    }

    void SetAutostart(bool enable)
    {
        if (_settingAutostart || !_autostart.CanManage) return;   // bản không cài: công tắc đã mờ, phòng hờ
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
        _gameTimer?.Stop();
        _watchdog?.Stop();
        _statsTimer?.Stop();
        _logTimer?.Stop();
        CompositionTarget.Rendering -= OnRendering;
        if (_overlay is { IsMoving: true }) _overlay.EndMove(save: true);
        _overlay?.Hide();   // không để con số cũ treo trên game
        // DefaultDesktopOnly: hộp thoại nổi lên trên cả game borderless
        MessageBox.Show(message, "FPS Overlay", MessageBoxButton.OK, MessageBoxImage.Error,
            MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_overlay is { IsMoving: true }) _overlay.EndMove(save: true);   // không mất vị trí vừa kéo
        _gameTimer?.Stop();
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
