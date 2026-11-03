using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FpsOverlay.Core.Install;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.Settings;

namespace FpsOverlay;

/// <summary>
/// Hộp thoại cài/cập nhật kiểu Cyberpunk, hiện khi chạy exe từ ngoài thư mục cài. Bấm cài: kiểm tra bản cài không
/// chạy, chép (ngoài luồng UI), trỏ lại tác vụ khởi động cùng Windows, rồi đóng với <see cref="Succeeded"/> = true.
/// Tuỳ chọn thêm (vd. PawnIO) đặt vào <c>OptionsPanel</c> và chạy trong <see cref="AfterInstallAsync"/>.
/// </summary>
public partial class InstallWindow : Window
{
    readonly InstallAction _action;
    // Chỉ đổi chữ của hộp thoại; không ghi file cài đặt (bước cài chỉ đọc cài đặt, ngôn ngữ app đổi trong app).
    AppLanguage _language;
    bool _busy;
    bool _refreshing = true;

    /// <summary>Cài/cập nhật xong (App chạy bản đã cài rồi thoát); false = huỷ hoặc đóng.</summary>
    public bool Succeeded { get; private set; }

    /// <param name="action"><see cref="InstallAction.Install"/> hoặc <see cref="InstallAction.Update"/>.</param>
    /// <param name="language">Ngôn ngữ trong cài đặt đã lưu; người dùng đổi được ngay trong hộp thoại (chỉ cho hộp thoại).</param>
    internal InstallWindow(InstallAction action, AppLanguage language)
    {
        _action = action;
        _language = language;
        InitializeComponent();
        Icon = SettingsWindow.CreateWindowIcon();
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));

        LanguageVi.IsChecked = language == AppLanguage.Vietnamese;
        LanguageEn.IsChecked = language == AppLanguage.English;
        VersionValue.Text = Installer.RunningVersion.ToString(3);
        ApplyLanguage();
        _refreshing = false;
    }

    void ApplyLanguage()
    {
        string T(StringKey key) => Loc.Get(key, _language);
        var update = _action == InstallAction.Update;

        Title = T(StringKey.InstallTitle);
        PromptSection.Content = "// " + T(update ? StringKey.UpdateButton : StringKey.InstallButton);
        PromptText.Text = string.Format(T(update ? StringKey.UpdatePrompt : StringKey.InstallPrompt), Installer.InstallDirectory);
        LanguageLabel.Text = T(StringKey.Language);
        VersionLabel.Text = T(StringKey.InstallVersion);
        StatusText.Text = T(StringKey.InstallWorking);
        InstallButton.Content = T(update ? StringKey.UpdateButton : StringKey.InstallButton);
        CancelButton.Content = T(StringKey.Cancel);
    }

    void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        _language = (AppLanguage)((FrameworkElement)sender).Tag;
        ApplyLanguage();
    }

    async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        // Bản cài đang chạy thì exe/PresentMon trong thư mục cài đang bị khoá: báo thoát nó trước, không tự giết.
        // Hộp thoại vẫn mở để người dùng thoát bản kia rồi bấm lại.
        if (Installer.IsInstalledCopyRunning())
        {
            MessageBox.Show(this, Loc.Get(StringKey.InstalledCopyRunning, _language), "FPS Overlay",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetBusy(true);
        try
        {
            await Task.Run(() =>
            {
                Installer.Install();
                // Tác vụ khởi động cùng Windows đang có (có thể trỏ vào thư mục build cũ): trỏ lại vào bản đã cài.
                new AutostartService().Retarget(Installer.InstalledExePath);
            });
            await AfterInstallAsync();
        }
        catch (Exception ex)
        {
            SetBusy(false);
            MessageBox.Show(this, $"{Loc.Get(StringKey.InstallFailed, _language)}\n\n{Loc.Get(StringKey.Details, _language)}: {ex.Message}",
                "FPS Overlay", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Succeeded = true;
        _busy = false;
        Close();
    }

    /// <summary>Các bước chạy sau khi đã chép xong (exe và PresentMon đã nằm trong thư mục cài). Hiện chưa có gì.</summary>
    Task AfterInstallAsync() => Task.CompletedTask;

    void SetBusy(bool busy)
    {
        _busy = busy;
        InstallButton.IsEnabled = CancelButton.IsEnabled = LanguageVi.IsEnabled = LanguageEn.IsEnabled = OptionsPanel.IsEnabled = !busy;
        StatusText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    // Đang chép thì không cho đóng (nút [ X ], Alt+F4): đóng giữa chừng sẽ thoát app khi chưa cài xong.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_busy) e.Cancel = true;
        base.OnClosing(e);
    }
}
