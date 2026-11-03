# Thông số, log, nhiều màn hình, cài đặt một file — Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thêm 1% low, GPU/CPU/RAM/nhiệt độ lên overlay một dòng, tự ghi log mỗi buổi chơi, overlay theo màn hình có game, cảnh báo PresentMon, tiết kiệm khi ẩn, và đóng gói thành một file tự cài vào `C:\Program Files\FpsOverlay` (kèm hỏi cài PawnIO).

**Architecture:** Logic thuần (1% low, chọn GPU, định dạng số, nhận biết buổi chơi, CSV/tóm tắt, vị trí overlay, quyết định cài/cập nhật) nằm trong `FpsOverlay.Core` có test. App WPF nối các phần: cảm biến đọc bằng LibreHardwareMonitorLib trên luồng nền mỗi giây, logger ghi file trên luồng nền, overlay chỉ đổi chữ khi giá trị đổi.

**Tech Stack:** C# / .NET 10, WPF + WinForms NotifyIcon, xUnit, LibreHardwareMonitorLib 0.9.6, PresentMon 2.6.0, schtasks.

**Spec:** `docs/superpowers/specs/2026-11-03-stats-logging-install-design.md`

## Global Constraints
- Toàn bộ chữ mới (menu, cảnh báo, cài đặt, hộp thoại cài/gỡ, file tóm tắt) có cả tiếng Việt và tiếng Anh qua `Loc`; test `Every_key_has_both_languages` phải còn pass.
- Nhãn overlay giống nhau ở hai ngôn ngữ: `FPS`, `1%`, `GPU`, `CPU`, `RAM`. Ngăn cách giữa các mục: ` · `.
- Đường cập nhật từng khung hình (`OnRendering` → `SetFps`) không cấp phát và không chờ khoá lâu; cảm biến và ghi file không chạy trên luồng UI.
- Cảm biến đọc mỗi **1 giây**. 1% low overlay trên **10 giây**, phân vị 99 của frametime, đổi ra FPS, cần ≥ 2 giây dữ liệu.
- Nóng: GPU ≥ **85°C**, CPU ≥ **90°C** → màu đỏ của phong cách (Cổ điển `#FF3B30`, Cyberpunk `#FF003C`).
- Cảnh báo PresentMon sau **10 giây** không có khung hình nào.
- Buổi chơi: bắt đầu khi cửa sổ phía trước phủ kín màn hình chứa nó **và** có FPS liên tục **> 10 giây**; kết thúc khi game thoát hoặc vắng **> 60 giây**; game khác đạt điều kiện trong lúc chờ → đóng buổi cũ, mở buổi mới.
- Log: `Documents\FpsOverlay\Logs\<Tên>_<yyyy-MM-dd_HH-mm>.csv` + `<cùng tên>_summary.txt`; cột `Time,Fps,OnePercentLow,GpuLoad,GpuTemp,CpuLoad,CpuTemp,RamUsedGb`; văn hoá bất biến; trống khi không có giá trị; ghi theo lô ~10 giây.
- Thư mục cài: `C:\Program Files\FpsOverlay\` (lấy qua `Environment.SpecialFolder.ProgramFiles`). Lối tắt "FPS Overlay" trong Start Menu chung. Mục gỡ: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FpsOverlay`.
- Phiên bản `<Version>1.0.0</Version>` trong `FpsOverlay.csproj`.
- Không chạy với quyền Admin file nào ở chỗ người dùng thường ghi được. Chỉ bản chạy từ thư mục cài mới được tạo/sửa tác vụ khởi động cùng Windows.
- Thư viện cảm biến lỗi → số đó `--`, app không sập.
- Không cài PawnIO nếu người dùng không tick.

## Review Focus
1. **Laptop hai GPU khi đứng ở desktop:** card rời ngủ (load 0 hoặc không đọc được nhiệt độ) → overlay phải hiện GPU tích hợp hoặc `--`, không được ném lỗi hay nhảy qua lại liên tục giữa hai card. Test trong Task 3 (`BusiestGpu` khi các card có load bằng nhau / null).
2. **Game thoát đột ngột giữa buổi:** tóm tắt vẫn được ghi, CSV không mất dòng đã lô trước đó. Test trong Task 5 (`Process_exit_ends_session_immediately`) và khi tắt app (flush trong Dispose).
3. **Tên tiến trình có ký tự lạ hoặc trùng giờ phút** (hai buổi cùng game trong một phút): tên file hợp lệ và không ghi đè file cũ. Test trong Task 5 (`File_stem_is_sanitized`, `Unique_path_adds_suffix`).
4. **Cài đè khi bản cũ đang chạy hoặc tác vụ khởi động đang trỏ vào thư mục build:** không xoá cài đặt người dùng, tác vụ được trỏ về bản cài. Test trong Task 8 (`InstallDecision` các trường hợp) + kiểm tay.
5. **Đổi màn hình có DPI khác:** overlay đúng góc, đúng cỡ trên màn hình mới, không kẹt ở toạ độ âm. Test trong Task 6 (`Placement_on_secondary_monitor_left_of_primary`).

---

### Task 1: Cảnh báo PresentMon im lặng + gỡ cập nhật khung hình khi ẩn

**Files:**
- Create: `src/FpsOverlay.Core/PresentMonHealth.cs`
- Modify: `src/FpsOverlay.Core/FpsCalculator.cs`, `src/FpsOverlay.Core/Localization/Loc.cs`, `src/FpsOverlay/App.xaml.cs`, `src/FpsOverlay/TrayIcon.cs`
- Test: `tests/FpsOverlay.Core.Tests/PresentMonHealthTests.cs`, `tests/FpsOverlay.Core.Tests/FpsCalculatorTests.cs`

**Interfaces:**
- Produces: `double? FpsCalculator.SecondsSinceAnyFrame()` (null nếu chưa nhận khung nào; dùng đồng hồ tiêm vào). `static bool PresentMonHealth.IsStalled(double secondsSinceStart, double? secondsSinceAnyFrame)` với `const double StallSeconds = 10`. `TrayIcon.SetWarning(string? text)` (null = ẩn dòng cảnh báo, tooltip về như cũ). Khoá Loc mới `PresentMonNoData`.

- [ ] **Step 1: Test**
```csharp
[Theory]
[InlineData(5, null, false)]     // mới chạy 5 giây: chưa cảnh báo
[InlineData(11, null, true)]     // 11 giây chưa có khung nào
[InlineData(30, 2.0, false)]     // có dữ liệu gần đây
[InlineData(30, 10.5, true)]     // im lặng 10,5 giây
public void IsStalled(double sinceStart, double? sinceFrame, bool expected) =>
    Assert.Equal(expected, PresentMonHealth.IsStalled(sinceStart, sinceFrame));

[Fact] public void Seconds_since_any_frame_null_before_first_frame() => Assert.Null(new FpsCalculator().SecondsSinceAnyFrame());
[Fact] public void Seconds_since_any_frame_uses_clock() { double clock = 100; var c = new FpsCalculator(() => clock); c.Add(new(1, 0)); clock += 3; Assert.Equal(3, c.SecondsSinceAnyFrame()!.Value, 3); }
```
- [ ] **Step 2:** `dotnet test` → FAIL (chưa có kiểu/hàm).
- [ ] **Step 3: Cài đặt.** App kiểm tra mỗi giây bằng `DispatcherTimer`, gọi `tray.SetWarning(...)` chỉ khi trạng thái đổi. Dòng cảnh báo là mục đầu menu, `Enabled = false`, kèm tooltip. Chuỗi: vi `"⚠ PresentMon không gửi dữ liệu. Thử chạy lại app với quyền Administrator."`, en `"⚠ PresentMon isn't sending data. Try restarting the app as Administrator."`. Đổi ngôn ngữ thì chữ cảnh báo đổi theo.
- [ ] **Step 4: Gỡ cập nhật khi ẩn.** `ToggleOverlay`: ẩn → `CompositionTarget.Rendering -= OnRendering`; hiện → `+=` (không bao giờ đăng ký hai lần). `Fail` vẫn gỡ như cũ.
- [ ] **Step 5:** `dotnet test` → PASS; `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 6: Commit** `feat: warn in tray when PresentMon sends no data; stop per-frame updates while hidden`

---

### Task 2: Lịch sử frametime và 1% low

**Files:**
- Create: `src/FpsOverlay.Core/FrameTimeHistory.cs`
- Modify: `src/FpsOverlay/App.xaml.cs` (đưa mỗi khung hình vào cả `FpsCalculator` và `FrameTimeHistory`)
- Test: `tests/FpsOverlay.Core.Tests/FrameTimeHistoryTests.cs`

**Interfaces:**
- Produces (thread-safe: `Add` từ luồng đọc, các hàm còn lại từ luồng khác):
  - `FrameTimeHistory(double windowSeconds = 10)`
  - `void Add(FrameSample frame)` — frametime = khoảng cách tới khung trước của cùng pid; giữ `windowSeconds` gần nhất tính theo `TimeSeconds` mới nhất.
  - `double? OnePercentLow(int processId)` và `double? OnePercentLow(IReadOnlyList<int> processIds)` (tiến trình đầu tiên có kết quả).
  - `int CopyFrameTimes(int processId, double afterTimeSeconds, List<double> into)` — thêm vào `into` các frametime có thời điểm khung > `afterTimeSeconds`, trả về số đã thêm; kèm `double LatestTimeSeconds { get; }`. Dùng cho log (Task 5).
  - `static double? OnePercentLowOf(IReadOnlyList<double> frameTimes)` — phân vị 99 kiểu nearest-rank (sắp tăng dần, chỉ số `ceil(0.99·n) − 1`), trả `1 / giá trị`; null nếu rỗng.
- Không cấp phát mỗi lần gọi `OnePercentLow`: dùng lại một mảng đệm để sắp xếp.

- [ ] **Step 1: Test**
```csharp
[Fact] public void Steady_60fps_one_percent_low_is_60() { var h = new FrameTimeHistory(); Feed(h, 1, 0, 1/60.0, 600); Assert.Equal(60, h.OnePercentLow(1)!.Value, 1); }
[Fact] public void Few_stutters_pull_one_percent_low_down() { /* 594 khung 1/60 + 6 khung 0,1 s */ Assert.Equal(10, h.OnePercentLow(1)!.Value, 1); }
[Fact] public void Less_than_two_seconds_is_null() { Feed(h, 1, 0, 1/60.0, 100); Assert.Null(h.OnePercentLow(1)); }
[Fact] public void Old_frames_leave_the_window() { /* 5 s ở 30 fps rồi 10 s ở 120 fps */ Assert.Equal(120, h.OnePercentLow(1)!.Value, 1); }
[Fact] public void Other_process_frames_do_not_mix() { /* pid 2 giật, pid 1 đều 60 */ Assert.Equal(60, h.OnePercentLow(1)!.Value, 1); }
[Fact] public void Candidates_use_first_with_data() => Assert.Equal(60, h.OnePercentLow([7, 1])!.Value, 1);
[Fact] public void Copy_frame_times_after_time() { Feed(h, 1, 0, 0.5, 9); var list = new List<double>(); Assert.Equal(4, h.CopyFrameTimes(1, 2.0, list)); Assert.All(list, ft => Assert.Equal(0.5, ft, 6)); }
[Fact] public void Percentile_nearest_rank() => Assert.Equal(1 / 0.1, FrameTimeHistory.OnePercentLowOf([.. Enumerable.Repeat(0.01, 99), 0.1])!.Value, 3);
```
- [ ] **Step 2:** FAIL. **Step 3:** cài đặt. **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat: frame-time history with 1% low`

---

### Task 3: Đọc cảm biến (LibreHardwareMonitor + RAM)

**Files:**
- Create: `src/FpsOverlay.Core/Sensors/SensorSnapshot.cs` (kiểu + `BusiestGpu`), `src/FpsOverlay/SensorService.cs`
- Modify: `src/FpsOverlay/FpsOverlay.csproj` (PackageReference `LibreHardwareMonitorLib` `0.9.6`), `src/FpsOverlay/NativeMethods.cs` (`GlobalMemoryStatusEx`)
- Test: `tests/FpsOverlay.Core.Tests/SensorSnapshotTests.cs`

**Interfaces:**
- Produces (Core):
  - `public readonly record struct SensorSnapshot(double? GpuLoad, double? GpuTemp, double? CpuLoad, double? CpuTemp, double? RamUsedGb)` với `static SensorSnapshot Empty`.
  - `public readonly record struct GpuReading(string Id, double? Load, double? Temp)`.
  - `static GpuReading? SensorMath.BusiestGpu(IReadOnlyList<GpuReading> gpus, string? previousId)` — load cao nhất; load bằng nhau (kể cả cùng null) thì giữ `previousId` nếu còn trong danh sách, không thì card đầu tiên; danh sách rỗng → null.
- Produces (app): `sealed class SensorService : IDisposable` — `SensorSnapshot Latest { get; }` (đọc không khoá, gán nguyên khối), `void Start()`, `void Stop()` (dừng vòng đọc, không giải phóng thư viện), `void Reinitialize()` (đóng/mở lại `Computer`, dùng sau khi cài PawnIO), `bool CpuTemperatureAvailable { get; }`. Vòng đọc trên một `Thread` nền (IsBackground), chu kỳ 1 giây; `Computer.Open()` chạy trong luồng đó (lần đầu 1–2 giây không chặn UI). Mọi ngoại lệ từ thư viện bị bắt, giá trị tương ứng thành null.
- Chọn cảm biến (tra lại tên thật bằng một lần chạy liệt kê sensor ở Step 3, ghi vào báo cáo task):
  - CPU load: `SensorType.Load`, tên `"CPU Total"`.
  - CPU temp: `SensorType.Temperature`, ưu tiên `"Core (Tctl/Tdie)"` (AMD), `"CPU Package"` (Intel), không có thì cảm biến nhiệt độ cao nhất của CPU.
  - GPU (HardwareType `GpuNvidia`/`GpuAmd`/`GpuIntel`): load `"GPU Core"` (Load), temp `"GPU Core"` (Temperature).
  - RAM: `GlobalMemoryStatusEx` → `(ullTotalPhys − ullAvailPhys) / 1024³`.

- [ ] **Step 1: Test** `BusiestGpu`: chọn load cao nhất; hai card cùng null → giữ `previousId`; previous không còn → card đầu; rỗng → null; card load null và card load 0 → card load 0 (null coi như thấp nhất).
- [ ] **Step 2:** FAIL. **Step 3:** cài đặt; chạy thử một lần (cần quyền Admin — nếu agent không có quyền Admin thì chạy không quyền, ghi lại cảm biến nào đọc được và tên chúng, phần còn lại để kiểm tay cuối kế hoạch). **Step 4:** PASS, build 0 cảnh báo.
- [ ] **Step 5: Commit** `feat: sensor service (GPU/CPU load and temperature, RAM)`

---

### Task 4: Overlay nhiều mục + công tắc trong cài đặt

**Files:**
- Create: `src/FpsOverlay.Core/StatFormat.cs`
- Modify: `src/FpsOverlay.Core/Settings/OverlaySettings.cs`, `SettingsStore.cs`, `Localization/Loc.cs`, `src/FpsOverlay/OverlayWindow.xaml(.cs)`, `OverlayTheme.cs`, `SettingsWindow.xaml(.cs)`, `App.xaml.cs`
- Test: `tests/FpsOverlay.Core.Tests/StatFormatTests.cs`, `OverlaySettingsTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `FrameTimeHistory.OnePercentLow(IReadOnlyList<int>)` (Task 2), `SensorService.Latest` (Task 3), `ForegroundProcess.GetCandidates()`.
- Produces:
  - `OverlaySettings` thêm (mặc định `true`): `ShowOnePercentLow`, `ShowGpuLoad`, `ShowGpuTemp`, `ShowCpuLoad`, `ShowCpuTemp`, `ShowRam`, `LoggingEnabled`; thêm `PawnIoOffered` (mặc định `false`, dùng ở Task 9). Load/Save đọc từng trường với giá trị mặc định khi thiếu (file cũ vẫn đọc được). `ResetToDefaults` giữ nguyên `LoggingEnabled` và `PawnIoOffered`, đưa các `Show*` về mặc định.
  - `static class StatFormat`: `Percent(double?)` → `"67%"`/`"--"`, `Temperature(double?)` → `"71°"`/`"--"`, `Gigabytes(double?)` → `"9.2G"`/`"--"` (một chữ số, văn hoá bất biến); làm tròn nửa lên; dùng chuỗi dựng sẵn cho 0–100% và 0–150° (cùng chuỗi cho cùng giá trị).
  - `static class HeatLevels`: `const double GpuHotCelsius = 85, CpuHotCelsius = 90`, `static bool IsHot(double? temp, double limit)` (so trên giá trị đã làm tròn như hiển thị).
  - `OverlayWindow.SetStats(double? onePercentLow, SensorSnapshot sensors, OverlaySettings settings)` — gọi mỗi giây (và khi settings đổi); chỉ gán `Text`/`Foreground`/`Visibility` khi khác giá trị cũ. `SetFps` giữ nguyên chữ ký.
- Bố cục: thêm các `TextBlock` mục `1%`, `GPU` (hai giá trị), `CPU` (hai giá trị), `RAM` vào `Items`, ngăn cách ` · ` dùng màu nhãn. Ẩn mục bằng `Visibility.Collapsed`; tắt cả hai số GPU (hoặc CPU) thì ẩn cả nhãn và dấu ngăn của nó. Font/hiệu ứng theo phong cách như mục FPS (giá trị Cyberpunk: JetBrains Mono, không phát sáng cho số phụ — chỉ FPS phát sáng như hiện nay). 1% low dùng `FpsLevels.Classify` giống FPS khi `ColorByFps` bật.
- Cài đặt: mục mới `// THÔNG SỐ` / `// STATS` gồm 6 công tắc (nhãn: "1% low", "GPU %", "Nhiệt độ GPU"/"GPU temp", "CPU %", "Nhiệt độ CPU"/"CPU temp", "RAM"). Trong lúc xem trước (cửa sổ cài đặt active), overlay hiện số mẫu cho các mục mới: 1% low = 0,8 × FPS mẫu, GPU 67% 71°, CPU 34% 65°, RAM 9.2G.
- Nếu `SensorService.CpuTemperatureAvailable` là false, dưới công tắc "Nhiệt độ CPU" hiện dòng ghi chú (chữ nhỏ) `"Cần cài PawnIO để đọc nhiệt độ CPU."` / `"PawnIO is required to read CPU temperature."` (nút cài thêm ở Task 9).

- [ ] **Step 1: Test** — `StatFormat` (null → `"--"`, `66.5` → `"67%"`, `70.6` → `"71°"`, `9.24` → `"9.2G"`, `Assert.Same` cho hai giá trị cùng làm tròn); `HeatLevels.IsHot(84.6, 85)` true, `IsHot(84.4, 85)` false, `IsHot(null, 85)` false; settings mặc định các `Show*` = true, `LoggingEnabled` = true, `PawnIoOffered` = false; file JSON cũ không có trường mới → giá trị mặc định; round-trip có các trường mới; `ResetToDefaults` giữ `LoggingEnabled = false`.
- [ ] **Step 2:** FAIL. **Step 3:** cài đặt. **Step 4:** PASS, build 0 cảnh báo.
- [ ] **Step 5: Commit** `feat: overlay shows 1% low, GPU, CPU and RAM with per-stat toggles`

---

### Task 5: Nhận biết buổi chơi + ghi log

**Files:**
- Create: `src/FpsOverlay.Core/Logging/GameSessionTracker.cs`, `src/FpsOverlay.Core/Logging/SessionLog.cs`, `src/FpsOverlay.Core/Logging/SessionSummary.cs`, `src/FpsOverlay/SessionLogger.cs`
- Modify: `src/FpsOverlay/App.xaml.cs`, `SettingsWindow.xaml(.cs)`, `Localization/Loc.cs`, `NativeMethods.cs` (`MonitorFromWindow`, `GetMonitorInfo`, `GetWindowRect` nếu chưa có)
- Test: `tests/FpsOverlay.Core.Tests/GameSessionTrackerTests.cs`, `SessionLogTests.cs`, `SessionSummaryTests.cs`

**Interfaces:**
- Produces (Core):
  - `public readonly record struct SessionInput(double Now, int? ProcessId, string? ProcessName, bool CoversMonitor, bool HasFps, bool SessionProcessAlive)` — gọi mỗi giây; `SessionProcessAlive` là trạng thái của tiến trình buổi hiện tại (true nếu chưa có buổi).
  - `public sealed record GameSession(int ProcessId, string ProcessName, double StartedAt)`.
  - `sealed class GameSessionTracker` với `const double StartAfterSeconds = 10, EndAfterSeconds = 60`; `GameSession? Current`; `IReadOnlyList<SessionChange> Tick(SessionInput input)` trả về danh sách thay đổi (rỗng nếu không có; có thể có hai: `Ended` rồi `Started`). `public readonly record struct SessionChange(SessionChangeKind Kind, GameSession Session)`; `enum SessionChangeKind { Started, Ended }`.
  - `static class SessionLog`: `const string Header = "Time,Fps,OnePercentLow,GpuLoad,GpuTemp,CpuLoad,CpuTemp,RamUsedGb"`; `static string FormatRow(DateTimeOffset time, double? fps, double? onePercentLow, SensorSnapshot s)` (thời gian dạng `yyyy-MM-ddTHH:mm:sszzz`; FPS/1% một chữ số thập phân; % và ° số nguyên; RAM hai chữ số); `static string FileStem(string processName, DateTime start)` → `<tên đã lọc>_<yyyy-MM-dd_HH-mm>` (ký tự trong `Path.GetInvalidFileNameChars()` và khoảng trắng thành `_`, rỗng → `Game`); `static string UniquePath(string directory, string stem, string extension, Func<string,bool> exists)` thêm `_2`, `_3`... khi trùng.
  - `sealed class SessionSummary`: `void AddSecond(double? fps, SensorSnapshot s)`, `void AddFrameTimes(IReadOnlyList<double> frameTimes)`, `string Format(AppLanguage language, TimeSpan duration)` — dòng: thời lượng, FPS trung bình (tổng khung / tổng thời gian khung), 1% low cả buổi (`FrameTimeHistory.OnePercentLowOf`), FPS thấp nhất/cao nhất theo giây, nhiệt độ GPU/CPU cao nhất; thiếu dữ liệu thì `--`.
- Produces (app): `sealed class SessionLogger : IDisposable` — mỗi giây App gọi `Tick(...)`; logger dùng `GameSessionTracker`, lấy frametime của game qua `FrameTimeHistory.CopyFrameTimes` (từ `LatestTimeSeconds` lần trước), tính `Fps` của giây đó = số khung / 1 giây, ghi dòng vào hàng đợi; một luồng nền ghi file theo lô mỗi 10 giây (`FileShare.Read`, thư mục tạo khi cần). Buổi kết thúc hoặc `Dispose` → ghi nốt + file tóm tắt. Lỗi IO chỉ ghi `Trace`, không sập.
- `CoversMonitor`: `GetWindowRect` của cửa sổ phía trước ⊇ vùng `rcMonitor` của màn hình chứa nó (`MonitorFromWindow` + `GetMonitorInfo`); bỏ qua cửa sổ desktop/shell (`GetShellWindow`, lớp `Progman`/`WorkerW`).
- Cài đặt: mục `// HỆ THỐNG` thêm công tắc "Ghi log FPS"/"Log FPS" và nút "MỞ THƯ MỤC LOG"/"OPEN LOG FOLDER" (mở Explorer; tạo thư mục nếu chưa có).
- **Khi ẩn overlay (hoàn thiện phần 2 của spec):** `LoggingEnabled` bật → PresentMon và `SensorService` chạy tiếp; tắt → `PresentMonRunner` dừng (Dispose) và `SensorService.Stop()`; hiện lại → tạo `PresentMonRunner` mới và `Start()`. Đổi `LoggingEnabled` lúc đang ẩn cũng áp dụng ngay.

- [ ] **Step 1: Test** (tracker dùng `Now` giả):
  - `Starts_after_ten_seconds_of_fullscreen_fps` (giây 0–10 chưa, giây 11 → `Started`).
  - `Window_not_covering_monitor_never_starts`.
  - `Losing_fps_before_ten_seconds_resets_the_count`.
  - `Alt_tab_under_sixty_seconds_keeps_session`, `Away_over_sixty_seconds_ends_session`.
  - `Process_exit_ends_session_immediately`.
  - `Other_game_qualifying_during_grace_replaces_session` (trả về `Ended` rồi `Started`).
  - `SessionLog`: header, `FormatRow` với giá trị null → ô trống, dấu chấm thập phân khi văn hoá hiện tại là `vi-VN`; `File_stem_is_sanitized` (`"Lies of P:"` → `Lies_of_P_`…); `Unique_path_adds_suffix`.
  - `SessionSummary`: trung bình, 1% low, min/max, nhiệt độ cao nhất, chuỗi tiếng Việt và tiếng Anh có đủ dòng; không có dữ liệu → `--`.
- [ ] **Step 2:** FAIL. **Step 3:** cài đặt. **Step 4:** PASS, build 0 cảnh báo.
- [ ] **Step 5: Commit** `feat: automatic per-session FPS logging with summary`

---

### Task 6: Overlay theo màn hình đang có game

**Files:**
- Create: `src/FpsOverlay.Core/OverlayPlacement.cs`
- Modify: `src/FpsOverlay/OverlayWindow.xaml.cs`, `NativeMethods.cs` (`GetDpiForMonitor` từ `Shcore.dll` nếu cần)
- Test: `tests/FpsOverlay.Core.Tests/OverlayPlacementTests.cs`

**Interfaces:**
- Produces: `public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)`; `static (int X, int Y) OverlayPlacement.Compute(PixelRect monitor, int windowWidth, int windowHeight, OverlayCorner corner, int marginPx)` — kẹp để cửa sổ không ra ngoài màn hình.
- `OverlayWindow.UpdatePosition()`: lấy màn hình của cửa sổ phía trước (`MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTOPRIMARY)`; cửa sổ phía trước là chính app hoặc desktop/shell → màn hình chính), margin = `round(8 × dpi/96)` của màn hình đó, kích thước cửa sổ thật bằng `GetWindowRect` của overlay, đặt bằng `SetWindowPos(... SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER)` chỉ khi toạ độ khác lần trước. Bỏ `Left/Top` theo `SystemParameters`. Vẫn gọi từ `SizeChanged`, `DpiChanged`, `Apply`, `KeepOnTop` (mỗi giây).
- Lúc xem trước (cửa sổ cài đặt active): overlay ở màn hình chứa cửa sổ cài đặt.

- [ ] **Step 1: Test** 4 góc trên màn hình chính `(0,0,1920,1080)` với cửa sổ 200×40, margin 8; `Placement_on_secondary_monitor_left_of_primary` (`(-2560,0,0,1440)`, góc trên phải → `X = -8 - 200`); cửa sổ lớn hơn màn hình → kẹp về mép trái/trên.
- [ ] **Step 2:** FAIL. **Step 3:** cài đặt. **Step 4:** PASS, build 0 cảnh báo.
- [ ] **Step 5: Commit** `feat: overlay follows the monitor of the foreground window`

---

### Task 7: Một file tự chứa (single-file, nhúng PresentMon và giấy phép)

**Files:**
- Create: `src/FpsOverlay/EmbeddedFiles.cs`, `src/FpsOverlay/Assets/Licenses/` (`PresentMon-LICENSE.txt` MIT, `LibreHardwareMonitor-LICENSE.txt` MPL-2.0 — chép đúng nguyên văn từ gói/thư mục nguồn, ghi nguồn trong báo cáo)
- Modify: `src/FpsOverlay/FpsOverlay.csproj`, `src/FpsOverlay/PresentMonRunner.cs`
- Test: kiểm bằng lệnh (không có test Core)

**Interfaces:**
- Produces: `static class EmbeddedFiles` — `static void ExtractAll(string directory)` bung `PresentMon.exe` và mọi file giấy phép (OFL font + Assets/Licenses) vào `directory`, chỉ ghi khi file thiếu hoặc mã băm SHA-256 khác bản nhúng; `static string ResourceHash(string name)`.
- `PresentMonRunner` nhận đường dẫn thư mục chứa PresentMon qua constructor (`PresentMonRunner(string toolDirectory)`); không còn dùng `AppContext.BaseDirectory` (với single-file, đó có thể là thư mục bung tạm).
- csproj: `<Version>1.0.0</Version>`, `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`, `<SelfContained>true</SelfContained>`, `<PublishSingleFile>true</PublishSingleFile>`, `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>`, `<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>`. PresentMon.exe và file giấy phép thành `EmbeddedResource` (bỏ các mục `CopyToOutputDirectory` cũ). Lệnh đóng gói mới: `dotnet publish src/FpsOverlay -c Release -o publish`.
- Tạm thời (trước Task 8) app bung vào `AppContext.BaseDirectory`-tương-đương là thư mục chứa exe (`Path.GetDirectoryName(Environment.ProcessPath)`); Task 8 đổi sang thư mục cài.

- [ ] **Step 1:** Sửa csproj, thêm `EmbeddedFiles`, sửa `PresentMonRunner`.
- [ ] **Step 2:** `dotnet test` PASS; `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 3:** `dotnet publish src/FpsOverlay -c Release -o publish` → thư mục `publish` chỉ có `FpsOverlay.exe` (và có thể `.pdb`), kích thước ghi vào báo cáo. Chạy `publish\FpsOverlay.exe --no-install` không cần ở bước này (cần Admin): chỉ kiểm bằng cách giải nén tài nguyên qua một test nhỏ trong scratch nếu muốn; không commit harness.
- [ ] **Step 4: Commit** `build: single-file self-contained exe with embedded PresentMon and licenses`

---

### Task 8: Tự cài, cập nhật, gỡ, `--no-install`, khoá khởi động cùng Windows ngoài thư mục cài

**Files:**
- Create: `src/FpsOverlay.Core/Install/InstallDecision.cs`, `src/FpsOverlay/Installer.cs`, `src/FpsOverlay/InstallWindow.xaml(.cs)`
- Modify: `src/FpsOverlay/App.xaml.cs`, `AutostartService.cs`, `SettingsWindow.xaml.cs`, `TrayIcon.cs`, `Localization/Loc.cs`
- Test: `tests/FpsOverlay.Core.Tests/InstallDecisionTests.cs`

**Interfaces:**
- Produces (Core): `enum InstallAction { Install, Update, RunInstalled }`; `static InstallAction InstallDecision.Decide(Version running, string runningHash, Version? installed, string? installedHash)` — không có bản cài → `Install`; running > installed → `Update`; bằng nhau và hash khác (không phân biệt hoa thường) → `Update`; còn lại → `RunInstalled`.
- Produces (app): `static class Installer` — `string InstallDirectory`, `string InstalledExePath`, `bool IsRunningInstalled` (so đường dẫn không phân biệt hoa thường), `void Install()` (tạo thư mục, chép exe, `EmbeddedFiles.ExtractAll`, lối tắt Start Menu chung qua COM `IShellLinkW`/`IPersistFile`, ghi khoá Uninstall: `DisplayName "FPS Overlay"`, `DisplayVersion`, `Publisher "FPS Overlay"`, `DisplayIcon`, `InstallLocation`, `UninstallString "\"...\FpsOverlay.exe\" --uninstall"`, `NoModify=1`, `NoRepair=1`), `void Uninstall()` (xoá tác vụ, lối tắt, khoá đăng ký, file trong thư mục cài; exe đang chạy tự xoá bằng `cmd /c ping -n 3 127.0.0.1 >nul & rmdir /s /q "<dir>"` khởi chạy ẩn ngay trước khi thoát), `void LaunchInstalled()`.
- Luồng khởi động trong `App.OnStartup` (trước mutex một bản chạy, vì bản cài có thể đang chạy):
  - `--uninstall` → hỏi xác nhận (MessageBox theo ngôn ngữ đã lưu) → `Uninstall()` → thoát.
  - `--no-install` hoặc `IsRunningInstalled` → chạy bình thường (bung tài nguyên vào thư mục của exe nếu là `--no-install`, vào thư mục cài nếu là bản cài).
  - Còn lại → `Decide(...)` → `RunInstalled`: `LaunchInstalled()` rồi thoát, không hỏi. `Install`/`Update`: hiện `InstallWindow` (kiểu Cyberpunk, dùng `Themes/Cyberpunk.xaml`, chọn được ngôn ngữ như cài đặt đã lưu), nút "CÀI ĐẶT"/"INSTALL" hoặc "CẬP NHẬT"/"UPDATE" và "HUỶ"/"CANCEL". Bản cài đang chạy (mutex đang bị giữ) → báo "Hãy thoát FPS Overlay đang chạy (biểu tượng ở khay) rồi thử lại." và không cài. Cài xong: nếu tác vụ khởi động cùng Windows đang có thì tạo lại trỏ vào `InstalledExePath`; `LaunchInstalled()`; thoát.
  - Huỷ → thoát.
- `AutostartService`: thêm `bool CanManage` (= `Installer.IsRunningInstalled`); khi false: `RefreshAsync` chỉ đọc (không tạo lại tác vụ), `TrySet` trả false với lỗi rỗng; ô tick ở khay và công tắc trong cài đặt bị mờ (`Enabled = false` / `IsEnabled = false`) kèm tooltip "Chỉ bản đã cài mới bật được."/"Only the installed copy can enable this.". Thêm `void Retarget(string exePath)` dùng cho bước cài.
- Mutex một bản chạy giữ nguyên tên; kiểm tra "bản cài đang chạy" dùng chính mutex đó (`Mutex.TryOpenExisting`).

- [ ] **Step 1: Test** `InstallDecision`: chưa cài → Install; 1.1.0 vs 1.0.0 → Update; 1.0.0 vs 1.1.0 → RunInstalled; bằng nhau, hash khác → Update; bằng nhau, hash giống (khác hoa thường) → RunInstalled.
- [ ] **Step 2:** FAIL. **Step 3:** cài đặt. **Step 4:** PASS, build 0 cảnh báo.
- [ ] **Step 5:** Kiểm tay để lại cho người dùng (cần Admin, đụng Program Files): ghi vào báo cáo danh sách bước kiểm.
- [ ] **Step 6: Commit** `feat: self-install to Program Files with update, uninstall and Start menu shortcut`

---

### Task 9: PawnIO — hỏi trong hộp thoại cài, tải, kiểm chữ ký, cài ngầm

**Files:**
- Create: `src/FpsOverlay/PawnIoSetup.cs`
- Modify: `src/FpsOverlay/InstallWindow.xaml(.cs)`, `SettingsWindow.xaml(.cs)`, `App.xaml.cs`, `Localization/Loc.cs`, `NativeMethods.cs` (`WinVerifyTrust` nếu dùng trực tiếp)

**Interfaces:**
- Consumes: `SensorService.Reinitialize()`, `SensorService.CpuTemperatureAvailable` (Task 3), `OverlaySettings.PawnIoOffered` (Task 4), `Installer.InstallDirectory` (Task 8).
- Produces: `static class PawnIoSetup` — `const string DownloadUrl = "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe"`; `bool IsInstalled()` (khoá Uninstall `PawnIO` trong HKLM, hoặc dịch vụ `PawnIO` tồn tại — xác minh cách nào đúng ở Step 1); `Task<PawnIoResult> InstallAsync(CancellationToken)` — tải bằng `HttpClient` vào `<InstallDirectory>\Temp\` (thư mục chỉ Admin ghi được; với `--no-install` dùng `%ProgramData%\FpsOverlay\Temp` tạo với ACL chỉ Administrators/SYSTEM), kiểm chữ ký Authenticode hợp lệ (`WinVerifyTrust`) **và** tên người ký khớp hằng `ExpectedSigner`, chạy `-install -silent` chờ xong, xoá file tải; `enum PawnIoResult { Installed, InstalledRebootRequired, DownloadFailed, SignatureInvalid, InstallFailed }` (mã thoát 3010 → `InstalledRebootRequired`).
- **`ExpectedSigner`:** controller hỏi người dùng cho phép tải bộ cài PawnIO một lần (tên file, nguồn, kích thước) để đọc tên người ký thật bằng `Get-AuthenticodeSignature`, rồi ghi hằng số đó vào brief của task. Không đoán.
- InstallWindow: nếu `!PawnIoSetup.IsInstalled()` và `!settings.PawnIoOffered` → hiện ô tick "Cài PawnIO để đọc nhiệt độ CPU (tải từ GitHub chính thức)" / "Install PawnIO to read CPU temperature (downloaded from its official GitHub)", mặc định bỏ trống. Không tick → đặt `PawnIoOffered = true`. Tick → cài PawnIO sau khi chép app xong, hiện tiến trình đơn giản trong hộp thoại, báo kết quả (cần khởi động lại → nói rõ).
- Cài đặt: dưới công tắc "Nhiệt độ CPU", khi chưa có PawnIO, có nút "CÀI PAWNIO"/"INSTALL PAWNIO" (chỉ bật ở bản đã cài hoặc `--no-install`), chạy `InstallAsync` không chặn UI, xong thì `SensorService.Reinitialize()` và cập nhật dòng ghi chú.
- Không bao giờ hỏi/cài lúc app tự khởi động cùng Windows.

- [ ] **Step 1:** Xác minh cách phát hiện PawnIO đã cài và người ký (theo mục trên). **Step 2:** cài đặt. **Step 3:** `dotnet test` PASS, build 0 cảnh báo. **Step 4:** kiểm tay để lại cho người dùng.
- [ ] **Step 5: Commit** `feat: offer PawnIO install with signature check for CPU temperature`

---

### Task 10: Đóng gói và danh sách kiểm tay

**Files:** không đổi code (trừ sửa lỗi phát hiện ra).

- [ ] **Step 1:** `dotnet test` PASS; `dotnet publish src/FpsOverlay -c Release -o publish` → một file `FpsOverlay.exe`.
- [ ] **Step 2:** Viết danh sách kiểm tay cho người dùng (trong báo cáo cuối, không commit): cài lần đầu + PawnIO; overlay đủ mục, công tắc từng mục, màu nóng; chơi game > 10 giây rồi thoát → có CSV và tóm tắt; alt-tab < 1 phút cùng buổi; ẩn FPS khi log bật/tắt (Task Manager: PresentMon còn/hết); cảnh báo PresentMon (khó tạo ra có chủ đích — chỉ kiểm khi gặp thật); cắm màn hình ngoài; khởi động cùng Windows từ bản cài; gỡ cài đặt giữ cài đặt và log.
