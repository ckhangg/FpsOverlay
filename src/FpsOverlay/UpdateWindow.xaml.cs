using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;
using FpsOverlay.Core.Update;

namespace FpsOverlay;

/// <summary>Lựa chọn trong hộp thoại cập nhật.</summary>
enum UpdateChoice
{
    /// <summary>Đã tải và kiểm xong bản mới (chỉ bản đã cài): App chạy <see cref="UpdateWindow.DownloadedPath"/> rồi thoát.</summary>
    Update,
    /// <summary>Không báo bản này nữa ở lần kiểm tự động (App lưu <c>SkippedVersion</c>).</summary>
    Skip,
    /// <summary>Đóng, không lưu gì (kể cả đóng bằng [ X ], Esc, hay huỷ lúc đang tải).</summary>
    Later,
    /// <summary>Mở trang phát hành trên GitHub (bản không cài, không tự cập nhật được).</summary>
    OpenPage,
}

/// <summary>
/// Hộp thoại "Có bản cập nhật" theo chủ đề (xem <see cref="ThemeManager"/>): hai số phiên bản, ghi chú phát hành và ba nút.
/// Bấm CẬP NHẬT (bản đã cài) thì chuyển sang chế độ tải: thanh tiến trình và nút HUỶ; tải và kiểm băm xong thì đóng với
/// <see cref="UpdateChoice.Update"/> và <see cref="DownloadedPath"/>. Chỉ trả về lựa chọn qua <see cref="Choice"/> khi
/// đóng; App lo phần còn lại (lưu bỏ qua, mở trang, chạy bản mới).
/// </summary>
public partial class UpdateWindow : Window
{
    readonly bool _canInstall;
    readonly AppLanguage _language;
    // Tải bản mới (UpdateService.DownloadAsync); null khi không cài được.
    readonly Func<IProgress<double>, CancellationToken, Task<string>>? _download;
    // App thoát thì huỷ lần tải đang chạy.
    readonly CancellationToken _appToken;
    CancellationTokenSource? _downloadCts;
    bool _closed;

    /// <summary>Lựa chọn của người dùng; mặc định <see cref="UpdateChoice.Later"/> (đóng bằng [ X ], Esc).</summary>
    internal UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

    /// <summary>Exe bản mới đã tải và kiểm băm (trong thư mục tạm chỉ Admin ghi được); chỉ có khi <see cref="Choice"/> là Update.</summary>
    internal string? DownloadedPath { get; private set; }

    internal ReleaseInfo Release { get; }

    /// <param name="download">Tải bản mới, trả về đường dẫn exe; null = không tự cập nhật được (nút chính là "Mở trang tải").</param>
    internal UpdateWindow(ReleaseInfo release, Version current, Func<IProgress<double>, CancellationToken, Task<string>>? download,
        AppLanguage language, CancellationToken appToken)
    {
        Release = release;
        _download = download;
        _canInstall = download is not null;
        _language = language;
        _appToken = appToken;
        InitializeComponent();
        Icon = SettingsWindow.CreateWindowIcon();
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));

        Title = T(StringKey.UpdateTitle);
        TitleSection.Content = "// v" + release.Version.ToString(3);   // tiêu đề đã ở thanh trên cửa sổ
        VersionsText.Text = string.Format(T(StringKey.UpdateVersions), "v" + current.ToString(3), "v" + release.Version.ToString(3));
        var notes = release.Notes.Replace("\r\n", "\n").Trim();
        NotesText.Text = notes;
        NotesBox.Visibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        PrimaryButton.Content = T(_canInstall ? StringKey.UpdateNow : StringKey.UpdateOpenPage);
        SkipButton.Content = T(StringKey.UpdateSkip);
        LaterButton.Content = T(StringKey.UpdateLater);
        CancelDownloadButton.Content = T(StringKey.UpdateCancel);
    }

    string T(StringKey key) => Loc.Get(key, _language);

    void Finish(UpdateChoice choice)
    {
        Choice = choice;
        Close();
    }

    void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        if (_download is null) Finish(UpdateChoice.OpenPage);
        else _ = DownloadAsync(_download);
    }

    void OnSkipClick(object sender, RoutedEventArgs e) => Finish(UpdateChoice.Skip);

    void OnLaterClick(object sender, RoutedEventArgs e) => Finish(UpdateChoice.Later);

    // Huỷ lần tải: DownloadAsync xoá file dở; hộp thoại đóng như "Để sau" (dòng báo ở khay ở lại).
    void OnCancelDownloadClick(object sender, RoutedEventArgs e) => _downloadCts?.Cancel();

    async Task DownloadAsync(Func<IProgress<double>, CancellationToken, Task<string>> download)
    {
        if (_downloadCts is not null) return;
        using var cts = _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(_appToken);
        SetDownloading(true);
        var progress = new Progress<double>(ShowProgress);
        try
        {
            var path = await download(progress, cts.Token);
            if (_closed)
            {
                TryDelete(path);   // hộp thoại đã đóng đúng lúc tải xong: không để lại exe không ai chạy
                return;
            }
            DownloadedPath = path;
            Finish(UpdateChoice.Update);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if (!_closed) Finish(UpdateChoice.Later);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Tải bản cập nhật lỗi: {ex.Message}");
            if (_closed) return;
            SetDownloading(false);   // về ba nút lựa chọn: thử lại, để sau hoặc bỏ qua
            MessageBox.Show(this, $"{T(StringKey.UpdateFailed)}\n\n{T(StringKey.Details)}: {ex.Message}", "FPS Overlay",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _downloadCts = null;
        }
    }

    void SetDownloading(bool downloading)
    {
        DownloadPanel.Visibility = DownloadButtons.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        ChoiceButtons.Visibility = downloading ? Visibility.Collapsed : Visibility.Visible;
        // Nút ẩn vẫn nhận Enter/Esc (IsDefault/IsCancel) nếu còn bật: tắt hẳn khi đang tải.
        PrimaryButton.IsEnabled = SkipButton.IsEnabled = LaterButton.IsEnabled = !downloading;
        DownloadProgress.IsIndeterminate = false;
        DownloadProgress.Value = 0;
        DownloadText.Text = T(StringKey.UpdateDownloading);
        if (downloading) CancelDownloadButton.Focus();
    }

    void ShowProgress(double fraction)
    {
        if (_closed || _downloadCts is null) return;
        if (double.IsNaN(fraction))
        {
            DownloadProgress.IsIndeterminate = true;
            return;
        }
        DownloadProgress.IsIndeterminate = false;
        DownloadProgress.Value = fraction;
        DownloadText.Text = $"{T(StringKey.UpdateDownloading)} {fraction:P0}";
    }

    // Đóng bằng [ X ] / Alt+F4 lúc đang tải: huỷ lần tải (file dở bị xoá) rồi đóng như "Để sau".
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        _closed = true;
        _downloadCts?.Cancel();
    }

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Không xoá được bản cập nhật đã tải: {ex.Message}");
        }
    }
}
