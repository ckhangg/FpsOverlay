# Đợt 5: thông số GPU, cảnh báo nhiệt, phong cách mới, bố cục dọc và kéo thả — Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dọn khung overlay thành bảng mô tả phong cách và danh sách dòng, rồi thêm 4 thông số GPU, cảnh báo nhiệt nhấp nháy/bíp, hai phong cách Neon tím và Terminal xanh, bố cục dọc, chế độ kéo thả vị trí.

**Architecture:**
- Phần thuần nằm trong `FpsOverlay.Core` và có test: màu và ký tự của phong cách, thứ tự/hiện ẩn/chữ của từng dòng, chọn cảm biến GPU, bộ theo dõi cảnh báo, phép tính vị trí, cài đặt mới, cột log.
- WPF (`FpsOverlay`) chỉ dựng giao diện từ các phần đó: font, hiệu ứng, viền, hai bố cục, nhấp nháy, chế độ di chuyển.

**Tech Stack:** C# / .NET 10, WPF + WinForms NotifyIcon, xUnit, LibreHardwareMonitorLib 0.9.6.

**Spec:** `docs/superpowers/specs/2026-11-03-gpu-alerts-themes-layout-design.md`

## Global Constraints
- Thứ tự dòng cố định: FPS, 1%, GPU (tải, nhiệt), VRAM, CLK, PWR, FAN, CPU (tải, nhiệt), RAM.
- Nhãn và đơn vị giống nhau ở hai ngôn ngữ: `FPS`, `1%`, `GPU`, `VRAM`, `CLK`, `PWR`, `FAN`, `CPU`, `RAM`, `MHz`, `W`, `RPM`.
- Mọi chuỗi giao diện mới có cả tiếng Việt và tiếng Anh trong `Loc`. Test `Every_key_has_both_languages` phải qua.
- Đường cập nhật FPS mỗi khung hình (`OnRendering` → `SetFps`) không cấp phát. Số cảm biến cập nhật mỗi giây, chỉ gán khi chữ/màu đổi.
- Bố cục ngang ở Cổ điển và Cyberpunk phải trông **như trước đợt này**.
- Ngoài chế độ di chuyển, overlay vẫn bấm xuyên, không chiếm focus, không chớp `--`.
- Mặc định:
  - `Layout` = Horizontal; `OffsetX` = `OffsetY` = 8 (0–4000 DIP);
  - 4 công tắc GPU mới tắt;
  - ngưỡng GPU 85 / CPU 90 (50–110 °C);
  - `HotAlertSeconds` 10 (0–60); nháy bật; tiếng tắt.
- Đợt nóng kết thúc khi nhiệt (đã làm tròn) **< ngưỡng − 3**, khi nhiệt null, khi overlay ẩn, khi đang xem trước, hoặc khi tắt hiện số nhiệt đó.
- Nháy khoảng 2 lần/giây: hiện = màu nóng, mờ = cùng màu ở 25% alpha. Kích thước overlay không đổi khi nháy.
- Tiếng: `SystemSounds.Exclamation`, một lần khi đợt nóng bắt đầu.
- Log: thêm vào cuối header `,GpuMemUsedGb,GpuClockMhz,GpuPowerW,GpuFanRpm` (F2, F0, F0, F0).
- `<Version>` trong `src/FpsOverlay/FpsOverlay.csproj` lên `1.1.0`.
- `dotnet build src/FpsOverlay` 0 cảnh báo; `dotnet test` qua hết.

## Review Focus
1. **File `settings.json` cũ (bản 1.0.0)** thiếu mọi trường mới, hoặc có `Style` là giá trị lạ: phải nạp được với mặc định, không mất màu Cổ điển/Cyberpunk đã chỉnh. Test trong Task 1 (`Old_file_without_new_fields_loads_defaults`).
2. **Nhiệt dao động sát ngưỡng** (85, 84, 85, 83, 86...): không được bíp lại mỗi lần chạm ngưỡng. Test trong Task 5 (`Hovering_near_threshold_does_not_restart_episode`).
3. **Overlay lớn hơn màn hình, hoặc offset đã lưu lớn hơn màn hình nhỏ** (kéo trên màn 4K rồi chơi ở màn 1080p): overlay vẫn nằm trọn trên màn hình. Test trong Task 9 (`Offset_larger_than_monitor_is_clamped_inside`).
4. **Card rời đang ngủ:** CLK/PWR = 0 thì hiện `--`, còn FAN = 0 thì hiện `0RPM`; không có cảm biến quạt thì `--`. Test trong Task 3 (`Zero_clock_and_power_are_null_but_zero_fan_is_kept`).
5. **Kết thúc chế độ di chuyển bằng cách bất kỳ** (ẩn overlay, mở cài đặt, Esc, Enter, bấm đúp, bấm lại mục khay): cờ bấm xuyên và không chiếm focus luôn được trả lại. Đây là phần WPF; nằm trong danh sách kiểm tay của Task 12 (mục 8–9) và bước kiểm bằng mắt của Task 11.

---

### Task 1: Bảng phong cách trong Core và bộ màu theo phong cách

Phần "0" của spec, nửa Core. Chưa thêm phong cách mới, chỉ đưa Classic/Cyberpunk vào bảng.

**Files:**
- Create: `src/FpsOverlay.Core/Settings/StyleSpec.cs`
- Modify: `src/FpsOverlay.Core/Settings/StyleColors.cs`, `src/FpsOverlay.Core/Settings/OverlaySettings.cs`
- Test: `tests/FpsOverlay.Core.Tests/StyleSpecTests.cs`, `tests/FpsOverlay.Core.Tests/OverlaySettingsTests.cs`, `tests/FpsOverlay.Core.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record StyleSpec(StyleColors DefaultColors, string Good, string Medium, string Bad, string Background, string Separator, string Prompt, IReadOnlyList<string> Swatches)`. Mọi màu là `#RRGGBB`. Màu "nóng" = `Bad`.
  - `public static class StyleSpecs { public static StyleSpec For(OverlayStyle style); }`.
  - `StyleColors.DefaultFor(style)` trả `StyleSpecs.For(style).DefaultColors`.
  - `OverlaySettings.ColorsFor` / `WithColors` / `ResetToDefaults` / `Normalize` viết lại để chạy cho **mọi** giá trị `OverlayStyle` (switch hoặc vòng lặp `Enum.GetValues`), nên Task 7 chỉ cần thêm hai thuộc tính màu.

- [ ] **Step 1: Test** (`StyleSpecTests`), các giá trị lấy đúng từ code hiện tại:
```csharp
[Fact] public void Classic_matches_previous_constants()
{
    var s = StyleSpecs.For(OverlayStyle.Classic);
    Assert.Equal(new StyleColors("#FF8C1A", "#FFFFFF", "#39FF14"), s.DefaultColors);
    Assert.Equal(("#39FF14", "#FFD400", "#FF3B30"), (s.Good, s.Medium, s.Bad));
    Assert.Equal("#000000", s.Background);
    Assert.Equal((" · ", ""), (s.Separator, s.Prompt));
    Assert.Equal(["#FF8C1A", "#FFFFFF", "#39FF14", "#FFD400", "#FF3B30", "#00BFFF", "#FF66CC", "#A0A0A0", "#00FF99", "#FFAA00"], s.Swatches);
}
[Fact] public void Cyberpunk_matches_previous_constants()
{
    var s = StyleSpecs.For(OverlayStyle.Cyberpunk);
    Assert.Equal(new StyleColors("#00F0FF", "#FFFFFF", "#FCEE0A"), s.DefaultColors);
    Assert.Equal(("#00F0FF", "#FCEE0A", "#FF003C"), (s.Good, s.Medium, s.Bad));
    Assert.Equal("#0A0A0F", s.Background);
    Assert.Equal(10, s.Swatches.Count);
}
[Fact] public void Every_style_has_a_spec_with_ten_valid_swatches()   // dùng Enum.GetValues; HexColor.TryNormalize cho mọi màu
```
  - Thêm vào `SettingsStoreTests` test `Old_file_without_new_fields_loads_defaults`: file chỉ có `{"Style":"Classic","ClassicColors":{"Label":"#123456","Value":"#FFFFFF","Fps":"#39FF14"}}` → `Style` Classic, `ClassicColors.Label` `#123456`, các trường khác bằng `Defaults`.
  - Thêm test `Unknown_style_falls_back_to_default`: `{"Style":"Rainbow"}` → Cyberpunk.
- [ ] **Step 2:** `dotnet test` → FAIL (chưa có `StyleSpecs`).
- [ ] **Step 3:** Tạo `StyleSpec`/`StyleSpecs`. Chuyển `StyleColors.DefaultFor` sang bảng. Viết lại 4 hàm màu của `OverlaySettings` theo kiểu "mọi phong cách". `ResetToDefaults` giữ bộ màu của mọi phong cách khác phong cách đang chọn (test `ResetToDefaults_*` hiện có vẫn qua).
- [ ] **Step 4:** `dotnet test` → PASS (cũ + mới).
- [ ] **Step 5:** Commit `refactor(core): style table for colors, separators and swatches`.

### Task 2: Danh sách dòng trong Core

**Files:**
- Create: `src/FpsOverlay.Core/StatRows.cs`
- Modify: `src/FpsOverlay.Core/Settings/OverlaySettings.cs` (thêm `OverlayLayout`)
- Test: `tests/FpsOverlay.Core.Tests/StatRowsTests.cs`

**Interfaces:**
- Produces:
  - `public enum OverlayLayout { Horizontal, Vertical }` (trong `OverlaySettings.cs`, cạnh `OverlayCorner`). Thuộc tính `Layout` thêm ở Task 9; Task 2 nhận layout qua tham số.
  - `public enum StatRow { OnePercentLow, Gpu, GpuMemory, GpuClock, GpuPower, GpuFan, Cpu, Ram }`. Khai báo đủ 8 giá trị ngay; ba dòng GPU mới được bật ở Task 4.
  - `public static class StatRows`:
    - `IReadOnlyList<StatRow> Order` (đúng thứ tự trên);
    - `string Label(StatRow row)`;
    - `bool IsVisible(StatRow row, OverlaySettings s)`: Gpu/Cpu hiện nếu bật tải **hoặc** nhiệt. Ở Task 2 các dòng GPU mới trả false; Task 4 nối công tắc;
    - `(string First, string Second) Values(StatRow row, double? onePercentLow, SensorSnapshot s, OverlaySettings settings, OverlayLayout layout)`;
    - `int TempCell(StatRow row, OverlaySettings s)`: 0 = không có nhiệt, 1 = ô First, 2 = ô Second. Overlay dùng để tô màu nóng và nháy đúng ô.
  - Quy tắc `Values`:
    - Gpu/Cpu: tải và nhiệt đều bật → (tải, nhiệt). Chỉ bật một → (số đó, `""`).
    - 1% → (`FpsCalculator.Format`, `""`). Ram → (`StatFormat.Gigabytes`, `""`).
    - Dòng có đơn vị (Task 3): ngang → (`"1590MHz"`, `""`); dọc → (`"1590"`, `"MHz"`).
    - Số không đọc được → `"--"`; ở dọc ô đơn vị vẫn giữ (`"--"`, `"MHz"`).

- [ ] **Step 1: Test**
```csharp
[Fact] public void Order_matches_spec() => Assert.Equal(
    [StatRow.OnePercentLow, StatRow.Gpu, StatRow.GpuMemory, StatRow.GpuClock, StatRow.GpuPower, StatRow.GpuFan, StatRow.Cpu, StatRow.Ram], StatRows.Order);
[Theory] [InlineData(true, true, true)] [InlineData(false, true, true)] [InlineData(true, false, true)] [InlineData(false, false, false)]
public void Gpu_row_visible_if_load_or_temp(bool load, bool temp, bool visible)
[Fact] public void Gpu_both_shown_gives_load_then_temp()   // ("67%", "71°"), TempCell == 2
[Fact] public void Gpu_temp_only_goes_in_first_cell()      // ("71°", ""), TempCell == 1
[Fact] public void Missing_values_show_dashes()            // SensorSnapshot.Empty → ("--", "--")
[Fact] public void Labels_are_fixed() // Label(Gpu)=="GPU", Label(GpuMemory)=="VRAM", Label(GpuClock)=="CLK", Label(GpuPower)=="PWR", Label(GpuFan)=="FAN", Label(OnePercentLow)=="1%"
```
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Cài `StatRows` dựa trên `StatFormat`/`FpsCalculator.Format` có sẵn (chuỗi dựng sẵn, không cấp phát cho số thường gặp).
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat(core): overlay row model shared by both layouts`.

### Task 3: Đọc 4 thông số GPU, định dạng và cột log

**Files:**
- Create: `src/FpsOverlay.Core/Sensors/GpuSensorPick.cs`
- Modify: `src/FpsOverlay.Core/Sensors/SensorSnapshot.cs`, `src/FpsOverlay.Core/StatFormat.cs`, `src/FpsOverlay.Core/Logging/SessionLog.cs`, `src/FpsOverlay.Core/StatRows.cs`, `src/FpsOverlay/SensorService.cs`
- Test: `tests/FpsOverlay.Core.Tests/GpuSensorPickTests.cs`, `StatFormatTests.cs`, `SessionLogTests.cs`, `StatRowsTests.cs`

**Interfaces:**
- Produces:
  - `SensorSnapshot(double? GpuLoad, double? GpuTemp, double? CpuLoad, double? CpuTemp, double? RamUsedGb, double? GpuMemoryUsedGb = null, double? GpuClockMhz = null, double? GpuPowerW = null, double? GpuFanRpm = null)`.
  - `GpuReading(string Id, double? Load, double? Temp, double? MemoryUsedGb = null, double? ClockMhz = null, double? PowerW = null, double? FanRpm = null)`.
  - `public readonly record struct SensorValue(string Type, string Name, float? Value)`. `Type` là tên `SensorType` của LibreHardwareMonitor dạng chuỗi (`"Load"`, `"Temperature"`, `"Clock"`, `"Power"`, `"Fan"`, `"SmallData"`), để Core không phụ thuộc thư viện.
  - `public static class GpuSensorPick { public static GpuReading Read(string id, IEnumerable<SensorValue> sensors); }`. Thứ tự thử theo bảng trong spec phần A:
    - Load "GPU Core";
    - Temperature "GPU Core" (> 0);
    - SmallData "GPU Memory Used" → "D3D Dedicated Memory Used", MB ÷ 1024;
    - Clock "GPU Core" (> 0);
    - Power "GPU Package" → "GPU Power" → "GPU Board Power" (> 0);
    - Fan: tên `"GPU Fan"` hoặc bắt đầu bằng `"GPU Fan"`, lấy cái đầu, 0 hợp lệ.
  - `StatFormat.Megahertz(double?, bool withUnit)`, `StatFormat.Watts(double?, bool withUnit)`, `StatFormat.Rpm(double?, bool withUnit)`: làm tròn nửa lên, văn hoá bất biến, null → `"--"`. Ví dụ `"1590MHz"` / `"1590"`, `"48W"`, `"2400RPM"`.
  - `StatRows.Values` cho GpuMemory/GpuClock/GpuPower/GpuFan theo quy tắc ở Task 2.

- [ ] **Step 1: Test**
```csharp
[Fact] public void Picks_nvidia_names()   // Clock "GPU Core" 1590, Power "GPU Package" 48, SmallData "GPU Memory Used" 3174.4, Fan "GPU Fan" 2400 → ClockMhz 1590, PowerW 48, MemoryUsedGb 3.1 (±0.01), FanRpm 2400
[Fact] public void Falls_back_to_d3d_memory_and_gpu_power()   // chỉ có "D3D Dedicated Memory Used" 2048 và Power "GPU Power" 30 → 2.0, 30
[Fact] public void Zero_clock_and_power_are_null_but_zero_fan_is_kept()   // Clock 0, Power 0, Fan 0 → null, null, 0
[Fact] public void Missing_fan_is_null()
[Fact] public void Fan_name_with_index_is_accepted()   // "GPU Fan 1" 1800 → 1800
[Fact] public void Load_and_temp_unchanged_rules()   // Temperature "GPU Core" 0 → Temp null (như PositiveOrNull hiện nay)
// StatFormat
[Fact] public void Units_with_and_without_suffix() // Megahertz(1589.6,true)=="1590MHz", Megahertz(1589.6,false)=="1590", Watts(47.5,true)=="48W", Rpm(null,true)=="--"
// SessionLog: sửa 3 test có sẵn theo header/cột mới
Assert.Equal("Time,Fps,OnePercentLow,GpuLoad,GpuTemp,CpuLoad,CpuTemp,RamUsedGb,GpuMemUsedGb,GpuClockMhz,GpuPowerW,GpuFanRpm", SessionLog.Header);
// Row_formats_values: snapshot thêm GpuMemoryUsedGb: 3.104, GpuClockMhz: 1589.6, GpuPowerW: 47.5, GpuFanRpm: 0 → đuôi ",3.10,1590,48,0"
// Row_with_nulls_has_empty_cells: "2026-11-03T20:15:07+00:00,,,,,,,,,,,"
// StatRows
[Fact] public void Clock_row_horizontal_and_vertical()   // ngang ("1590MHz",""), dọc ("1590","MHz"), null dọc ("--","MHz")
```
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Cài phần Core.
  - `SensorService.ReadAll` đổi sang: với mỗi card GPU, dựng danh sách `SensorValue` từ `hw.Sensors` (`s.SensorType.ToString()`, `s.Name`, `s.Value`), rồi gọi `GpuSensorPick.Read`.
  - `BusiestGpu` giữ nguyên; snapshot lấy đủ 4 trường mới từ card được chọn.
  - Xoá `PositiveOrNull` / `Pick` GPU đã chuyển sang Core (giữ phần CPU).
  - Thêm một lần `Trace.TraceInformation` liệt kê `Type/Name` mọi cảm biến GPU khi mở `Computer`, để kiểm tên trên máy thật.
- [ ] **Step 4:** `dotnet test` → PASS; `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 5:** Commit `feat(sensors): GPU memory, clock, power and fan readings plus log columns`.

### Task 4: Overlay WPF dựng từ bảng phong cách và danh sách dòng (bố cục ngang), cùng 4 công tắc GPU mới

Đây là phần "0" phía WPF, cộng phần hiển thị của A. Sau task này overlay trông như cũ, và bật được VRAM/CLK/PWR/FAN.

**Files:**
- Modify: `src/FpsOverlay/OverlayTheme.cs`, `src/FpsOverlay/OverlayWindow.xaml`, `src/FpsOverlay/OverlayWindow.xaml.cs`, `src/FpsOverlay.Core/Settings/OverlaySettings.cs`, `src/FpsOverlay.Core/Settings/SettingsStore.cs`, `src/FpsOverlay.Core/StatRows.cs`, `src/FpsOverlay.Core/Localization/Loc.cs`, `src/FpsOverlay/SettingsWindow.xaml(.cs)`, `src/FpsOverlay/App.xaml.cs`
- Test: `OverlaySettingsTests.cs`, `SettingsStoreTests.cs`, `StatRowsTests.cs`

**Interfaces:**
- Consumes: `StyleSpecs.For`, `StatRows.*` (Task 1–3).
- Produces:
  - `OverlaySettings.ShowGpuMemory`, `ShowGpuClock`, `ShowGpuPower`, `ShowGpuFan` (mặc định `false`, có trong `SettingsStore.Load`, `ResetToDefaults` đặt về false).
  - Trong `OverlayTheme.cs`: `sealed record OverlayVisuals(FontFamily LabelFont, FontFamily ValueFont, double FpsLabelSize, double FpsValueSize, double StatLabelSize, double StatValueSize, double FpsGapSize, double StatGapSize, CornerRadius CornerRadius, Thickness BorderThickness, Brush? BorderBrush, bool BorderGlow, double Chamfer, Thickness Padding, TextGlow Glow, bool Outline, bool Scanlines)`, với `enum TextGlow { None, FpsOnly, LabelsAndFps, All }` và `static OverlayVisuals OverlayTheme.Visuals(OverlayStyle)`.
  - Classic: Consolas/Consolas, 18/24/14/18, gap 11/9, bo 4, padding 12,4,12,4, Outline, không glow.
  - Cyberpunk: Chakra Petch/JetBrains Mono, 18/24/14/18, gap 20/16, viền trái 3 `#FCEE0A`, vát 9, padding 10,3,19,3, `FpsOnly`.
  - `OverlayTheme.LevelBrush`, `FromHex`: màu đọc từ `StyleSpecs`.
  - Overlay giữ một đối tượng chữ cho mỗi `StatRow` (dấu ngăn, nhãn, ô First, ô Second), tạo một lần lúc dựng. `SetStats` lặp `StatRows.Order` và chỉ gán khi đổi, như `StatItem.Set` hiện nay.
  - Tham số mới: `SetStats(double? onePercentLow, SensorSnapshot sensors, OverlaySettings settings)` giữ nguyên chữ ký.
  - Loc khoá mới:

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `StatGpuMemory` | "VRAM GPU" | "GPU VRAM" |
    | `StatGpuClock` | "Xung nhân GPU" | "GPU core clock" |
    | `StatGpuPower` | "Công suất GPU" | "GPU power" |
    | `StatGpuFan` | "Quạt GPU" | "GPU fan" |

- [ ] **Step 1: Test**
```csharp
[Fact] public void New_gpu_switches_default_off()   // Defaults.ShowGpuMemory/Clock/Power/Fan == false
[Fact] public void Store_round_trips_new_switches()  // Save rồi Load giữ true
[Fact] public void Gpu_extra_rows_follow_switches()  // IsVisible(GpuClock, Defaults with { ShowGpuClock = true }) == true
```
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Thêm 4 thuộc tính và đọc/ghi.
  - Nối `IsVisible` cho các dòng mới.
  - Viết lại `OverlayWindow`:
    - XAML chỉ còn khung `Chrome` + `Zoom` + một panel ngang;
    - các dòng sinh trong code từ `StatRows.Order`;
    - `Apply` lấy mọi thứ từ `OverlayTheme.Visuals` và `StyleSpecs`;
    - dấu ngăn lấy từ `StyleSpec.Separator`; dấu nhắc lấy từ `StyleSpec.Prompt` và đặt trước nhãn FPS;
    - glow theo màu hiện tại của chữ (như `_cyberGlow` hiện nay).
  - Thêm 4 công tắc vào "// THÔNG SỐ" ngay sau GPU °C, trong `OnStatChanged` và `Refresh`.
  - Số mẫu khi xem trước (`App.UpdateStats`): `GpuMemoryUsedGb: 3.1, GpuClockMhz: 1590, GpuPowerW: 48, GpuFanRpm: 2400`.
- [ ] **Step 4:** `dotnet test` → PASS; `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 5: Kiểm bằng mắt** (cần quyền Admin; nếu phiên không có quyền thì ghi lại để người dùng kiểm ở Task 12).
  - Chạy `FpsOverlay.exe --no-install` trước và sau task, mở cài đặt (xem trước), chụp overlay ở Cổ điển và Cyberpunk, Scale 100%. Hai ảnh phải giống nhau.
  - Bật 4 công tắc mới: thấy `· VRAM 3.1G · CLK 1590MHz · PWR 48W · FAN 2400RPM` giữa GPU và CPU.
- [ ] **Step 6:** Commit `refactor(overlay): build overlay from style table and row list; add GPU stat switches`.

### Task 5: Bộ theo dõi cảnh báo nhiệt và cài đặt cảnh báo (Core)

**Files:**
- Create: `src/FpsOverlay.Core/HeatAlarm.cs`
- Modify: `src/FpsOverlay.Core/StatFormat.cs` (xoá `GpuHotCelsius`/`CpuHotCelsius`, giữ `HeatLevels.IsHot`), `OverlaySettings.cs`, `SettingsStore.cs`
- Test: `tests/FpsOverlay.Core.Tests/HeatAlarmTests.cs`, `StatFormatTests.cs` (bỏ test `Limits`), `OverlaySettingsTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public enum HeatState { Normal, Hot, Alarming }`.
  - `public sealed class HeatAlarm`:
    - `public const int ReleaseMargin = 3;`
    - `public HeatState State { get; }`
    - `public bool Update(double? temp, int threshold, int holdSeconds, double nowSeconds)`: trả `true` đúng một lần khi đợt nóng bắt đầu;
    - `public void Reset()`.
  - Quy tắc:
    - "nóng" = `HeatLevels.IsHot(temp, threshold)`;
    - null → Reset;
    - đang Alarming thì giữ cho tới khi `Math.Round(temp) < threshold − 3`;
    - không Alarming: nóng thì nhớ mốc bắt đầu, đủ `holdSeconds` (≥) thì Alarming và trả true; không nóng thì xoá mốc, Normal.
  - `OverlaySettings`:
    - `GpuHotThreshold` (85), `CpuHotThreshold` (90), `HotAlertSeconds` (10), `HotAlertBlink` (true), `HotAlertSound` (false);
    - hằng `MinHotThreshold = 50`, `MaxHotThreshold = 110`, `MaxHotAlertSeconds = 60`;
    - `Normalize` kẹp các số; `ResetToDefaults` đặt về mặc định.

- [ ] **Step 1: Test**
```csharp
[Fact] public void Hot_becomes_alarming_after_hold()   // 86 ở t=0..9 → State Hot, Update trả false; t=10 → Alarming, trả true; t=11 → trả false
[Fact] public void Zero_hold_alarms_immediately()
[Fact] public void Dip_below_threshold_restarts_hold()  // 86 t=0..5, 84 t=6, 86 t=7..16 → chưa Alarming tới t=17
[Fact] public void Hovering_near_threshold_does_not_restart_episode()   // Alarming rồi 84, 83, 85, 82.6(→83) → vẫn Alarming, không trả true lần nào
[Fact] public void Episode_ends_below_threshold_minus_margin()   // Alarming rồi 81.4 (→81 < 82) → Normal
[Fact] public void Null_reading_resets()
[Fact] public void Reset_then_needs_full_hold_again()
// Settings
[Fact] public void Alert_defaults_match_spec()   // 85, 90, 10, true, false
[Fact] public void Normalize_clamps_alert_values()   // 10/200/-5 → 50/110/0; HotAlertSeconds 99 → 60
[Fact] public void Store_round_trips_alert_settings()
```
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Cài `HeatAlarm` và các thuộc tính.
  - Ở `OverlayWindow.SetStats`, thay hằng ngưỡng cũ bằng `settings.GpuHotThreshold` / `settings.CpuHotThreshold` để app vẫn build.
- [ ] **Step 4:** `dotnet test` → PASS; build 0 cảnh báo.
- [ ] **Step 5:** Commit `feat(core): heat alarm tracker and alert settings`.

### Task 6: Cảnh báo trên overlay, tiếng, mục "// CẢNH BÁO" và nút thử

**Files:**
- Modify: `src/FpsOverlay/App.xaml.cs`, `src/FpsOverlay/OverlayWindow.xaml.cs`, `src/FpsOverlay/SettingsWindow.xaml(.cs)`, `src/FpsOverlay.Core/Localization/Loc.cs`

**Interfaces:**
- Consumes: `HeatAlarm`, cài đặt cảnh báo (Task 5); `StatRows.TempCell` (Task 2).
- Produces:
  - `OverlayWindow.SetAlerts(bool gpuBlinking, bool cpuBlinking)`.
    - Một `DispatcherTimer` 250 ms chỉ chạy khi có ô đang nháy.
    - Mỗi nhịp đổi Foreground của ô nhiệt giữa brush nóng và bản 25% alpha (frozen, cache theo màu).
    - Hết nháy thì trả brush thường/nóng như `SetStats` chọn.
  - `OverlayWindow.RunAlertTest(TimeSpan duration)`: trong `duration`, `SetStats` coi `ShowGpuTemp`/`ShowCpuTemp` là true, và cả hai ô nhiệt nháy bằng màu nóng.
    - Lúc này App đang xem trước nên mỗi giây gọi `SetAlerts(false, false)`. Trong `duration`, lần thử phải **thắng** lời gọi đó (overlay OR hai cờ với trạng thái thử), nếu không lần thử bị tắt sau tối đa 1 giây.
  - `App`:
    - hai trường `HeatAlarm _gpuAlarm, _cpuAlarm`;
    - trong `UpdateStats`, khi không xem trước: `started = alarm.Update(temp, threshold, settings.HotAlertSeconds, _clock.Elapsed.TotalSeconds)` nếu số nhiệt đó đang bật hiện, ngược lại `Reset()`;
    - xem trước → Reset cả hai;
    - `started && HotAlertSound` → `SystemSounds.Exclamation.Play()`;
    - gọi `overlay.SetAlerts(HotAlertBlink && gpu.State == Alarming, ...)`;
    - `ToggleOverlay` khi ẩn → Reset cả hai và `SetAlerts(false, false)`.
  - `SettingsWindow` nhận thêm tham số `Action testAlert`. App truyền vào lambda gọi `overlay.RunAlertTest(TimeSpan.FromSeconds(3))` và phát tiếng nếu `HotAlertSound`.
  - Loc khoá mới:

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `SectionAlerts` | "// CẢNH BÁO" | "// ALERTS" |
    | `GpuHotThreshold` | "Ngưỡng nóng GPU" | "GPU hot threshold" |
    | `CpuHotThreshold` | "Ngưỡng nóng CPU" | "CPU hot threshold" |
    | `HotAlertSeconds` | "Nóng liên tục (giây)" | "Hot for (seconds)" |
    | `HotAlertBlink` | "Nhấp nháy khi quá nóng" | "Blink when too hot" |
    | `HotAlertSound` | "Phát tiếng" | "Play sound" |
    | `TestAlert` | "Thử cảnh báo" | "Test alert" |

  - Bố cục mục: hai thanh trượt 50–110 hiện "85°", một thanh trượt 0–60 hiện "10s", hai công tắc, một nút.

- [ ] **Step 1:** Cài theo Interfaces. Test `Every_key_has_both_languages` bao các khoá mới.
- [ ] **Step 2:** `dotnet test` → PASS; build 0 cảnh báo.
- [ ] **Step 3: Kiểm bằng mắt** (quyền Admin):
  - Hạ ngưỡng GPU xuống 50, `HotAlertSeconds` 0, chơi game: số nhiệt GPU đỏ và nháy; bật tiếng thì bíp một lần.
  - Tắt hiện GPU °C: hết nháy.
  - Nút "Thử cảnh báo" khi đã tắt cả hai số nhiệt: hai số hiện ra và nháy 3 giây rồi biến.
- [ ] **Step 4:** Commit `feat(alerts): blinking hot temperatures, optional sound, alerts section with test button`.

### Task 7: Neon tím và Terminal xanh trong Core

**Files:**
- Modify: `OverlaySettings.cs` (enum + 2 thuộc tính màu), `StyleSpec.cs`, `SettingsStore.cs`
- Test: `StyleSpecTests.cs`, `OverlaySettingsTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `OverlayStyle { Classic, Cyberpunk, NeonPurple, TerminalGreen }` (thêm vào cuối để giá trị số cũ không đổi).
  - `OverlaySettings.NeonPurpleColors`, `TerminalGreenColors`.
  - Bảng (đúng giá trị spec phần C):

    | | NeonPurple | TerminalGreen |
    |---|---|---|
    | DefaultColors | `#FF4FD8`, `#F3E6FF`, `#FF4FD8` | `#1FAA44`, `#33FF66`, `#7CFF9E` |
    | Good / Medium / Bad | `#FF4FD8` / `#FFB86C` / `#FF2E4D` | `#7CFF9E` / `#E5FF4F` / `#FF5F45` |
    | Background | `#1A0828` | `#000800` |
    | Separator / Prompt | `" · "` / `""` | `" \| "` / `"> "` |

  - Swatches: 10 màu tự chọn hợp phong cách (người làm quyết), gồm 3 màu mặc định.

- [ ] **Step 1: Test**
```csharp
[Fact] public void Neon_purple_spec_matches_design()       // các giá trị trong bảng
[Fact] public void Terminal_green_spec_matches_design()
[Fact] public void Swatches_contain_default_colors()       // mọi phong cách: DefaultColors.Label/Value/Fps nằm trong Swatches
[Fact] public void Reset_keeps_colors_of_other_three_styles()   // Style = NeonPurple, chỉnh màu cả 4 → Reset: NeonPurple về mặc định, 3 bộ kia giữ
[Fact] public void Store_round_trips_new_styles()          // Style "TerminalGreen" + TerminalGreenColors
```
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Cài.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat(core): Neon Purple and Terminal Green styles`.

### Task 8: Hai phong cách mới trên overlay, font và cài đặt

**Files:**
- Create:
  - `src/FpsOverlay/Assets/Fonts/Orbitron-Bold.ttf`, `OFL-Orbitron.txt`;
  - `src/FpsOverlay/Assets/Fonts/VT323-Regular.ttf`, `OFL-VT323.txt`.
- Modify: `src/FpsOverlay/OverlayTheme.cs`, `OverlayWindow.xaml(.cs)`, `SettingsWindow.xaml(.cs)`, `Loc.cs`, `src/FpsOverlay/Assets/Licenses/THIRD-PARTY-NOTICES.txt`

**Interfaces:**
- Consumes: `StyleSpecs.For` cho 4 phong cách (Task 7); `OverlayVisuals` (Task 4).
- Produces:
  - `OverlayTheme.Visuals(NeonPurple)`: label Orbitron, value JetBrains Mono, cỡ 17/24/13/18, viền 1.5 cả bốn cạnh `#C64BFF`, `BorderGlow = true`, bo 10, `LabelsAndFps`.
  - `OverlayTheme.Visuals(TerminalGreen)`: VT323/VT323, cỡ 24/32/20/24, không viền, góc vuông, `All`, `Scanlines = true`.
  - Cỡ chữ là điểm khởi đầu; chỉnh bằng mắt so với `vertical-v2.html` / `styles-layout.html`.
  - Viền phát sáng vẽ bằng một `Border` riêng phía sau nội dung mang `DropShadowEffect` màu `#C64BFF`, để chữ không bị nhoè.
  - Vạch CRT: `DrawingBrush` lặp (vạch tối 1 px, chu kỳ 3 px) phủ trên nền.
  - Loc: `StyleNeonPurple` ("Neon tím" / "Neon Purple"), `StyleTerminalGreen` ("Terminal xanh" / "Terminal Green").
  - Cài đặt có 4 nút phong cách (`Tag` = giá trị enum, dùng lại `OnStyleChecked`). Swatch lấy từ `StyleSpecs.For(style).Swatches`, thay hai mảng hằng trong `SettingsWindow`.

- [ ] **Step 1: Lấy font.**
  - VT323: `https://github.com/google/fonts/raw/main/ofl/vt323/VT323-Regular.ttf` và `.../ofl/vt323/OFL.txt`.
  - Orbitron Bold tĩnh: lấy từ `https://github.com/theleagueof/orbitron` (thư mục bản phát hành có `Orbitron-Bold.ttf` hoặc `Orbitron Bold.ttf`) kèm `OFL.txt` của kho đó. Không có thì dùng `ofl/orbitron/Orbitron[wght].ttf` của google/fonts và ghi lại trong báo cáo (đậm giả).
  - Kiểm: mở bằng `[System.Windows.Media.GlyphTypeface]` hoặc xem tên family đúng "Orbitron" / "VT323".
  - Thêm 2 dòng vào `THIRD-PARTY-NOTICES.txt` theo mẫu dòng Chakra Petch.
- [ ] **Step 2:** Cài `Visuals` cho hai phong cách, viền phát sáng, vạch CRT, 4 nút phong cách, swatch từ bảng.
- [ ] **Step 3:** `dotnet test` → PASS; `dotnet build src/FpsOverlay` 0 cảnh báo.
- [ ] **Step 4: Kiểm bằng mắt** (quyền Admin):
  - Chọn từng phong cách mới, so với `.superpowers/brainstorm/392-1793687877/content/styles-layout.html`.
  - Đổi màu nhãn ở Neon: ánh sáng đổi theo.
  - Cổ điển/Cyberpunk không đổi.
- [ ] **Step 5:** Commit `feat(styles): Neon Purple and Terminal Green overlay styles with Orbitron and VT323`.

### Task 9: Vị trí theo offset, góc gần nhất và kẹp trong màn hình (Core)

**Files:**
- Modify: `src/FpsOverlay.Core/OverlayPlacement.cs`, `OverlaySettings.cs`, `SettingsStore.cs`
- Test: `OverlayPlacementTests.cs`, `OverlaySettingsTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `OverlaySettings.Layout` (`OverlayLayout.Horizontal`), `OffsetX`, `OffsetY` (double DIP, mặc định `DefaultOffset = 8`, kẹp 0–`MaxOffset = 4000`).
  - `OverlaySettings WithCorner(OverlayCorner corner)`: đặt góc và đưa hai offset về 8.
  - `OverlayPlacement.Compute(PixelRect monitor, int windowWidth, int windowHeight, OverlayCorner corner, int offsetXPx, int offsetYPx) -> (int X, int Y)`.
    - Thay chữ ký cũ (một `marginPx`); sửa 3 test cũ truyền `8, 8`.
    - Kẹp để cửa sổ nằm trọn trong màn hình. Cửa sổ lớn hơn màn hình thì dính mép trái/trên như cũ.
  - `OverlayPlacement.FromWindow(PixelRect monitor, PixelRect window) -> (OverlayCorner Corner, int OffsetXPx, int OffsetYPx)`.
    - Tâm cửa sổ < tâm màn hình theo trục X → trái, ngược lại phải; trục Y tương tự.
    - Offset = khoảng từ mép tương ứng tới cửa sổ, kẹp ≥ 0.

- [ ] **Step 1: Test**
```csharp
[Fact] public void Offsets_apply_from_chosen_corner()   // BottomRight, offset (40, 100), cửa sổ 200x40 trên 1920x1080 → (1680, 940)
[Fact] public void Offset_larger_than_monitor_is_clamped_inside()   // TopLeft, offset (3000, 3000) → (1920-200, 1080-40)
[Theory] [InlineData(100, 100, OverlayCorner.TopLeft, 100, 100)] [InlineData(1600, 100, OverlayCorner.TopRight, 120, 100)]
         [InlineData(100, 1000, OverlayCorner.BottomLeft, 100, 40)] [InlineData(1700, 1000, OverlayCorner.BottomRight, 20, 40)]
public void From_window_picks_nearest_corner(int left, int top, OverlayCorner corner, int ox, int oy)   // cửa sổ 200x40 trên Primary
[Fact] public void From_window_then_compute_round_trips()   // trên màn phụ toạ độ âm (-2560,0,0,1440)
[Fact] public void With_corner_resets_offsets()
[Fact] public void Layout_and_offsets_defaults_and_store_round_trip()
[Fact] public void Normalize_clamps_offsets()   // -5 → 0, 9999 → 4000, NaN → 8
```
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Cài. Ở `OverlayWindow.UpdatePosition`, đổi DIP → px theo DPI màn hình (`offset * dpi / 96`, làm tròn) và gọi chữ ký mới. Tray `CornerRequested` và `SettingsWindow.OnCornerChecked` dùng `WithCorner`.
- [ ] **Step 4:** `dotnet test` → PASS; build 0 cảnh báo.
- [ ] **Step 5:** Commit `feat(core): corner offsets, nearest-corner and full on-screen clamp`.

### Task 10: Bố cục dọc

**Files:**
- Modify: `src/FpsOverlay/OverlayWindow.xaml(.cs)`, `SettingsWindow.xaml(.cs)`, `Loc.cs`

**Interfaces:**
- Consumes: `OverlaySettings.Layout` (Task 9), `StatRows.Values(..., layout)` (Task 2), `OverlayVisuals` (Task 4/8).
- Produces:
  - Khi `Layout == Vertical`: các dòng nằm trong một `Grid` 3 cột (nhãn | ô First căn phải | ô Second), mỗi dòng hiện là một hàng.
    - Không có dấu ngăn; dấu nhắc chỉ ở hàng FPS.
    - Dòng ẩn thì hàng ẩn (Collapsed).
    - Ô Second của CLK/PWR/FAN dùng cỡ và màu nhãn phụ.
  - Đổi bố cục chỉ xếp lại các đối tượng chữ đã có, không tạo lại mỗi giây. `SetFps` vẫn không cấp phát.
  - Viền, vát, bo, phát sáng, vạch CRT bao cả khối (đã nằm trên `Chrome`).
  - Cài đặt: hai nút "Ngang" / "Dọc" trong "// GIAO DIỆN" cạnh chọn góc. Loc:

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `Layout` | "Bố cục" | "Layout" |
    | `LayoutHorizontal` | "Ngang" | "Horizontal" |
    | `LayoutVertical` | "Dọc" | "Vertical" |

- [ ] **Step 1:** Cài.
- [ ] **Step 2:** `dotnet test` → PASS; build 0 cảnh báo.
- [ ] **Step 3: Kiểm bằng mắt** (quyền Admin):
  - 4 phong cách ở bố cục dọc, so với `vertical-v2.html`.
  - Bật/tắt từng công tắc: hàng tương ứng biến mất.
  - Chỉ bật GPU °C: nhiệt nằm ở cột 2.
  - Đổi Scale: khối co giãn đúng, góc vẫn đúng.
- [ ] **Step 4:** Commit `feat(overlay): vertical table layout`.

### Task 11: Chế độ di chuyển (kéo thả)

**Files:**
- Modify: `src/FpsOverlay/OverlayWindow.xaml.cs`, `src/FpsOverlay/TrayIcon.cs`, `src/FpsOverlay/App.xaml.cs`, `src/FpsOverlay/NativeMethods.cs` (nếu thiếu `SetForegroundWindow`), `Loc.cs`

**Interfaces:**
- Consumes: `OverlayPlacement.FromWindow`, `OffsetX/OffsetY`, `WithCorner` (Task 9).
- Produces:
  - `OverlayWindow`:
    - `bool IsMoving`;
    - `void BeginMove()`;
    - `void EndMove(bool save)`;
    - `event Action<OverlayCorner, double, double>? PositionSaved` (góc, offset X/Y theo DIP);
    - `event Action<bool>? MovingChanged`.
  - `BeginMove`:
    - nhớ `GetForegroundWindow()` và vị trí hiện tại;
    - bỏ `WS_EX_TRANSPARENT` và `WS_EX_NOACTIVATE`; `IsHitTestVisible = true`; con trỏ `SizeAll`;
    - viền đứt nét quanh `Chrome` (vẽ thêm, không đổi kích thước khối);
    - `Activate()`; `UpdatePosition` ngừng tự chạy;
    - kéo bằng `DragMove()` khi `MouseLeftButtonDown`.
  - Kết thúc:
    - Enter hoặc bấm đúp → `EndMove(true)`; Esc → `EndMove(false)` (về vị trí đã nhớ);
    - lưu: lấy màn hình chứa tâm cửa sổ (`MonitorFromRect`/`MonitorFromWindow`), `FromWindow`, đổi px → DIP theo DPI màn hình đó, phát `PositionSaved`;
    - mọi đường kết thúc: trả đủ 2 cờ, bỏ viền đứt nét, `IsHitTestVisible = false`, cố `SetForegroundWindow(cửa sổ đã nhớ)` (lỗi thì bỏ qua), chạy lại `UpdatePosition`.
  - `TargetWindow()` trả màn hình hiện tại của overlay khi `IsMoving`, không về màn hình chính.
  - `TrayIcon`:
    - mục "Di chuyển overlay" / "Move overlay" (khoá Loc `TrayMove`) đặt sau "Góc hiển thị";
    - `event Action? MoveRequested`; `void SetMoving(bool)` (dấu tick);
    - `Apply` đặt `Enabled = overlayVisible`.
  - `App`:
    - `MoveRequested` → `overlay.IsMoving ? EndMove(true) : BeginMove()`;
    - `PositionSaved` → `settings.Update(s => s with { Corner = c, OffsetX = x, OffsetY = y })`;
    - `MovingChanged` → `tray.SetMoving`;
    - `ToggleOverlay` (ẩn) và `OpenSettings` gọi `EndMove(true)` nếu đang di chuyển.

- [ ] **Step 1:** Cài theo Interfaces.
- [ ] **Step 2:** `dotnet test` → PASS; build 0 cảnh báo.
- [ ] **Step 3: Kiểm bằng mắt** (quyền Admin): kéo tới giữa phải màn hình rồi Enter, sau đó kiểm:
  - `settings.json` có `Corner` TopRight và offset hợp lý;
  - mở game: overlay đúng chỗ và vẫn bấm xuyên;
  - Esc huỷ được;
  - bấm đúp lưu được;
  - chọn góc trong cài đặt: về sát góc 8 DIP.
- [ ] **Step 4:** Commit `feat(overlay): move mode with drag, Enter/Esc/double-click and focus restore`.

### Task 12: Phiên bản, tài liệu và danh sách kiểm tay

**Files:**
- Modify: `src/FpsOverlay/FpsOverlay.csproj` (`<Version>1.1.0</Version>`), `HANDOFF.md`

- [ ] **Step 1:** Đổi version.
- [ ] **Step 2:** Cập nhật `HANDOFF.md`:
  - bảng lịch sử thêm "Đợt 5";
  - mục tài liệu thêm spec và plan này;
  - trạng thái hiện tại; số test mới.
- [ ] **Step 3:** Thêm vào mục 9 của `HANDOFF.md` các mục kiểm tay của đợt này:
  1. Cổ điển và Cyberpunk ngang trông như bản 1.0.0.
  2. VRAM/CLK/PWR/FAN đúng trên GTX 1650 khi chơi; ở desktop là `--` hoặc số của GPU tích hợp; FAN `--` nếu máy không báo.
  3. Log mới có 4 cột cuối.
  4. Cảnh báo:
     - ngưỡng thấp + 0 giây: nháy, bíp một lần;
     - nhiệt dao động sát ngưỡng không bíp lại;
     - tắt hiện nhiệt: hết nháy.
  5. Nút "Thử cảnh báo" khi đã tắt hai số nhiệt.
  6. Neon tím và Terminal xanh ở cả hai bố cục, so với bản vẽ thử; đổi màu nhãn thì ánh sáng đổi theo.
  7. Bố cục dọc: tắt/bật từng mục; chỉ bật nhiệt; Scale 50% và 300%.
  8. Kéo thả: Enter, Esc, bấm đúp, bấm lại mục khay.
  9. Kết thúc chế độ di chuyển bằng cách ẩn overlay hoặc mở cài đặt: overlay lại bấm xuyên, không chiếm focus.
  10. Sau khi kéo: focus về game; overlay theo game sang màn hình phụ với cùng góc và khoảng cách.
  11. Cập nhật đè bản 1.0.0 bằng file mang đi 1.1.0: hộp thoại báo Cập nhật; cài đặt cũ (màu đã chỉnh) còn nguyên.
- [ ] **Step 4:** `dotnet test` → PASS; `dotnet build src/FpsOverlay` 0 cảnh báo; `dotnet publish src/FpsOverlay -c Release -o publish` thành công.
- [ ] **Step 5:** Commit `chore: version 1.1.0 and handoff for batch 5`.
