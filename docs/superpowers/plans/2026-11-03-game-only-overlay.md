# Đợt 6: chỉ hiện khi chơi game, gọn cửa sổ cài đặt — Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overlay tự ẩn khi cửa sổ phía trước không phải game và hiện ngay khi vào game. Thêm danh sách "Luôn hiện / Không bao giờ hiện". Gom 10 công tắc thông số thành một ô checkbox mở tại chỗ.

**Architecture:**
- Phần thuần trong `FpsOverlay.Core`, có test: danh sách tên ứng dụng, quy tắc "có phải game không", quyết định hiện/ẩn, cài đặt mới.
- App WPF làm ba việc:
  - mỗi 250 ms dò cửa sổ phía trước (kiểu cửa sổ, phủ kín màn hình, đường dẫn exe được nhớ theo PID), rồi gọi Core để quyết định;
  - gom mọi chỗ hiện/ẩn overlay vào một hàm duy nhất;
  - thêm hai mục khay và các ô mở tại chỗ trong cửa sổ cài đặt.

**Tech Stack:** C# / .NET 10, WPF + WinForms NotifyIcon, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-03-game-only-overlay-design.md`

## Global Constraints
- Overlay hiện khi cửa sổ phía trước là game, tức thoả một trong ba điều kiện:
  - phủ kín màn hình chứa nó;
  - exe nằm trong đường dẫn chứa `\steamapps\common\`, `\XboxGames\` hoặc `\WindowsApps\` (không phân biệt hoa thường);
  - tên exe có trong "Luôn hiện".

  "Không bao giờ hiện" thắng tất cả. Với khung ApplicationFrameHost, xét **mọi** tiến trình ứng viên.
- Thứ tự ưu tiên hiện/ẩn:
  1. ẩn bằng tay → ẩn;
  2. đang di chuyển → hiện;
  3. đang xem trước cài đặt → hiện;
  4. tuỳ chọn tắt → hiện;
  5. là game → hiện, không thì ẩn.
- Cửa sổ của **chính app** ở phía trước (menu khay, cửa sổ cài đặt, overlay): **giữ nguyên quyết định trước đó**, không đổi.
- Kiểm mỗi **250 ms**.
- `ShowOnlyInGame` mặc định `true`; `AlwaysShowApps`, `NeverShowApps` mặc định rỗng.
- Khôi phục mặc định **không** xoá hai danh sách.
- Một tên có ở cả hai danh sách thì giữ ở "Không bao giờ hiện". Tên so không phân biệt hoa thường, bỏ dòng rỗng và trùng.
- Overlay tự ẩn mà log tắt: dừng PresentMon và cảm biến. Log bật: vẫn chạy.
- Chữ "Ẩn FPS"/"Hiện FPS" ở khay chỉ theo lựa chọn bằng tay.
- 10 công tắc thông số được thay bằng nút "Chọn thông số hiển thị ▾" kèm "Đang hiện N/10" và ô 10 checkbox, 2 cột, mở tại chỗ, mặc định thu gọn. Ghi chú PawnIO vẫn hiện khi ô thu gọn. Hai danh sách ứng dụng dùng cùng kiểu.
- Mọi chữ mới có tiếng Việt và tiếng Anh trong `Loc`.
- Đường FPS mỗi khung hình không cấp phát. Ngoài chế độ di chuyển, overlay bấm xuyên và không chiếm focus.
- `<Version>` = `1.2.0`. Build 0 cảnh báo, mọi test qua.

## Review Focus
1. **Mở menu khay giữa lúc chơi game:** overlay không được tắt rồi bật lại, vì cửa sổ phía trước lúc đó là của chính app. Test trong Task 1 (`Own_window_in_front_keeps_previous_decision`).
2. **Không đọc được đường dẫn exe** (tiến trình đã thoát, bị từ chối quyền): coi như không nằm trong thư mục game, không lỗi. Test trong Task 1 (`Unknown_path_is_not_a_game_folder`).
3. **Tên viết hoa thường khác nhau hoặc trùng giữa hai danh sách:** `EldenRing.exe` và `eldenring.exe` là một; trùng thì "Không bao giờ" thắng. Test trong Task 1 (`Names_compare_case_insensitively_and_never_wins`).
4. **File `settings.json` của bản 1.1.0** (không có trường mới): tuỳ chọn bật, hai danh sách rỗng, không lỗi. Test trong Task 1 (`Old_file_gets_game_only_defaults`).
5. **Ẩn bằng tay rồi vào game:** vẫn ẩn. **Đang tự ẩn mà bật chế độ di chuyển:** overlay hiện để kéo. Test trong Task 1 (`Manual_hide_beats_game`, `Moving_shows_even_when_not_game`).

---

### Task 1: Core — danh sách ứng dụng, quy tắc game, quyết định hiện/ẩn, cài đặt mới

**Files:**
- Create: `src/FpsOverlay.Core/GameOnly/AppNames.cs`, `src/FpsOverlay.Core/GameOnly/GameWindowRule.cs`
- Modify: `src/FpsOverlay.Core/Settings/OverlaySettings.cs`, `src/FpsOverlay.Core/Settings/SettingsStore.cs`
- Test: `tests/FpsOverlay.Core.Tests/GameWindowRuleTests.cs`, `AppNamesTests.cs`, `OverlaySettingsTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `[JsonConverter(...)] public sealed class AppNames : IEquatable<AppNames>, IReadOnlyList<string>` — bất biến; tên đã chuẩn hoá: cắt khoảng trắng, chỉ lấy phần tên file (`Path.GetFileName`), bỏ rỗng, bỏ trùng không phân biệt hoa thường, giữ cách viết của lần đầu.
    - `static AppNames Empty`
    - `static AppNames From(IEnumerable<string?> names)`
    - `bool Contains(string? exeName)` (không phân biệt hoa thường)
    - `AppNames With(string exeName)`
    - `AppNames Without(string exeName)`
    - `Equals` so theo nội dung (không phân biệt hoa thường, cùng thứ tự).
    - JSON ghi/đọc thành mảng chuỗi.
  - `public enum ForegroundKind { None, Own, Desktop, Other }`
  - `public static class GameWindowRule`:
    - `static bool IsGameFolder(string? exePath)` — chứa một trong `\steamapps\common\`, `\XboxGames\`, `\WindowsApps\` (OrdinalIgnoreCase); null/rỗng → false.
    - `static bool IsGame(bool coversMonitor, IReadOnlyList<string?> exePaths, AppNames always, AppNames never)` — tên exe của bất kỳ đường dẫn nào nằm trong `never` → false; còn lại `coversMonitor || any IsGameFolder || any always.Contains(name)`.
    - `static bool ShouldShow(bool hiddenByUser, bool moving, bool previewing, bool onlyInGame, ForegroundKind kind, bool isGame, bool previous)` — đúng thứ tự ưu tiên ở Global Constraints. Ở bước 5: `Own` → `previous`; `None`/`Desktop` → false; `Other` → `isGame`.
  - `OverlaySettings`:
    - `bool ShowOnlyInGame` (true);
    - `AppNames AlwaysShowApps`, `AppNames NeverShowApps` (`AppNames.Empty`);
    - `OverlaySettings WithAlwaysShow(string exe)` (thêm vào Always, bỏ khỏi Never);
    - `WithNeverShow(string exe)` (ngược lại);
    - `WithoutApp(string exe)` (bỏ khỏi cả hai);
    - `Normalize` gỡ khỏi Always mọi tên có trong Never;
    - `ResetToDefaults` giữ `AlwaysShowApps`/`NeverShowApps` (đặt `ShowOnlyInGame` về true).
  - `SettingsStore.Load` đọc ba trường; mảng hỏng → rỗng.

- [ ] **Step 1: Test**
```csharp
[Theory]
[InlineData(@"D:\SteamLibrary\steamapps\common\ELDEN RING\Game\eldenring.exe", true)]
[InlineData(@"C:\XboxGames\Forza Horizon 5\Content\ForzaHorizon5.exe", true)]
[InlineData(@"C:\Program Files\WindowsApps\Microsoft.X_1.0\game.exe", true)]
[InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", false)]
public void Game_folders(string path, bool expected)
[Fact] public void Unknown_path_is_not_a_game_folder()   // IsGameFolder(null) == false, IsGameFolder("") == false
[Fact] public void Covers_monitor_is_game()               // IsGame(true, [@"C:\x\vlc.exe"], Empty, Empty) == true
[Fact] public void Always_list_makes_windowed_app_a_game() // IsGame(false, [@"C:\Games\celeste.exe"], From(["Celeste.exe"]), Empty) == true
[Fact] public void Never_list_beats_fullscreen_and_folder() // IsGame(true, [steam path], Empty, From(["eldenring.exe"])) == false
[Fact] public void Frame_host_candidates_are_all_checked()  // paths [@"C:\Windows\System32\ApplicationFrameHost.exe", @"C:\Program Files\WindowsApps\G_1\g.exe"] → true
[Fact] public void Names_compare_case_insensitively_and_never_wins() // From(["EldenRing.exe","eldenring.exe"]).Count == 1; settings with "a.exe" in both → Normalize keeps only Never
[Fact] public void Own_window_in_front_keeps_previous_decision()   // ShouldShow(false,false,false,true,Own,false,previous:true) == true; previous:false → false
[Fact] public void Desktop_hides_when_game_only()       // ShouldShow(false,false,false,true,Desktop,false,true) == false
[Fact] public void Option_off_always_shows()            // ShouldShow(false,false,false,false,Desktop,false,false) == true
[Fact] public void Manual_hide_beats_game()             // ShouldShow(true,true,true,false,Other,true,true) == false
[Fact] public void Moving_shows_even_when_not_game()    // ShouldShow(false,true,false,true,Desktop,false,false) == true
[Fact] public void Previewing_shows()                   // ShouldShow(false,false,true,true,Desktop,false,false) == true
[Fact] public void With_always_and_never_move_between_lists() // WithAlwaysShow("a.exe").WithNeverShow("A.EXE") → Never has it, Always empty; WithoutApp removes from both
[Fact] public void Reset_keeps_app_lists()
[Fact] public void Old_file_gets_game_only_defaults()   // {"Style":"Classic"} → ShowOnlyInGame true, lists empty
[Fact] public void Store_round_trips_app_lists()        // lists survive Save→Load; JSON holds string arrays
[Fact] public void Settings_with_same_lists_are_equal() // two Loads of the same file are Equal (record equality, needed by SettingsService change check)
```
- [ ] **Step 2:** `dotnet test` → FAIL (chưa có kiểu).
- [ ] **Step 3:** Cài theo Interfaces.
- [ ] **Step 4:** `dotnet test` → PASS; `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 5:** Commit `feat(core): game-only overlay rules, app lists and settings`.

### Task 2: App — dò cửa sổ mỗi 250 ms, một chỗ hiện/ẩn, hai mục khay

**Files:**
- Create: `src/FpsOverlay/ForegroundWindowInfo.cs`
- Modify: `src/FpsOverlay/App.xaml.cs`, `src/FpsOverlay/TrayIcon.cs`, `src/FpsOverlay/OverlayWindow.xaml.cs`, `src/FpsOverlay/SessionLogger.cs`, `src/FpsOverlay/NativeMethods.cs`, `src/FpsOverlay.Core/Localization/Loc.cs`

**Interfaces:**
- Consumes: `GameWindowRule`, `ForegroundKind`, `AppNames`, `OverlaySettings.ShowOnlyInGame/AlwaysShowApps/NeverShowApps/WithAlwaysShow/WithNeverShow` (Task 1).
- Produces:
  - `static class ForegroundWindowInfo` (luồng UI):
    - `ForegroundKind Kind(IntPtr hwnd)`: Zero → None; tiến trình của app → Own; shell window, `Progman`, `WorkerW` → Desktop; còn lại → Other.
    - `bool CoversMonitor(IntPtr hwnd)`: chuyển từ `SessionLogger.ForegroundCoversMonitor`; SessionLogger gọi lại hàm này, không để hai bản.
    - `string? ExePath(int pid)`: `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `QueryFullProcessImageName`, nhớ theo PID (cache cỡ nhỏ, xoá mục cũ khi PID không còn trong lần dò); lỗi → null.
  - `OverlayWindow.ExternalForeground()` dùng `ForegroundWindowInfo.Kind(...) == Other` để không lặp phép lọc.
  - `App`:
    - `bool _hiddenByUser`; `bool _shown` (quyết định trước); `DispatcherTimer _gameTimer` 250 ms → `UpdateVisibility()`.
    - `UpdateVisibility()`:
      - lấy `hwnd = GetForegroundWindow()`, `kind`;
      - với `Other`: `paths` = `ExePath` của mọi PID trong `ForegroundProcess.GetCandidates()`, `isGame = GameWindowRule.IsGame(CoversMonitor(hwnd), paths, s.AlwaysShowApps, s.NeverShowApps)`, và nhớ `_lastAppName = Path.GetFileName(paths[0])` (tiến trình chính theo thứ tự GetCandidates);
      - `show = GameWindowRule.ShouldShow(_hiddenByUser, overlay.IsMoving, IsPreviewing, s.ShowOnlyInGame, kind, isGame, _shown)`;
      - nếu khác trạng thái hiện tại thì `SetOverlayShown(show)`.
    - `SetOverlayShown(bool)`: **chỗ duy nhất** gọi `overlay.Show()/Hide()`. Nó gỡ/đăng ký `OnRendering`, và khi ẩn thì gọi `EndMove(true)` nếu đang di chuyển, reset hai `HeatAlarm` và `SetAlerts(false,false)`. Sau đó gọi `ApplyResourceState()`.
    - `ToggleOverlay` chỉ đảo `_hiddenByUser`, cập nhật chữ khay theo `!_hiddenByUser`, rồi gọi `UpdateVisibility()`.
    - `ApplyResourceState` giữ nguyên ý nghĩa (`overlay.IsVisible || LoggingEnabled`).
    - Gọi `UpdateVisibility()` ngay khi `settings.Changed`, khi mở/đóng cửa sổ cài đặt và khi bắt đầu/kết thúc di chuyển, để không chờ nhịp 250 ms.
    - `MoveRequested` khi overlay đang tự ẩn: `BeginMove` phải chạy sau khi overlay đã hiện. Gọi `SetOverlayShown(true)` trước, rồi `BeginMove()`.
  - `TrayIcon`:
    - `event Action? AlwaysShowRequested`, `event Action? NeverShowRequested`;
    - `void SetAppTarget(string? exeName)`: chữ hai mục theo tên, null → hai mục mờ và hiện "—";
    - hai mục đặt sau "Di chuyển overlay";
    - mục "Di chuyển overlay" `Enabled = !hiddenByUser` (đổi tham số `overlayVisible` của `Apply` thành ý nghĩa "người dùng không ẩn tay").
    - App gọi `SetAppTarget(_lastAppName)` trong `MenuOpening`. `AlwaysShowRequested` → `settings.Update(s => s.WithAlwaysShow(_lastAppName))`; Never tương tự.
  - Loc:

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `TrayAlwaysShow` | "Luôn hiện cho {0}" | "Always show for {0}" |
    | `TrayNeverShow` | "Không bao giờ hiện cho {0}" | "Never show for {0}" |

    Định dạng bằng `string.Format` ở chỗ gọi.

- [ ] **Step 1:** Cài theo Interfaces.
- [ ] **Step 2:** `dotnet test` → PASS (các test `SessionLog`/`GameSessionTracker` cũ vẫn qua); `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 3: Kiểm bằng mắt** (cần Admin; không có quyền thì ghi "chờ kiểm tay"):
  - desktop → overlay ẩn; vào game borderless → hiện ngay;
  - mở menu khay giữa game → overlay không chớp tắt;
  - "Luôn hiện cho notepad.exe" → overlay hiện khi Notepad ở phía trước;
  - "Không bao giờ hiện" cho trình duyệt rồi xem video toàn màn hình → không hiện;
  - "Ẩn FPS" rồi vào game → vẫn ẩn;
  - "Di chuyển overlay" từ desktop → overlay hiện để kéo.
- [ ] **Step 4:** Commit `feat(overlay): show overlay only while gaming, tray app lists`.

### Task 3: Cửa sổ cài đặt — ô thông số mở tại chỗ, công tắc mới, danh sách ứng dụng

**Files:**
- Modify: `src/FpsOverlay/SettingsWindow.xaml`, `src/FpsOverlay/SettingsWindow.xaml.cs`, `src/FpsOverlay/Themes/Cyberpunk.xaml`, `src/FpsOverlay.Core/Localization/Loc.cs`
- Test: `tests/FpsOverlay.Core.Tests/OverlaySettingsTests.cs`

**Interfaces:**
- Consumes: `OverlaySettings.ShowOnlyInGame`, `AlwaysShowApps`, `NeverShowApps`, `WithoutApp` (Task 1); 10 thuộc tính `Show*` có sẵn.
- Produces:
  - `int OverlaySettings.VisibleStatCount` (đếm 10 cờ `ShowOnePercentLow, ShowGpuLoad, ShowGpuTemp, ShowGpuMemory, ShowGpuClock, ShowGpuPower, ShowGpuFan, ShowCpuLoad, ShowCpuTemp, ShowRam`) và `const int StatCount = 10`.
  - Mục "// THÔNG SỐ":
    - một nút kiểu Cyberpunk "Chọn thông số hiển thị ▾" (khi mở đổi thành "▴") và một `TextBlock` "Đang hiện N/10";
    - bên dưới là panel `Collapsed` chứa 10 `CheckBox` theo style mới `Cp.Check` trong `Cyberpunk.xaml`, 2 cột theo nhóm: cột trái 1%, GPU tải, GPU nhiệt, VRAM, CLK; cột phải PWR, FAN, CPU tải, CPU nhiệt, RAM;
    - bấm nút thì đảo `Visibility`. Mỗi lần mở cửa sổ, panel bắt đầu thu gọn;
    - xoá 10 cặp nhãn + công tắc cũ. `OnStatChanged` đọc từ các CheckBox, giữ guard `_refreshing`;
    - `PawnIoPanel` nằm **ngoài** panel thu gọn, ngay dưới nút.
  - Mục "// GIAO DIỆN": công tắc `OnlyInGameSwitch` (`ShowOnlyInGame`).
  - Ngay dưới công tắc đó: nút "Danh sách ứng dụng ▾" kèm tóm tắt "Luôn hiện: A · Không bao giờ: B". Panel thu gọn gồm hai nhóm, mỗi nhóm liệt kê tên exe kèm nút "Xoá" (→ `WithoutApp`). Nhóm rỗng hiện "(trống)". Dựng lại danh sách trong `Refresh` khi hai danh sách đổi.
  - Loc:

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `OnlyInGame` | "Chỉ hiện khi chơi game" | "Only show while gaming" |
    | `ChooseStats` | "Chọn thông số hiển thị" | "Choose stats" |
    | `StatsSummary` | "Đang hiện {0}/{1}" | "Showing {0}/{1}" |
    | `AppLists` | "Danh sách ứng dụng" | "App lists" |
    | `AppListsSummary` | "Luôn hiện: {0} · Không bao giờ: {1}" | "Always: {0} · Never: {1}" |
    | `AlwaysShowList` | "Luôn hiện" | "Always show" |
    | `NeverShowList` | "Không bao giờ hiện" | "Never show" |
    | `AppListEmpty` | "(trống)" | "(empty)" |
    | `RemoveApp` | "Xoá" | "Remove" |

    Các khoá `Stat*` cũ dùng lại cho nhãn checkbox.
  - Đánh số lại hàng của Grid cài đặt cho khớp; không để trùng hàng.

- [ ] **Step 1: Test**
```csharp
[Fact] public void Visible_stat_count_counts_ten_switches()   // Defaults: 6 (4 GPU extras off); all on: 10; all off: 0; StatCount == 10
```
- [ ] **Step 2:** `dotnet test` → FAIL; cài `VisibleStatCount`; → PASS.
- [ ] **Step 3:** Cài phần giao diện theo Interfaces. `dotnet build src/FpsOverlay` 0 cảnh báo; `dotnet test` qua.
- [ ] **Step 4: Kiểm bằng mắt** (Admin):
  - cửa sổ cài đặt ngắn hơn rõ rệt;
  - mở/thu ô thông số; tick/bỏ tick đổi overlay ngay; "Đang hiện N/10" đúng;
  - ghi chú PawnIO vẫn thấy khi ô thu gọn;
  - danh sách ứng dụng hiện đúng và xoá được;
  - đổi ngôn ngữ thì mọi chữ mới đổi theo.
- [ ] **Step 5:** Commit `feat(settings): collapsible stat checklist, game-only switch and app lists`.

### Task 4: Phiên bản và tài liệu

**Files:**
- Modify: `src/FpsOverlay/FpsOverlay.csproj` (`<Version>1.2.0</Version>`), `HANDOFF.md`

- [ ] **Step 1:** Đổi version.
- [ ] **Step 2:** `HANDOFF.md`:
  - bảng lịch sử thêm "Đợt 6" (chưa merge, nhánh `feat/game-only-overlay`);
  - mục tài liệu thêm spec và plan này;
  - mục 5 cập nhật trạng thái và số test;
  - mục 7 thêm các quyết định: cửa sổ của app ở phía trước giữ quyết định trước; Reset giữ danh sách; "Không bao giờ" thắng.
- [ ] **Step 3:** Thêm vào mục 9 phần "Đợt 6": các bước kiểm bằng mắt của Task 2 và Task 3, cộng thêm:
  - cập nhật đè bản 1.1.0 → 1.2.0, cài đặt cũ còn nguyên và tuỳ chọn mới đang bật;
  - game Game Pass chạy cửa sổ được nhận ra;
  - tắt log, desktop: PresentMon biến khỏi Task Manager; vào game thì overlay hiện và có số sau vài giây.
- [ ] **Step 4:** `dotnet test` qua; `dotnet build src/FpsOverlay` 0 cảnh báo; `dotnet publish src/FpsOverlay -c Release -o publish` thành công.
- [ ] **Step 5:** Commit `chore: version 1.2.0 and handoff for batch 6`.
