# Đợt 8: giao diện theo chủ đề và cập nhật qua GitHub — Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cửa sổ cài đặt, hộp thoại cài và hộp thoại cập nhật đổi giao diện theo 4 chủ đề overlay. App tự kiểm và cài bản mới từ GitHub Releases.

**Architecture:**
- Phần thuần trong Core, có test: đọc JSON release, so phiên bản, quyết định báo, đọc `.sha256`, đọc tham số dòng lệnh, XML tác vụ có tham số, `SkippedVersion`, test cùng tập khoá giữa các file chủ đề.
- WPF:
  - mỗi chủ đề là một `ResourceDictionary` (`Themes/<Style>.xaml`) cùng bộ khoá `Ui.*`, được `ThemeManager` hoán đổi trong `Application.Resources`;
  - mọi chỗ dùng chuyển sang `DynamicResource` / `SetResourceReference`;
  - `UpdateService` dùng `HttpClient`.

**Tech Stack:** C# / .NET 10, WPF, WinForms NotifyIcon, xUnit, System.Text.Json, HttpClient.

**Spec:** `docs/superpowers/specs/2026-11-04-themed-ui-updater-design.md`

## Global Constraints
- **Bốn chủ đề** theo bảng trong spec phần A (màu, font, hình dáng điều khiển).
  - Neon: tiêu đề dùng Chakra Petch phát sáng hồng, không dùng Orbitron (Orbitron không có tiếng Việt).
  - Terminal: VT323; công tắc `[BẬT]`/`[TẮT]` (en `[ON]`/`[OFF]`), thanh trượt `[######----]`, nút `[ … ]`, ký hiệu mở/thu `[+]`/`[-]`. Điều khiển vẫn là điều khiển WPF thật (bàn phím, chuột).
- **Đổi chủ đề tức thì**, không mở lại cửa sổ. Cài lần đầu dùng Cyberpunk.
- **Kiểm cập nhật:** `GET https://api.github.com/repos/ckhangg/FpsOverlay/releases/latest`, header `User-Agent: FpsOverlay/<version>`, `Accept: application/vnd.github+json`, timeout 10 s. Cần cả hai asset `FpsOverlay.exe` và `FpsOverlay.exe.sha256`.
- **So phiên bản** `major.minor.patch`, bỏ `v` và đuôi `-…`/`+…`; chỉ báo khi lớn hơn.
- **Tự động:** im lặng khi lỗi, bỏ qua `SkippedVersion`. **Bấm tay:** luôn báo kết quả, hiện cả bản đã bỏ qua.
- **Khi chạy với `--autostart`:** chỉ thông báo khay + dòng "Có bản vX.Y.Z…" đầu menu, không bật hộp thoại.
- **Hộp thoại cập nhật:** Cập nhật / Bỏ qua bản này / Để sau. Bản `--no-install`: nút chính "Mở trang tải" (`html_url`).
- **Tải về:** `C:\Program Files\FpsOverlay\Temp` (ACL như PawnIO), tên ngẫu nhiên `.exe`, tối đa 400 MB, chỉ HTTPS, kiểm SHA-256. Rồi chạy `<exe> --update-after <PID>` và thoát.
- **`--update-after <PID>`:** chờ PID (đúng `FpsOverlay.exe`) thoát tối đa 30 s; tự cài đè không hỏi, không hỏi PawnIO; hiện cửa sổ tiến trình theo chủ đề đã lưu; xong mở bản đã cài.
- **Dọn file:** bản đã cài xoá `*.exe` trong `Temp` cũ hơn 1 giờ, mỗi lần khởi động.
- `SkippedVersion` lưu trong settings; Khôi phục mặc định giữ nguyên nó.
- Tác vụ khởi động cùng Windows có tham số `--autostart`; tác vụ cũ được tự sửa.
- Phiên bản **1.4.0**.
- Mọi chữ mới có tiếng Việt và tiếng Anh. Build 0 cảnh báo, mọi test qua. Đường FPS mỗi khung hình không cấp phát. Kiểm cập nhật không chặn luồng UI.

## Review Focus
1. **GitHub trả về release thiếu asset, tag lạ (`v1.4`, `1.4.0-beta`), JSON hỏng hoặc 403 do giới hạn lượt gọi:** không báo cập nhật, không lỗi. Test trong Task 1 (`Missing_asset_means_no_update`, `Odd_tags_parse_or_are_ignored`, `Broken_json_is_no_update`).
2. **Đổi chủ đề lúc cửa sổ cài đặt đang mở** (kể cả ô đang xổ ra, đang gõ mã màu): không lỗi thiếu tài nguyên; mọi phần đổi theo. Test cùng tập khoá trong Task 2; kiểm bằng mắt trong Task 3–4.
3. **Mạng rớt giữa lúc tải, hoặc người dùng bấm Huỷ:** xoá file dở, app cũ chạy tiếp. Kiểm bằng mắt trong Task 6.
4. **App cũ không thoát kịp khi bản mới chạy `--update-after`:** chờ tối đa 30 s, rồi báo lỗi như hiện nay, không cài nửa chừng. Test đọc tham số trong Task 1; kiểm tay trong Task 6.
5. **Tác vụ khởi động cùng Windows tạo từ bản 1.3.0** (không có `--autostart`): được sửa lần mở app đầu tiên. Test `Task_without_autostart_argument_needs_repair` trong Task 1.

---

### Task 1: Core — release, phiên bản, quyết định báo, sha256, tham số, tác vụ, SkippedVersion

**Files:**
- Create: `src/FpsOverlay.Core/Update/ReleaseInfo.cs`, `src/FpsOverlay.Core/Update/UpdatePolicy.cs`, `src/FpsOverlay.Core/CommandLine.cs`
- Modify: `src/FpsOverlay.Core/Autostart/AutostartTask.cs`, `OverlaySettings.cs`, `SettingsStore.cs`
- Test: `tests/FpsOverlay.Core.Tests/ReleaseInfoTests.cs`, `UpdatePolicyTests.cs`, `CommandLineTests.cs`, `AutostartTaskTests.cs`, `OverlaySettingsTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ReleaseInfo(Version Version, string Tag, string Notes, string PageUrl, string ExeUrl, string HashUrl)`
    - `static ReleaseInfo? Parse(string json)`: null khi JSON hỏng, thiếu tag, tag không đọc được, hoặc thiếu một trong hai asset (tên so chính xác, không phân biệt hoa thường).
    - Chỉ nhận URL `https://`.
  - `public static class UpdatePolicy`:
    - `static Version? ParseVersion(string? text)`: bỏ `v`/`V` đầu, cắt tại `-` hoặc `+`, cần đúng 3 số, không thì null;
    - `static bool ShouldOffer(Version current, ReleaseInfo? latest, string? skippedVersion, bool manual)`: false nếu `latest` null hoặc không lớn hơn; tự động mà `ParseVersion(skipped)` bằng `latest.Version` thì false; bấm tay thì bỏ qua `skipped`;
    - `static string? ParseSha256File(string text)`: chuỗi 64 hex đầu tiên, đổi sang chữ thường; không có thì null.
  - `public sealed record CommandLine(bool Uninstall, bool NoInstall, bool Autostart, int? UpdateAfterPid, bool PawnIoOffered)`
    - `static CommandLine Parse(IReadOnlyList<string> args)`: không phân biệt hoa thường; `--update-after` cần số nguyên dương ngay sau, sai thì null.
    - Giữ nguyên tên tham số cũ: `--uninstall`, `--no-install`, và tham số PawnIO đang có trong `PawnIoSetup.OfferedArgument`. Đọc chuỗi đó rồi chuyển hằng sang Core nếu cần.
  - `AutostartTask.BuildXml(string exePath, string userId)` thêm `<Arguments>--autostart</Arguments>` trong `Exec`.
  - `public const string AutostartArgument = "--autostart"`.
  - `static string? ReadArguments(string xml)`.
  - `static bool NeedsRepair(string xml, string exePath)`: đường dẫn khác (không phân biệt hoa thường) **hoặc** tham số khác `--autostart`.
  - `OverlaySettings.SkippedVersion` (`string?`, mặc định null), có trong Load/Save; `ResetToDefaults` giữ nguyên.

- [ ] **Step 1: Test**
```csharp
// ReleaseInfo — JSON mẫu: tag_name "v1.4.0", body "Notes", html_url "https://github.com/ckhangg/FpsOverlay/releases/tag/v1.4.0",
// assets [{name:"FpsOverlay.exe", browser_download_url:"https://github.com/…/FpsOverlay.exe"}, {name:"FpsOverlay.exe.sha256", browser_download_url:"https://github.com/…/FpsOverlay.exe.sha256"}]
[Fact] public void Parses_release()            // Version 1.4.0, Notes "Notes", ExeUrl/HashUrl đúng
[Fact] public void Missing_asset_means_no_update()   // bỏ asset .sha256 → null
[Fact] public void Broken_json_is_no_update()        // "{", "[]", "" → null
[Fact] public void Http_urls_are_rejected()          // browser_download_url "http://…" → null
[Theory] [InlineData("v1.4.0", "1.4.0")] [InlineData("1.4.0-beta", "1.4.0")] [InlineData("V2.0.1+abc", "2.0.1")]
public void Odd_tags_parse_or_are_ignored(string tag, string expected)   // cộng: "v1.4" → ParseVersion null → Parse trả null
// UpdatePolicy
[Fact] public void Offers_only_newer()           // current 1.4.0: latest 1.4.1 → true, 1.4.0 → false, 1.3.9 → false
[Fact] public void Skipped_version_suppressed_only_when_automatic()   // skipped "1.4.1": auto false, manual true; latest 1.4.2 auto true
[Fact] public void Sha256_file_parsing()         // "ABCD…(64 hex) *FpsOverlay.exe\n" → lowercase 64 hex; "nope" → null
// CommandLine
[Fact] public void Parses_flags()                // ["--Autostart"] → Autostart; ["--update-after","1234"] → 1234; ["--update-after","x"] → null; ["--update-after"] → null
// AutostartTask
[Fact] public void Xml_has_autostart_argument()  // ReadArguments(BuildXml(p,u)) == "--autostart"
[Fact] public void Task_without_autostart_argument_needs_repair()   // XML cũ (không có Arguments) → NeedsRepair true; XML mới cùng đường dẫn → false; khác đường dẫn → true
// Settings
[Fact] public void Skipped_version_round_trips_and_survives_reset()
```
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Cài.
  - `App.OnStartup` chuyển sang dùng `CommandLine.Parse` (giữ nguyên hành vi hiện có).
  - `AutostartService.QueryAndRepair` dùng `NeedsRepair`.
- [ ] **Step 4:** `dotnet test` PASS; `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 5:** Commit `feat(core): release parsing, update policy, command line, autostart argument`.

### Task 2: Hạ tầng chủ đề — khoá `Ui.*`, gắn động, ThemeManager, test cùng tập khoá

**Files:**
- Create: `src/FpsOverlay/ThemeManager.cs`, `tests/FpsOverlay.Core.Tests/ThemeKeysTests.cs`
- Modify: `src/FpsOverlay/Themes/Cyberpunk.xaml`, `src/FpsOverlay/App.xaml`, `src/FpsOverlay/App.xaml.cs`, `SettingsWindow.xaml(.cs)`, `InstallWindow.xaml(.cs)`

**Interfaces:**
- Produces:
  - Mọi khoá `Cp.X` đổi thành `Ui.X`. Có thể đổi tên theo vai trò, ví dụ `Cp.Yellow` → `Ui.Accent`, `Cp.Cyan` → `Ui.Secondary`, `Cp.Red` → `Ui.Danger`.
  - Thêm khoá:
    - `Ui.ExpandGlyph`, `Ui.CollapseGlyph` (`sys:String`, Cyberpunk "▾"/"▴");
    - `Ui.OnText`, `Ui.OffText` (`sys:String`; app ghi đè theo ngôn ngữ).
  - `static class ThemeManager`:
    - `static void Apply(OverlayStyle style)`: hoán đổi đúng một merged dictionary trong `Application.Current.Resources`, nạp `pack://application:,,,/Themes/<StyleName>.xaml`. File chưa có thì dùng Cyberpunk;
    - `static void ApplyLanguage(AppLanguage lang)`: đặt `Ui.OnText`/`Ui.OffText` ("BẬT"/"TẮT", "ON"/"OFF") ở cấp `Application.Resources`, ngoài file chủ đề.
  - Mọi `StaticResource Ui.*` trong `SettingsWindow.xaml`, `InstallWindow.xaml` và trong các style của file chủ đề đổi thành `DynamicResource`.
  - Chỗ dựng control bằng code dùng `SetResourceReference(...)` thay `FindResource`.
  - `RefreshToggleTitles` lấy ký hiệu từ `Ui.ExpandGlyph`/`Ui.CollapseGlyph`.
  - App gọi `ThemeManager.Apply(settings.Style)` lúc khởi động (cả nhánh cài, theo settings đã lưu) và mỗi khi `Style` đổi. Gọi `ApplyLanguage` lúc khởi động và khi đổi ngôn ngữ.
  - `ThemeKeysTests`:
    - tìm thư mục `src/FpsOverlay/Themes` bằng cách đi ngược từ `AppContext.BaseDirectory` tới thư mục chứa `FpsOverlay.sln` hoặc `.git`;
    - đọc mọi `*.xaml` trong đó bằng `XDocument`, lấy tập `x:Key`;
    - assert mọi file có **cùng tập khoá**;
    - assert mọi `{DynamicResource Ui.X}` / `{StaticResource Ui.X}` trong `SettingsWindow.xaml`, `InstallWindow.xaml`, `UpdateWindow.xaml` (nếu có) đều nằm trong tập đó.

- [ ] **Step 1: Test** `ThemeKeysTests` (`All_theme_files_define_the_same_keys`, `Windows_use_only_defined_keys`) → FAIL (khoá `Ui.*` chưa có).
- [ ] **Step 2:** Đổi tên khoá, gắn động, `ThemeManager`. Cyberpunk phải **trông y như trước**.
- [ ] **Step 3:** `dotnet test` PASS; build 0 cảnh báo.
- [ ] **Step 4:** Commit `refactor(ui): theme dictionaries with Ui.* keys and runtime switching`.

### Task 3: Chủ đề Cổ điển và Neon tím

**Files:**
- Create: `src/FpsOverlay/Themes/Classic.xaml`, `src/FpsOverlay/Themes/NeonPurple.xaml`

**Interfaces:**
- Consumes: bộ khoá `Ui.*` của Task 2 (`ThemeKeysTests` buộc đủ khoá).
- Produces: hai file với đúng màu, font, hình dáng ở bảng spec phần A và bản vẽ `themed-settings-v5.html`.
  - Neon: bo góc 14, viền `#C64BFF` phát sáng, nút viên thuốc. Cửa sổ có thể cần `AllowsTransparency`; nếu cần thì đặt bằng style hoặc thuộc tính lấy từ tài nguyên. Không được làm hỏng Cổ điển và Cyberpunk.
  - Neon tiêu đề: Chakra Petch phát sáng.
- [ ] **Step 1:** Tạo hai file. `ThemeKeysTests` PASS.
- [ ] **Step 2:** Build 0 cảnh báo; test qua.
- [ ] **Step 3: Kiểm bằng mắt** (Admin; không có thì ghi "chờ kiểm tay"): đổi qua lại 4 phong cách khi cửa sổ cài đặt đang mở, so với bản vẽ; chữ không đè nhau ở cả hai ngôn ngữ.
- [ ] **Step 4:** Commit `feat(ui): Classic and Neon Purple window themes`.

### Task 4: Chủ đề Terminal xanh

**Files:**
- Create: `src/FpsOverlay/Themes/TerminalGreen.xaml`

**Interfaces:**
- Consumes: khoá `Ui.*`, `Ui.OnText`/`Ui.OffText`, `Ui.ExpandGlyph`/`Ui.CollapseGlyph` (đặt `[+]`/`[-]`).
- Produces:
  - **Công tắc** (`ToggleButton`): vẽ `[` + `Ui.OnText`/`Ui.OffText` + `]`.
  - **Thanh trượt** (`Slider`): template vẽ 10 ô `#`/`-` theo `(Value-Minimum)/(Maximum-Minimum)` (converter làm tròn), bấm/kéo trên dải đặt giá trị theo vị trí, phím mũi tên vẫn chạy.
  - **Nút:** `[ ` + nội dung + ` ]`.
  - **Lựa chọn đang chọn:** nền `#33FF66`, chữ `#000800`.
  - Nền `#000800`, vạch quét, VT323, cỡ chữ khoảng 1,25 lần.
  - Converter (nếu cần) đặt trong `src/FpsOverlay/Themes/TerminalConverters.cs`. Phép tính số ô là hàm thuần trong Core `public static class AsciiBar { static string Render(double value, double min, double max, int cells = 10); }` có test (`[######----]` ở 60%, min = max → toàn `-`).
- [ ] **Step 1: Test** `AsciiBar` → FAIL → cài → PASS.
- [ ] **Step 2:** Tạo theme. `ThemeKeysTests` PASS. Build 0 cảnh báo.
- [ ] **Step 3: Kiểm bằng mắt** (Admin): Terminal như bản vẽ; kéo thanh trượt dạng chữ; Tab/Space trên công tắc.
- [ ] **Step 4:** Commit `feat(ui): Terminal Green window theme with text controls`.

### Task 5: Kiểm cập nhật, hộp thoại, khay, nút trong cài đặt

**Files:**
- Create: `src/FpsOverlay/UpdateService.cs`, `src/FpsOverlay/UpdateWindow.xaml(.cs)`
- Modify: `App.xaml.cs`, `TrayIcon.cs`, `SettingsWindow.xaml(.cs)`, `Loc.cs`

**Interfaces:**
- Consumes: `ReleaseInfo`, `UpdatePolicy`, `CommandLine.Autostart`, `SkippedVersion` (Task 1); chủ đề `Ui.*` (Task 2).
- Produces:
  - `sealed class UpdateService`:
    - `Task<ReleaseInfo?> CheckAsync(CancellationToken)` (dùng `HttpClient` riêng, timeout 10 s);
    - `static Version CurrentVersion` (từ assembly, `UpdatePolicy.ParseVersion`).
  - `App`:
    - khởi động xong, đợi khoảng 5 s rồi `CheckAsync` trên nền, gọi `UpdatePolicy.ShouldOffer(..., manual:false)`;
    - nếu có: `--autostart` → `tray.ShowUpdateAvailable(release)` (balloon + dòng đầu menu); không thì mở `UpdateWindow`;
    - "Để sau" → vẫn đặt dòng đầu menu;
    - nút tay → `manual:true`, báo kết quả bằng `MessageBox` (mới nhất / lỗi).
  - `TrayIcon`:
    - `event Action? CheckUpdatesRequested`, `event Action? UpdateItemClicked`;
    - `void ShowUpdateAvailable(string version, bool balloon)`;
    - mục "Kiểm tra cập nhật" đặt trước dấu ngăn cách và Thoát.
  - `UpdateWindow(ReleaseInfo release, Version current, bool canInstall, AppLanguage language)`:
    - kết quả `UpdateChoice { Update, Skip, Later, OpenPage }`;
    - nút chính "Cập nhật" hoặc "Mở trang tải" khi `!canInstall`;
    - "Bỏ qua bản này" lưu `SkippedVersion`.
    - Task 5 chỉ dừng ở lựa chọn; Task 6 nối phần tải.
  - Cài đặt, mục "// HỆ THỐNG": nút "Kiểm tra cập nhật".
  - Loc (vi/en):

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `CheckUpdates` | "Kiểm tra cập nhật" | "Check for updates" |
    | `UpdateTitle` | "Có bản cập nhật" | "Update available" |
    | `UpdateVersions` | "Phiên bản hiện tại: {0} → Bản mới: {1}" | "Current version: {0} → New: {1}" |
    | `UpdateNow` | "Cập nhật" | "Update" |
    | `UpdateSkip` | "Bỏ qua bản này" | "Skip this version" |
    | `UpdateLater` | "Để sau" | "Later" |
    | `UpdateOpenPage` | "Mở trang tải" | "Open download page" |
    | `UpToDate` | "Bạn đang dùng bản mới nhất ({0})." | "You're on the latest version ({0})." |
    | `UpdateCheckFailed` | "Không kiểm tra được cập nhật." | "Couldn't check for updates." |
    | `TrayUpdateAvailable` | "Có bản {0}…" | "Version {0} available…" |
    | `UpdateBalloon` | "Có bản FPS Overlay {0} — bấm để xem" | "FPS Overlay {0} is available — click to view" |

- [ ] **Step 1:** Cài. Test `Every_key_has_both_languages` qua.
- [ ] **Step 2:** Build 0 cảnh báo; test qua.
- [ ] **Step 3: Kiểm bằng mắt** (Admin): hộp thoại theo chủ đề; nút tay khi không có mạng báo lỗi; khi đã mới nhất báo đúng.
- [ ] **Step 4:** Commit `feat(update): check GitHub for updates, update dialog, tray and settings entry`.

### Task 6: Tải, kiểm băm, chuyển giao `--update-after`, dọn file

**Files:**
- Modify: `UpdateService.cs`, `UpdateWindow.xaml(.cs)`, `App.xaml.cs` (`RunSetup`), `InstallWindow.xaml(.cs)`, `Installer.cs`, `PawnIoSetup.cs` (tách hàm tạo thư mục Temp an toàn dùng chung)

**Interfaces:**
- Consumes: Task 1 (`ParseSha256File`, `CommandLine.UpdateAfterPid`), Task 5 (`UpdateWindow`, `UpdateChoice.Update`).
- Produces:
  - `static string SecureTemp.PrepareDirectory()`: tách từ `PawnIoSetup.PrepareTempDirectory`, cùng hành vi; PawnIO và cập nhật cùng dùng.
  - `UpdateService.DownloadAsync(ReleaseInfo, IProgress<double>, CancellationToken) -> string exePath`:
    - tải `.sha256` trước, rồi exe;
    - từ chối khi dài hơn 400 MB và khi URL sau chuyển hướng không phải HTTPS;
    - ghi bằng `FileMode.CreateNew` vào `SecureTemp.PrepareDirectory()`;
    - so SHA-256; sai, huỷ hay lỗi thì xoá file và ném lỗi.
  - `UpdateWindow` ở chế độ tải: thanh tiến trình và nút Huỷ. Thành công: `Process.Start(exePath, "--update-after " + Environment.ProcessId)` rồi `Application.Current.Shutdown()`.
  - `RunSetup` khi có `UpdateAfterPid`:
    1. chờ PID (chỉ khi tên tiến trình là `FpsOverlay` và đường dẫn là exe đã cài), tối đa 30 s; quá thì báo `InstalledCopyRunning` và thoát;
    2. hiện `InstallWindow` ở chế độ tự chạy (không nút, không ô PawnIO, chữ "Đang cập nhật lên vX.Y.Z…"), gọi luồng cài/cập nhật sẵn có theo `Installer.Decide()`;
    3. xong thì `LaunchInstalled()` không kèm cờ PawnIO.
  - Bản đã cài khi khởi động: xoá `*.exe` trong thư mục Temp cũ hơn 1 giờ (try/catch từng file).
  - Loc:

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `UpdateDownloading` | "Đang tải bản cập nhật…" | "Downloading update…" |
    | `UpdateCancel` | "Huỷ" | "Cancel" |
    | `UpdateFailed` | "Không cập nhật được." | "Update failed." |
    | `UpdatingTo` | "Đang cập nhật lên {0}…" | "Updating to {0}…" |

- [ ] **Step 1:** Cài. Build 0 cảnh báo; test qua.
- [ ] **Step 2: Kiểm tay** (Admin, chờ người dùng): ghi rõ trong báo cáo các bước kiểm (Task 7 đưa vào HANDOFF).
- [ ] **Step 3:** Commit `feat(update): download, verify and hand over to the new version`.

### Task 7: Phiên bản 1.4.0, README, HANDOFF

**Files:**
- Modify: `src/FpsOverlay/FpsOverlay.csproj` (`1.4.0`), `README.md`, `HANDOFF.md`

- [ ] **Step 1:** Version 1.4.0.
- [ ] **Step 2:** `README.md` (vi + en): mục "Cập nhật" và ghi chú bảo mật về mã băm (không chống tài khoản GitHub bị chiếm).
- [ ] **Step 3:** `HANDOFF.md`:
  - lịch sử "Đợt 8" (chưa merge, nhánh `feat/themed-ui-updater`);
  - tài liệu spec/plan;
  - quyết định mới: font Neon, `--autostart`, thư mục Temp, `SkippedVersion`;
  - mục 9 "Đợt 8": kiểm bằng mắt 4 chủ đề; kiểm cập nhật thật (phát hành bản thử 1.4.1, cần người dùng đồng ý); rút mạng giữa lúc tải; bấm Huỷ; tác vụ 1.3.0 được thêm `--autostart`; thông báo khay khi tự chạy.
- [ ] **Step 4:** Build 0 cảnh báo; test qua.
- [ ] **Step 5:** Commit `chore: version 1.4.0, README and handoff for batch 8`.
