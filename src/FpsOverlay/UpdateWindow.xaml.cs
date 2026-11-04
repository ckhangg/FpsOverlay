using System.Windows;
using System.Windows.Input;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;
using FpsOverlay.Core.Update;

namespace FpsOverlay;

/// <summary>Lựa chọn trong hộp thoại cập nhật.</summary>
enum UpdateChoice
{
    /// <summary>Tải và cài bản mới (chỉ bản đã cài).</summary>
    Update,
    /// <summary>Không báo bản này nữa ở lần kiểm tự động (App lưu <c>SkippedVersion</c>).</summary>
    Skip,
    /// <summary>Đóng, không lưu gì (kể cả đóng bằng [ X ] hay Esc).</summary>
    Later,
    /// <summary>Mở trang phát hành trên GitHub (bản không cài, không tự cập nhật được).</summary>
    OpenPage,
}

/// <summary>
/// Hộp thoại "Có bản cập nhật" theo chủ đề (xem <see cref="ThemeManager"/>): hai số phiên bản, ghi chú phát hành và ba nút.
/// Chỉ trả về lựa chọn qua <see cref="Choice"/> khi đóng; App lo phần còn lại (lưu bỏ qua, mở trang, tải).
/// </summary>
public partial class UpdateWindow : Window
{
    readonly bool _canInstall;

    /// <summary>Lựa chọn của người dùng; mặc định <see cref="UpdateChoice.Later"/> (đóng bằng [ X ], Esc).</summary>
    internal UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

    internal ReleaseInfo Release { get; }

    /// <param name="canInstall">Bản đã cài (tự cập nhật được); không thì nút chính là "Mở trang tải".</param>
    internal UpdateWindow(ReleaseInfo release, Version current, bool canInstall, AppLanguage language)
    {
        Release = release;
        _canInstall = canInstall;
        InitializeComponent();
        Icon = SettingsWindow.CreateWindowIcon();
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));

        string T(StringKey key) => Loc.Get(key, language);
        Title = T(StringKey.UpdateTitle);
        TitleSection.Content = "// v" + release.Version.ToString(3);   // tiêu đề đã ở thanh trên cửa sổ
        VersionsText.Text = string.Format(T(StringKey.UpdateVersions), "v" + current.ToString(3), "v" + release.Version.ToString(3));
        var notes = release.Notes.Replace("\r\n", "\n").Trim();
        NotesText.Text = notes;
        NotesBox.Visibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        PrimaryButton.Content = T(canInstall ? StringKey.UpdateNow : StringKey.UpdateOpenPage);
        SkipButton.Content = T(StringKey.UpdateSkip);
        LaterButton.Content = T(StringKey.UpdateLater);
    }

    void Finish(UpdateChoice choice)
    {
        Choice = choice;
        Close();
    }

    void OnPrimaryClick(object sender, RoutedEventArgs e) => Finish(_canInstall ? UpdateChoice.Update : UpdateChoice.OpenPage);

    void OnSkipClick(object sender, RoutedEventArgs e) => Finish(UpdateChoice.Skip);

    void OnLaterClick(object sender, RoutedEventArgs e) => Finish(UpdateChoice.Later);
}
