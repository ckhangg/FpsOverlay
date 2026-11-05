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
using FpsOverlay.Core.Update;

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
    readonly UpdateService _updates = new();
    // Huỷ lần kiểm cập nhật đang chạy (và lần chờ trước khi kiểm) khi app thoát.
    readonly CancellationTokenSource _updateCts = new();
    // Lần gọi GitHub gần nhất (đang chạy thì kiểm tự động và bấm tay dùng chung, không gọi hai lần).
    Task<ReleaseInfo?>? _updateCheck;
    // Đã có lần kiểm nào bắt đầu: lần kiểm tự động (sau 5 s) bỏ qua nếu người dùng đã bấm kiểm tay trước đó.
    bool _updateChecked;
    // Lần bấm tay đang chờ kết quả / đang báo: lần kiểm tự động dùng chung kết quả thì không báo thêm.
    bool _manualCheckActive;
    // Bản mới đang báo ở khay (dòng đầu menu / balloon); bấm vào thì mở lại hộp thoại với bản này.
    ReleaseInfo? _pendingRelease;
    UpdateWindow? _updateWindow;

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
        var args = FpsOverlay.Core.CommandLine.Parse(e.Args);
        if (args.Uninstall)
        {
            RunUninstall();
            Shutdown();
            return;
        }
        var noInstall = args.NoInstall;
        if (!noInstall && !Installer.IsRunningInstalled)
        {
            RunSetup(args.UpdateAfterPid);   // tự Shutdown khi xong hoặc huỷ
            return;
        }

        // Hai bản chạy cùng lúc sẽ tranh nhau một phiên PresentMon và làm bản kia dừng.
        _singleInstance = new Mutex(true, Installer.SingleInstanceMutexName, out var isFirst);
        if (!isFirst)
        {
            // Chỉ đọc ngôn ngữ, không mở SettingsService (bản này không được ghi file cài đặt).
            var language = SettingsStore.Load(SettingsStore.DefaultPath).Language;
            MessageBox.Show(Loc.Get(StringKey.AlreadyRunning, language), "EzFPS",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var settings = _settings = new SettingsService(SettingsStore.DefaultPath);
        // Bản setup vừa hỏi người dùng về PawnIO (tick hay không): nhớ lại để lần cập nhật sau không hỏi nữa.
        // Cờ này chỉ đến từ bản setup; Task Scheduler chạy bản cài không kèm tham số nào.
        if (args.PawnIoOffered && !settings.Current.PawnIoOffered)
            settings.Update(s => s with { PawnIoOffered = true });
        // Chủ đề cửa sổ theo phong cách overlay, chữ công tắc theo ngôn ngữ. Đăng ký trước mọi cửa sổ: cửa sổ cài đặt
        // dựng lại chữ (vd. ký hiệu mục thu gọn) trong Changed của nó, lúc đó chủ đề mới phải được nạp rồi.
        ApplyTheme(settings.Current);
        settings.Changed += ApplyTheme;
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
        tray.CheckUpdatesRequested += () => _ = CheckForUpdatesManuallyAsync();
        tray.UpdateItemClicked += () =>
        {
            if (_pendingRelease is { } release) ShowUpdateWindow(release);
        };
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
        // Exe bản cập nhật đã chạy xong (không tự xoá được mình), bộ cài PawnIO sót lại: xoá cái cũ hơn một giờ.
        SecureTemp.DeleteStaleExecutables();

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
        UpdateVisibility();   // quyết định ngay, không chờ nhịp 250 ms đầu (tránh chớp overlay lúc khởi động)
        _gameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _gameTimer.Tick += (_, _) => UpdateVisibility();
        _gameTimer.Start();

        _ = CheckForUpdatesOnStartupAsync(args.Autostart);
    }

    // Kiểm cập nhật một lần sau khi mở app, đợi vài giây để không làm chậm lúc khởi động. Không bao giờ ném ra ngoài:
    // không có mạng, quá giờ, GitHub lỗi hay giới hạn lượt gọi thì chỉ ghi Trace.
    async Task CheckForUpdatesOnStartupAsync(bool autostart)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), _updateCts.Token);
            // Người dùng đã bấm kiểm tay trong lúc chờ: lần đó đã (hoặc đang) báo kết quả, không kiểm thêm.
            if (_updateChecked || _settings is null) return;
            var release = await CheckSharedAsync();
            // Người dùng bấm kiểm tay trong lúc lần này đang chạy: lần bấm tay dùng chung kết quả và tự báo, ở đây
            // không mở thêm hộp thoại nào (lỗi cũng để lần bấm tay báo).
            if (_manualCheckActive || _settings is null || release is null ||
                !UpdatePolicy.ShouldOffer(UpdateService.CurrentVersion, release, _settings.Current.SkippedVersion, manual: false))
                return;
            // Tự chạy lúc đăng nhập Windows, hoặc đang có game ở phía trước (không cướp focus của game): chỉ báo ở khay
            // (balloon + dòng đầu menu), không bật hộp thoại. Bấm dòng đó / balloon thì mở hộp thoại.
            if (autostart || IsGameInFront())
            {
                _pendingRelease = release;
                _tray?.ShowUpdateAvailable(release.Version.ToString(3), balloon: true);
            }
            else
                ShowUpdateWindow(release);
        }
        catch (OperationCanceledException) when (_updateCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Kiểm cập nhật lúc khởi động lỗi: {ex.Message}");
        }
    }

    // Game đang ở phía trước theo trạng thái hiện/ẩn sẵn có: chế độ chỉ-hiện-khi-chơi đang bật và overlay đang hiện mà không
    // phải vì xem trước hay đang di chuyển (khi đó chỉ game, hoặc app trong "Luôn hiện", mới làm overlay hiện).
    bool IsGameInFront() =>
        _settings is { Current.ShowOnlyInGame: true } && _shown && !_hiddenByUser && !IsPreviewing && _overlay is { IsMoving: false };

    // Lần gọi GitHub đang chạy thì dùng chung (kiểm tự động và bấm tay không gọi hai lần); không thì gọi mới.
    Task<ReleaseInfo?> CheckSharedAsync()
    {
        if (_updateCheck is { IsCompleted: false } running) return running;
        _updateChecked = true;
        return _updateCheck = _updates.CheckAsync(_updateCts.Token);
    }

    // Nút "Kiểm tra cập nhật" (cài đặt, khay): luôn báo kết quả, kể cả khi đang chờ lần kiểm tự động (dùng chung lần đó).
    // Có bản mới (kể cả bản đã bỏ qua) thì mở hộp thoại; đã mới nhất hay lỗi thì báo bằng hộp thông báo.
    async Task CheckForUpdatesManuallyAsync()
    {
        if (_settings is null) return;
        if (_updateWindow is not null)
        {
            _updateWindow.Activate();
            return;
        }
        if (_manualCheckActive) return;   // lần bấm trước đang chờ kết quả, nó sẽ báo
        // Đặt cờ trước khi chờ, gỡ sau khi đã báo xong: lần kiểm tự động (nếu đang chạy) thấy cờ thì không mở thêm gì.
        _manualCheckActive = true;
        try
        {
            ReleaseInfo? release;
            try
            {
                release = await CheckSharedAsync();
            }
            catch (Exception ex)
            {
                if (_updateCts.IsCancellationRequested) return;   // app đang thoát
                Trace.TraceWarning($"Kiểm cập nhật lỗi: {ex.Message}");
                MessageBox.Show($"{Loc.Get(StringKey.UpdateCheckFailed, Language)}\n\n{Loc.Get(StringKey.Details, Language)}: {ex.Message}",
                    "EzFPS", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_settings is null || _updateCts.IsCancellationRequested) return;
            if (release is not null && UpdatePolicy.ShouldOffer(UpdateService.CurrentVersion, release, _settings.Current.SkippedVersion, manual: true))
                ShowUpdateWindow(release);
            else
                MessageBox.Show(string.Format(Loc.Get(StringKey.UpToDate, Language), "v" + UpdateService.CurrentVersion.ToString(3)),
                    "EzFPS", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            _manualCheckActive = false;
        }
    }

    // Một hộp thoại cập nhật một lúc; đang mở thì đưa lên trước. Lựa chọn xử lý khi đóng.
    void ShowUpdateWindow(ReleaseInfo release)
    {
        if (_settings is null) return;
        if (_updateWindow is not null)
        {
            _updateWindow.Activate();
            return;
        }
        // Chỉ bản đã cài tự cập nhật được (thư mục Temp trong Program Files, cài lại vào đúng chỗ); bản khác mở trang tải.
        Func<IProgress<double>, CancellationToken, Task<string>>? download = Installer.IsRunningInstalled
            ? (progress, token) => _updates.DownloadAsync(release, progress, token)
            : null;
        var window = _updateWindow = new UpdateWindow(release, UpdateService.CurrentVersion, download, Language, _updateCts.Token);
        window.Closed += (_, _) =>
        {
            _updateWindow = null;
            OnUpdateChoice(window.Release, window.Choice, window.DownloadedPath);
        };
        window.Show();
        window.Activate();
    }

    void OnUpdateChoice(ReleaseInfo release, UpdateChoice choice, string? downloadedPath)
    {
        if (_updateCts.IsCancellationRequested) return;   // hộp thoại bị đóng vì app thoát
        switch (choice)
        {
            case UpdateChoice.Update when downloadedPath is not null:
                StartUpdate(release, downloadedPath);
                break;
            case UpdateChoice.Skip:
                // Lần kiểm tự động sau không báo bản này nữa; bản lớn hơn vẫn báo. Khôi phục mặc định không xoá.
                var version = release.Version.ToString(3);
                _settings?.Update(s => s with { SkippedVersion = version });
                _pendingRelease = null;
                _tray?.HideUpdateAvailable();
                break;
            case UpdateChoice.OpenPage:
                OpenReleasePage(release);
                RemindInTray(release);
                break;
            default:   // Để sau: không lưu gì; dòng báo ở khay ở lại tới khi tắt app
                RemindInTray(release);
                break;
        }
    }

    void RemindInTray(ReleaseInfo release)
    {
        _pendingRelease = release;
        _tray?.ShowUpdateAvailable(release.Version.ToString(3), balloon: false);
    }

    // Đã tải và kiểm băm bản mới (exe trong Temp của thư mục cài, chỉ Admin ghi được): chạy nó ở chế độ setup với
    // --update-after <PID này> rồi thoát. Bản mới chờ tiến trình này thoát hẳn (OnExit nhả mutex một bản chạy, dừng
    // PresentMon, ghi nốt log và cài đặt) mới cài đè, rồi chạy bản đã cài. Không chạy được thì báo, app chạy tiếp.
    void StartUpdate(ReleaseInfo release, string exePath)
    {
        try
        {
            var start = new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,   // app đã là Admin: bản mới thừa hưởng quyền, không hỏi UAC
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
            };
            start.ArgumentList.Add(FpsOverlay.Core.CommandLine.UpdateAfterArgument);
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Bản mới là exe single-file: mặc định bung thư viện native vào %TEMP% (tiến trình không Admin ghi được, cài sẵn
            // dll giả vào đó được) rồi nạp với quyền Admin. Cho bung vào Temp của thư mục cài (chỉ Admin ghi được).
            var extract = Path.Combine(Path.GetDirectoryName(exePath)!, UpdateHandover.ExtractDirectoryName);
            Directory.CreateDirectory(extract);
            start.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = extract;
            // Chạy từ tác vụ khởi động (Task Scheduler): tách bản mới khỏi job của tác vụ để nó không bị tắt cùng app này.
            // Không trong job, hay job không cho tách: chạy như cũ.
            if (!JobBreakaway.TryStart(start, useEnvironment: true))
            {
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Process.Start returned null");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không chạy được bản cập nhật {release.Tag}: {ex.Message}");
            try { File.Delete(exePath); }
            catch (Exception deleteError) when (deleteError is IOException or UnauthorizedAccessException) { }
            ShowError(StringKey.UpdateFailed, ex, Language);
            RemindInTray(release);
            return;
        }
        Trace.TraceInformation($"Chuyển sang bản cập nhật {release.Tag}.");
        Shutdown();
    }

    // Trang phát hành mở bằng trình duyệt mặc định qua explorer.exe theo đường dẫn đầy đủ (như nút mở thư mục log;
    // tên trần thì CreateProcess tìm trong thư mục exe trước). Chỉ mở URL https (ReleaseInfo.Parse cũng đã lọc).
    static void OpenReleasePage(ReleaseInfo release)
    {
        try
        {
            if (!Uri.TryCreate(release.PageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            using var _ = Process.Start(explorer, $"\"{uri.AbsoluteUri}\"");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không mở được trang phát hành: {ex.Message}");
        }
    }

    // Chạy từ ngoài thư mục cài: cài mới/cập nhật qua hộp thoại, hoặc chạy luôn bản đã cài (không hỏi). Xong thì thoát.
    // updateAfterPid (--update-after): bản cài cũ vừa tải bản này về rồi thoát; chờ nó thoát hẳn rồi tự cập nhật (không hỏi).
    async void RunSetup(int? updateAfterPid)
    {
        // Chỉ đọc (ngôn ngữ, đã hỏi PawnIO chưa), không mở SettingsService: bản này không bao giờ ghi file cài đặt.
        var saved = SettingsStore.Load(SettingsStore.DefaultPath);
        var language = saved.Language;
        ApplyTheme(saved);   // hộp thoại cài mang chủ đề của phong cách đã lưu
        // Không mang Payload.zip (bản cài nhiều file chép đi chỗ khác, bản dotnet build): báo rõ, không cài bản hỏng.
        if (!Installer.HasPayload)
        {
            MessageBox.Show(Loc.Get(StringKey.InstallPayloadMissing, language), "EzFPS",
                MessageBoxButton.OK, MessageBoxImage.Error);
            // Tự cập nhật: bản cũ đã (đang) thoát; chờ nó thoát hẳn rồi mở lại bản đang cài.
            if (updateAfterPid is { } failedPid && await WaitForOldInstanceAsync(failedPid))
                RelaunchInstalledAfterFailedUpdate(language);
            Shutdown();
            return;
        }

        var auto = updateAfterPid is not null;
        if (updateAfterPid is { } pid)
        {
            // Bản cũ còn chạy (quá 30 s) hay có bản cài khác đang chạy: exe trong thư mục cài đang bị khoá, không cài đè.
            if (!await WaitForOldInstanceAsync(pid) || Installer.IsInstalledCopyRunning())
            {
                MessageBox.Show(Loc.Get(StringKey.InstalledCopyRunning, language), "EzFPS",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
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
            if (auto) RelaunchInstalledAfterFailedUpdate(language);
            Shutdown();
            return;
        }

        var window = new InstallWindow(action, language, saved.PawnIoOffered, auto);
        window.Closed += (_, _) =>
        {
            // Tự cập nhật lỗi (hộp thoại đã báo): vẫn mở lại bản đang có trong thư mục cài để người dùng không mất overlay.
            // PayloadFiles.Apply chuyển từng file vào chỗ, gọi lại an toàn; exe cài còn đó thì chạy được.
            if (window.Succeeded || (auto && File.Exists(Installer.InstalledExePath)))
            {
                // Đã hỏi PawnIO: bản đã cài (chủ của file cài đặt) nhận cờ này và tự ghi PawnIoOffered = true.
                // Tự cập nhật không hỏi PawnIO nên không kèm cờ.
                try { Installer.LaunchInstalled(window.PawnIoOffered ? [PawnIoSetup.OfferedArgument] : []); }
                catch (Exception ex) { ShowError(StringKey.InstallFailed, ex, language); }
            }
            Shutdown();
        };
        window.Show();
        window.Activate();
    }

    // Chờ bản cài cũ (PID truyền qua --update-after) thoát, tối đa 30 s. Chỉ chờ khi PID đó đúng là FpsOverlay chạy từ exe
    // đã cài (PID có thể đã bị tiến trình khác dùng lại); không phải hay đã thoát thì không chờ (true). Không đọc được
    // tên/đường dẫn thì chờ mutex một bản chạy được nhả, tối đa 30 s. false = bản cũ (bản cài) vẫn chạy sau 30 s.
    static async Task<bool> WaitForOldInstanceAsync(int pid)
    {
        Process process;
        try { process = Process.GetProcessById(pid); }
        catch (ArgumentException) { return true; }   // đã thoát
        using (process)
        {
            try
            {
                if (!UpdateHandover.IsOldInstance(process.ProcessName, process.MainModule?.FileName, Installer.InstalledExePath))
                    return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Vừa thoát, hoặc không đọc được đường dẫn (bị từ chối quyền): không biết có phải bản cũ không, nên chờ
                // mutex một bản chạy được nhả (bản cài nào đó còn chạy thì exe cài đang bị khoá).
                return await WaitForInstalledCopyExitAsync(pid);
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                Trace.TraceWarning($"Bản cũ (PID {pid}) chưa thoát sau 30 giây.");
                return false;
            }
        }
    }

    // Hỏi mutex một bản chạy mỗi 250 ms, tối đa 30 s. true = không còn bản cài nào chạy.
    static async Task<bool> WaitForInstalledCopyExitAsync(int pid)
    {
        var waited = Stopwatch.StartNew();
        while (Installer.IsInstalledCopyRunning())
        {
            if (waited.Elapsed >= TimeSpan.FromSeconds(30))
            {
                Trace.TraceWarning($"Không xác định được PID {pid}; bản cài vẫn chạy sau 30 giây.");
                return false;
            }
            await Task.Delay(250);
        }
        return true;
    }

    // Tự cập nhật (--update-after) dừng vì lỗi (đã báo): mở lại bản đang có trong thư mục cài, nếu còn và chưa có bản nào
    // chạy, để người dùng không mất overlay (như nhánh hộp thoại cài lỗi trong RunSetup).
    static void RelaunchInstalledAfterFailedUpdate(AppLanguage language)
    {
        if (!File.Exists(Installer.InstalledExePath) || Installer.IsInstalledCopyRunning()) return;
        try { Installer.LaunchInstalled(); }
        catch (Exception ex) { ShowError(StringKey.InstallFailed, ex, language); }
    }

    static void ApplyTheme(OverlaySettings settings)
    {
        ThemeManager.Apply(settings.Style);
        ThemeManager.ApplyLanguage(settings.Language);
    }

    // --uninstall (lệnh gỡ trong Settings → Apps): hỏi xác nhận rồi gỡ. Bản cài đang chạy thì báo thoát nó trước, không tự giết.
    static void RunUninstall()
    {
        var language = SettingsStore.Load(SettingsStore.DefaultPath).Language;
        if (MessageBox.Show(Loc.Get(StringKey.UninstallConfirm, language), "EzFPS",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        if (Installer.IsInstalledCopyRunning())
        {
            MessageBox.Show(Loc.Get(StringKey.InstalledCopyRunning, language), "EzFPS",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Exception? failure = null;
        try { Installer.Uninstall(); }
        catch (Exception ex) { failure = ex; }

        // Thông báo trước: hộp thông báo giữ app sống (và khoá file trong thư mục cài).
        if (failure is null)
            MessageBox.Show(Loc.Get(StringKey.UninstallDone, language), "EzFPS",
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
        MessageBox.Show($"{Loc.Get(key, language)}\n\n{Loc.Get(StringKey.Details, language)}: {ex.Message}", "EzFPS",
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
            _overlay.RefreshPosition();   // theo màn hình của game hiện tại, không giữ vị trí cũ tới nhịp 1 s
            UpdateStats();                // số liệu mới ngay, không hiện số cũ tới nhịp 1 s
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
            InstallPawnIo, _pawnIoInstall is not null, TestAlert, () => _ = CheckForUpdatesManuallyAsync());
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
            MessageBox.Show(message, "EzFPS", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _settingAutostart = false;
        }
    }

    void Fail(string message)
    {
        _updateCts.Cancel();
        _gameTimer?.Stop();
        _watchdog?.Stop();
        _statsTimer?.Stop();
        _logTimer?.Stop();
        CompositionTarget.Rendering -= OnRendering;
        if (_overlay is { IsMoving: true }) _overlay.EndMove(save: true);
        _overlay?.Hide();   // không để con số cũ treo trên game
        // DefaultDesktopOnly: hộp thoại nổi lên trên cả game borderless
        MessageBox.Show(message, "EzFPS", MessageBoxButton.OK, MessageBoxImage.Error,
            MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_overlay is { IsMoving: true }) _overlay.EndMove(save: true);   // không mất vị trí vừa kéo
        _updateCts.Cancel();
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
