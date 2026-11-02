# UI Themes & Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overlay một dòng có hai phong cách (Cổ điển / Cyberpunk), đổi màu theo FPS, cửa sổ cài đặt kiểu Cyberpunk với xem trước trực tiếp, hai ngôn ngữ Việt/Anh, menu khay mở rộng và icon riêng.

**Architecture:** Lõi (`FpsOverlay.Core`) thêm mô hình cài đặt bất biến có chuẩn hoá giá trị, đọc/ghi JSON, phân loại mức FPS, số FPS mẫu cho chế độ xem trước và bảng chuỗi hai ngôn ngữ — tất cả có test. App giữ cài đặt hiện tại trong `SettingsService` (phát sự kiện khi đổi, ghi file có trì hoãn); overlay, menu khay và cửa sổ cài đặt đều lắng nghe service này và áp dụng ngay.

**Tech Stack:** C# / .NET 10, WPF (không thư viện giao diện ngoài), System.Text.Json, xUnit. Font Chakra Petch + JetBrains Mono (SIL OFL) đóng gói làm resource.

**Spec:** `docs/superpowers/specs/2026-11-02-ui-themes-settings-design.md`

## Global Constraints

- Không dùng WPF-UI hay thư viện giao diện có sẵn; tự viết style/template.
- Overlay luôn bấm xuyên qua, không chiếm focus — kể cả khi cửa sổ cài đặt mở và sau khi đổi phong cách.
- Mọi font hiển thị chữ tiếng Việt phải có đủ bộ ký tự tiếng Việt (Chakra Petch, JetBrains Mono, Consolas).
- Mọi thay đổi cài đặt áp dụng ngay, không khởi động lại.
- File cài đặt `%AppData%\FpsOverlay\settings.json`; thiếu/hỏng/giá trị lạ không được làm app không mở.
- Giữ nguyên hành vi đợt trước: `FpsCalculator`, `PresentMonRunner`, mutex một bản chạy, hộp lỗi `DefaultDesktopOnly`, đặt lại topmost mỗi giây.
- Màu Cổ điển: nhãn `#FF8C1A`, giá trị `#FFFFFF`, FPS `#39FF14`; mức tốt/trung bình/kém `#39FF14` / `#FFD400` / `#FF3B30`. Font Consolas đậm, nền đen bo góc 4px.
- Màu Cyberpunk: nhãn `#00F0FF`, giá trị `#FFFFFF`, FPS `#FCEE0A`; mức tốt/trung bình/kém `#00F0FF` / `#FCEE0A` / `#FF003C`; nền `#0A0A0F`, viền trái 3px `#FCEE0A`, góc phải dưới vát 9px; FPS phát sáng nhẹ.
- Mặc định: Cyberpunk, trên trái, kích cỡ 100%, độ mờ nền 72%, đổi màu bật, mốc tốt 60, mốc kém 30, tiếng Việt.
- Khoảng: kích cỡ 50–300%, độ mờ nền 0–100%, mốc 10–240 với mốc kém < mốc tốt.
- Kích cỡ 100%: số FPS cao 24px, nhãn ≈ 18px (3/4). Lề 8px tới mép màn hình chính (không trừ taskbar).

## Review Focus

1. **`settings.json` hỏng một phần** (một màu sai định dạng, mốc ngược, enum lạ) → chỉ phần hỏng về mặc định, phần còn lại giữ. Test: Task 1.
2. **Kéo mốc kém vượt mốc tốt (hoặc ngược lại)** → mốc kia bị đẩy theo, không bao giờ kém ≥ tốt. Test: Task 1.
3. **Đổi phong cách / kích cỡ / góc khi overlay đang hiện** → overlay vẫn bấm xuyên qua và vẫn nằm trọn trong màn hình (300% ở góc dưới phải). Test: Task 3 kiểm tra thủ công.
4. **Đổi ngôn ngữ khi cửa sổ cài đặt đang mở** → mọi chữ trong cửa sổ và menu khay đổi ngay. Test: Task 4 kiểm tra thủ công; Task 2 test mọi khoá chuỗi có đủ hai bản.
5. **Kéo thanh trượt liên tục** → không ghi file dồn dập, không lỗi khoá file. Test: Task 3 (`SettingsService` ghi trì hoãn 500 ms), kiểm tra thủ công số lần ghi.

---

### Task 1: Mô hình cài đặt và lưu trữ (lõi)

**Files:**
- Create: `src/FpsOverlay.Core/Settings/OverlaySettings.cs`, `src/FpsOverlay.Core/Settings/StyleColors.cs`, `src/FpsOverlay.Core/Settings/HexColor.cs`, `src/FpsOverlay.Core/Settings/SettingsStore.cs`
- Test: `tests/FpsOverlay.Core.Tests/OverlaySettingsTests.cs`, `tests/FpsOverlay.Core.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces (namespace `FpsOverlay.Core.Settings`):
  - `enum OverlayStyle { Classic, Cyberpunk }`, `enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }`, `enum AppLanguage { Vietnamese, English }`
  - `sealed record StyleColors(string Label, string Value, string Fps)` + `static StyleColors DefaultFor(OverlayStyle style)` (giá trị theo Global Constraints).
  - `static class HexColor` — `bool TryNormalize(string? input, out string normalized)`: nhận `#RRGGBB` hoặc `RRGGBB`, không phân biệt hoa thường, trả `#RRGGBB` viết hoa.
  - `sealed record OverlaySettings` với thuộc tính `Style, Corner, Scale (double, 0.5–3.0), BackgroundOpacity (double, 0–1), ColorByFps (bool), GoodThreshold (int), BadThreshold (int), Language, ClassicColors (StyleColors), CyberpunkColors (StyleColors)`; hằng `MinThreshold = 10`, `MaxThreshold = 240`.
    - `static OverlaySettings Defaults`
    - `OverlaySettings Normalize()` — kẹp mọi khoảng; màu không hợp lệ → màu mặc định của đúng ô đó; nếu `BadThreshold >= GoodThreshold` thì `BadThreshold = GoodThreshold - 1` (sau khi kẹp `GoodThreshold` vào `[11, 240]`).
    - `StyleColors ColorsFor(OverlayStyle style)`, `OverlaySettings WithColors(OverlayStyle style, StyleColors colors)`
    - `OverlaySettings WithGoodThreshold(int value)` — kẹp; nếu `BadThreshold >= value` thì đẩy `BadThreshold = value - 1`. `OverlaySettings WithBadThreshold(int value)` — kẹp; nếu `value >= GoodThreshold` thì đẩy `GoodThreshold = value + 1`.
    - `OverlaySettings ResetToDefaults()` — về `Defaults` nhưng giữ `Language`, giữ phong cách đang chọn (`Style`) và bộ màu của phong cách **không** được chọn; bộ màu của phong cách đang chọn về mặc định.
  - `static class SettingsStore` — `OverlaySettings Load(string path)` (thiếu file, JSON hỏng, ngoại lệ IO → `Defaults`; đọc được thì `.Normalize()`; thuộc tính thiếu lấy từ `Defaults`; enum là chuỗi, enum lạ → mặc định của thuộc tính đó); `void Save(string path, OverlaySettings settings)` (tạo thư mục nếu chưa có, ghi file tạm rồi `File.Move(..., overwrite: true)`). `static string DefaultPath` = `%AppData%\FpsOverlay\settings.json`.

- [ ] **Step 1: Viết test**

```csharp
// OverlaySettingsTests
[Fact] public void Defaults_match_spec()
{
    var d = OverlaySettings.Defaults;
    Assert.Equal(OverlayStyle.Cyberpunk, d.Style); Assert.Equal(OverlayCorner.TopLeft, d.Corner);
    Assert.Equal(1.0, d.Scale); Assert.Equal(0.72, d.BackgroundOpacity); Assert.True(d.ColorByFps);
    Assert.Equal(60, d.GoodThreshold); Assert.Equal(30, d.BadThreshold); Assert.Equal(AppLanguage.Vietnamese, d.Language);
    Assert.Equal(new StyleColors("#00F0FF", "#FFFFFF", "#FCEE0A"), d.CyberpunkColors);
    Assert.Equal(new StyleColors("#FF8C1A", "#FFFFFF", "#39FF14"), d.ClassicColors);
}
[Fact] public void Normalize_clamps_ranges()
{
    var s = (OverlaySettings.Defaults with { Scale = 9, BackgroundOpacity = -1, GoodThreshold = 999, BadThreshold = 1 }).Normalize();
    Assert.Equal(3.0, s.Scale); Assert.Equal(0, s.BackgroundOpacity); Assert.Equal(240, s.GoodThreshold); Assert.Equal(10, s.BadThreshold);
}
[Fact] public void Normalize_fixes_inverted_thresholds()          // Review Focus 2
{
    var s = (OverlaySettings.Defaults with { GoodThreshold = 40, BadThreshold = 50 }).Normalize();
    Assert.Equal(40, s.GoodThreshold); Assert.Equal(39, s.BadThreshold);
}
[Fact] public void Normalize_replaces_only_bad_color()            // Review Focus 1
{
    var s = (OverlaySettings.Defaults with { CyberpunkColors = new("nope", "#abcdef", "#FCEE0A") }).Normalize();
    Assert.Equal(new StyleColors("#00F0FF", "#ABCDEF", "#FCEE0A"), s.CyberpunkColors);
}
[Fact] public void Dragging_bad_above_good_pushes_good()          // Review Focus 2
{
    var s = OverlaySettings.Defaults.WithBadThreshold(80);
    Assert.Equal(80, s.BadThreshold); Assert.Equal(81, s.GoodThreshold);
}
[Fact] public void Dragging_good_below_bad_pushes_bad()
{
    var s = OverlaySettings.Defaults.WithGoodThreshold(20);
    Assert.Equal(20, s.GoodThreshold); Assert.Equal(19, s.BadThreshold);
}
[Fact] public void Reset_keeps_language_style_and_other_style_colors()
{
    var custom = new StyleColors("#111111", "#222222", "#333333");
    var s = (OverlaySettings.Defaults with { Language = AppLanguage.English, Style = OverlayStyle.Classic, Scale = 2,
        ClassicColors = custom, CyberpunkColors = custom }).ResetToDefaults();
    Assert.Equal(AppLanguage.English, s.Language); Assert.Equal(OverlayStyle.Classic, s.Style); Assert.Equal(1.0, s.Scale);
    Assert.Equal(StyleColors.DefaultFor(OverlayStyle.Classic), s.ClassicColors); Assert.Equal(custom, s.CyberpunkColors);
}
[Theory] [InlineData("#abcdef", "#ABCDEF")] [InlineData("00f0ff", "#00F0FF")]
public void Hex_normalizes(string input, string expected) { Assert.True(HexColor.TryNormalize(input, out var n)); Assert.Equal(expected, n); }
[Theory] [InlineData("")] [InlineData("#12345")] [InlineData("#GGGGGG")] [InlineData(null)]
public void Hex_rejects(string? input) => Assert.False(HexColor.TryNormalize(input, out _));

// SettingsStoreTests (mỗi test dùng một thư mục tạm riêng, xoá trong Dispose)
[Fact] public void Missing_file_gives_defaults()
[Fact] public void Corrupt_json_gives_defaults()                   // ghi "{ not json" → Assert.Equal(OverlaySettings.Defaults, Load(path))
[Fact] public void Round_trips()                                   // Save rồi Load một bản đã đổi Style/Corner/Scale/màu → bằng nhau
[Fact] public void Unknown_enum_and_missing_fields_fall_back()     // ghi {"Style":"Neon","Scale":2} → Style = Cyberpunk, Scale = 2, còn lại = Defaults
[Fact] public void Save_creates_directory()                        // path trong thư mục con chưa tồn tại → File.Exists sau Save
```

- [ ] **Step 2: Chạy, xác nhận fail**

Run: `dotnet test --filter "OverlaySettingsTests|SettingsStoreTests"`
Expected: FAIL (lỗi biên dịch, chưa có type).

- [ ] **Step 3: Viết các type trên.** Đọc JSON bằng `JsonDocument` và lấy từng thuộc tính một để thuộc tính lỗi không kéo cả file về mặc định.

- [ ] **Step 4: Chạy toàn bộ test**

Run: `dotnet test`
Expected: PASS (15 test cũ + test mới).

- [ ] **Step 5: Commit** — `feat: overlay settings model and JSON store`

---

### Task 2: Mức FPS, số mẫu xem trước, chuỗi hai ngôn ngữ (lõi)

**Files:**
- Create: `src/FpsOverlay.Core/FpsLevels.cs`, `src/FpsOverlay.Core/PreviewFps.cs`, `src/FpsOverlay.Core/Localization/Loc.cs`
- Test: `tests/FpsOverlay.Core.Tests/FpsLevelsTests.cs`, `tests/FpsOverlay.Core.Tests/PreviewFpsTests.cs`, `tests/FpsOverlay.Core.Tests/LocTests.cs`

**Interfaces:**
- Consumes: `AppLanguage` (Task 1).
- Produces:
  - `enum FpsLevel { None, Good, Medium, Bad }`; `static class FpsLevels` — `FpsLevel Classify(double? fps, int goodThreshold, int badThreshold)`: `null` → `None`; phân loại trên giá trị **đã làm tròn** (khớp số hiển thị): `≥ good` → `Good`, `< bad` → `Bad`, còn lại `Medium`.
  - `static class PreviewFps` — `const double PeriodSeconds = 6`; `double At(double seconds)`: sóng tam giác 20 → 144 → 20 theo chu kỳ.
  - `enum StringKey` và `static class Loc` — `string Get(StringKey key, AppLanguage language)`. Khoá và chuỗi (Việt / Anh):
    `TrayTooltip` "FPS Overlay" / "FPS Overlay"; `TrayHide` "Ẩn FPS" / "Hide FPS"; `TrayShow` "Hiện FPS" / "Show FPS"; `TraySettings` "Cài đặt..." / "Settings..."; `TrayCorner` "Góc hiển thị" / "Position"; `TrayExit` "Thoát" / "Exit";
    `CornerTopLeft` "Trên trái" / "Top left"; `CornerTopRight` "Trên phải" / "Top right"; `CornerBottomLeft` "Dưới trái" / "Bottom left"; `CornerBottomRight` "Dưới phải" / "Bottom right";
    `SettingsTitle` "CÀI ĐẶT" / "SETTINGS"; `SectionAppearance` "// GIAO DIỆN" / "// APPEARANCE"; `SectionColors` "// MÀU SẮC" / "// COLORS"; `SectionFpsColor` "// MÀU THEO FPS" / "// FPS COLORS";
    `Language` "Ngôn ngữ" / "Language"; `Style` "Phong cách" / "Style"; `StyleClassic` "Cổ điển" / "Classic"; `StyleCyberpunk` "Cyberpunk" / "Cyberpunk"; `Corner` "Góc hiển thị" / "Position"; `Scale` "Kích cỡ" / "Size"; `BackgroundOpacity` "Độ mờ nền" / "Background"; `LabelColor` "Màu nhãn" / "Label color"; `ValueColor` "Màu giá trị" / "Value color"; `FpsColor` "Màu FPS" / "FPS color"; `ColorByFps` "Đổi màu theo FPS" / "Color by FPS"; `GoodThreshold` "Tốt từ" / "Good from"; `BadThreshold` "Kém dưới" / "Bad below"; `ResetDefaults` "KHÔI PHỤC MẶC ĐỊNH" / "RESTORE DEFAULTS"; `Done` "XONG" / "DONE";
    `AlreadyRunning` "FPS Overlay đang chạy rồi (xem biểu tượng ở khay hệ thống)." / "FPS Overlay is already running (see the tray icon).";
    `PresentMonStopped` "PresentMon đã dừng bất ngờ nên không đo được FPS.\nHãy chắc rằng app đang chạy với quyền Administrator." / "PresentMon stopped unexpectedly, so FPS can't be measured.\nMake sure the app is running as Administrator.";
    `PresentMonStartFailed` "Không khởi động được PresentMon.\nFile PresentMon.exe phải nằm cùng thư mục với FpsOverlay.exe." / "Couldn't start PresentMon.\nPresentMon.exe must be in the same folder as FpsOverlay.exe.";
    `Details` "Chi tiết" / "Details".

- [ ] **Step 1: Viết test**

```csharp
[Theory]
[InlineData(null, FpsLevel.None)] [InlineData(60.0, FpsLevel.Good)] [InlineData(59.6, FpsLevel.Good)]
[InlineData(59.4, FpsLevel.Medium)] [InlineData(30.0, FpsLevel.Medium)] [InlineData(29.4, FpsLevel.Bad)]
public void Classify(double? fps, FpsLevel expected) => Assert.Equal(expected, FpsLevels.Classify(fps, 60, 30));

[Theory] [InlineData(0, 20)] [InlineData(1.5, 82)] [InlineData(3, 144)] [InlineData(4.5, 82)] [InlineData(6, 20)] [InlineData(9, 144)]
public void Preview_triangle(double t, double expected) => Assert.Equal(expected, PreviewFps.At(t), 3);

[Fact] public void Every_key_has_both_languages()                   // Review Focus 4
{
    foreach (var key in Enum.GetValues<StringKey>())
        foreach (var lang in Enum.GetValues<AppLanguage>())
            Assert.False(string.IsNullOrWhiteSpace(Loc.Get(key, lang)), $"{key}/{lang}");
}
[Fact] public void Spot_check() { Assert.Equal("Ẩn FPS", Loc.Get(StringKey.TrayHide, AppLanguage.Vietnamese)); Assert.Equal("Hide FPS", Loc.Get(StringKey.TrayHide, AppLanguage.English)); }
```

- [ ] **Step 2: Chạy, xác nhận fail** — `dotnet test --filter "FpsLevelsTests|PreviewFpsTests|LocTests"` → FAIL (chưa có type).
- [ ] **Step 3: Viết `FpsLevels`, `PreviewFps`, `Loc`** (`Loc` dùng `Dictionary<StringKey, (string Vi, string En)>`; khoá thiếu ném `KeyNotFoundException` để test bắt được).
- [ ] **Step 4: Chạy toàn bộ test** — `dotnet test` → PASS.
- [ ] **Step 5: Commit** — `feat: FPS levels, preview FPS and vi/en strings`

---

### Task 3: Overlay theo phong cách + SettingsService

**Files:**
- Create: `src/FpsOverlay/SettingsService.cs`, `src/FpsOverlay/OverlayTheme.cs`, `src/FpsOverlay/Assets/Fonts/` (ChakraPetch-Medium.ttf, ChakraPetch-SemiBold.ttf, ChakraPetch-Bold.ttf, JetBrainsMono-Medium.ttf, JetBrainsMono-Bold.ttf, OFL-ChakraPetch.txt, OFL-JetBrainsMono.txt)
- Modify: `src/FpsOverlay/OverlayWindow.xaml(.cs)`, `src/FpsOverlay/App.xaml.cs`, `src/FpsOverlay/FpsOverlay.csproj` (font là `<Resource>`)

**Interfaces:**
- Consumes: `OverlaySettings`, `SettingsStore`, `FpsLevels`, `PreviewFps` (Task 1, 2).
- Produces:
  - `sealed class SettingsService : IDisposable` — `OverlaySettings Current { get; }`; `event Action<OverlaySettings>? Changed` (gọi trên luồng UI); `void Update(Func<OverlaySettings, OverlaySettings> change)` (áp `change`, `.Normalize()`, nếu khác thì đặt `Current`, phát `Changed`, hẹn ghi file sau 500 ms — mỗi lần Update mới dời hẹn); `Dispose` ghi ngay nếu còn hẹn. Constructor `SettingsService(string path)` gọi `SettingsStore.Load`.
  - `static class OverlayTheme` — `Brush LevelBrush(OverlayStyle style, FpsLevel level)` theo màu mức trong Global Constraints; `FontFamily LabelFont(OverlayStyle)`, `FontFamily ValueFont(OverlayStyle)` (Classic: Consolas; Cyberpunk: `pack://application:,,,/Assets/Fonts/#Chakra Petch` và `#JetBrains Mono`).
  - `OverlayWindow`: `void Apply(OverlaySettings settings)` (font, màu, nền: Classic = `Border` bo 4px; Cyberpunk = nền có viền trái 3px vàng và góc phải dưới vát 9px qua `Clip`/`Path`; FPS Cyberpunk có `DropShadowEffect` cùng màu FPS, `BlurRadius 6`, `ShadowDepth 0`; kích cỡ qua `LayoutTransform` `ScaleTransform`; độ mờ nền qua alpha của nền), `void SetFps(double? fps, OverlaySettings settings)` (chữ = `FpsCalculator.Format`; màu = `LevelBrush` nếu `ColorByFps` và level ≠ None, ngược lại `ColorsFor(style).Fps`, riêng `--` dùng `ColorsFor(style).Value`). Overlay dựng theo dạng hàng ngang các mục nhãn+giá trị (đợt này một mục "FPS") để đợt 3 thêm mục. Vị trí tính lại sau mỗi `SizeChanged` và mỗi `Apply` theo `Corner`, lề 8 DIP, dựa trên `SystemParameters.PrimaryScreenWidth/Height`.
  - `App`: tạo `SettingsService(SettingsStore.DefaultPath)`, `overlay.Apply(Current)`, `Changed += overlay.Apply`; trong `OnRendering`: `fps = IsPreviewing ? PreviewFps.At(stopwatch) : calculator.GetFps(pid)`; `public bool IsPreviewing { get; set; }` (Task 4 bật/tắt).

- [ ] **Step 1: Hỏi người dùng, rồi tải font** từ repo Google Fonts chính thức (`github.com/google/fonts`, thư mục `ofl/chakrapetch` và `ofl/jetbrainsmono`), kèm file `OFL.txt` của từng font. JetBrains Mono trên Google Fonts là variable font; nếu không có file tĩnh Medium/Bold thì dùng file variable và ghi ruling.
- [ ] **Step 2: Viết `SettingsService`, `OverlayTheme`, sửa `OverlayWindow` và `App` như trên.**
- [ ] **Step 3: Build và test** — `dotnet build` → `Build succeeded`, 0 warning; `dotnet test` → PASS.
- [ ] **Step 4: Kiểm tra thủ công (người dùng chạy, cần Admin)** — sửa tay `settings.json` (hoặc dùng Task 4 khi xong) để xem: Cyberpunk mặc định ở trên trái hiện `--` vàng/trắng đúng mockup; đổi `Style` sang `Classic` → Consolas, nhãn cam; `Scale` 3 + `Corner` `BottomRight` → overlay nằm trọn trong màn hình (Review Focus 3); vào game → số đổi màu theo mốc; bấm chuột vào overlay vẫn trúng game.
- [ ] **Step 5: Commit** — `feat: themed overlay driven by settings service`

---

### Task 4: Cửa sổ cài đặt Cyberpunk, menu khay, icon, ngôn ngữ

**Files:**
- Create: `src/FpsOverlay/Themes/Cyberpunk.xaml` (ResourceDictionary), `src/FpsOverlay/SettingsWindow.xaml(.cs)`, `src/FpsOverlay/TrayIconImage.cs`
- Modify: `src/FpsOverlay/TrayIcon.cs`, `src/FpsOverlay/App.xaml(.cs)`

**Interfaces:**
- Consumes: `SettingsService` (Task 3), `Loc`, `StringKey` (Task 2), `OverlaySettings.With*`, `ResetToDefaults` (Task 1), `App.IsPreviewing` (Task 3).
- Produces:
  - `Themes/Cyberpunk.xaml`: style cho `Window` (nền `#0A0A0F`, `WindowChrome` không viền, thanh tiêu đề tự vẽ kéo được, góc trên phải và dưới trái vát 22px), `Button` chính (nền `#FCEE0A`, chữ đen) và `Button` viền đỏ `#FF003C`, `RadioButton` dạng nút phân đoạn (chọn = nền vàng chữ đen), `Slider` (rãnh `#2A2A35`, phần đã chọn `#00F0FF` phát sáng, núm vàng nghiêng), `ToggleButton` dạng công tắc, `TextBox` nhập mã hex, `RadioButton` ô màu. Font Chakra Petch; số dùng JetBrains Mono màu `#00F0FF`. Bám mockup `cyberpunk-v2`.
  - `SettingsWindow(SettingsService service)`: các dòng theo thứ tự — mục "Giao diện": Ngôn ngữ (Tiếng Việt / English), Phong cách, Góc hiển thị, Kích cỡ (50–300%, bước 10), Độ mờ nền (0–100%, bước 5); mục "Màu sắc" (cho phong cách đang chọn): Màu nhãn, Màu giá trị, Màu FPS — mỗi dòng 10 ô màu gợi ý + ô hex (gõ mã không hợp lệ thì viền đỏ, không áp dụng); mục "Màu theo FPS": công tắc, Tốt từ, Kém dưới (10–240, dùng `WithGoodThreshold`/`WithBadThreshold`); nút Khôi phục mặc định (`ResetToDefaults`) và Xong (đóng). Mọi control gọi `service.Update(...)`; cửa sổ nghe `Changed` để cập nhật control và chữ theo ngôn ngữ. Mở: `App.IsPreviewing = true`; đóng: `false`.
  - Ô màu gợi ý — Cyberpunk: `#00F0FF #FCEE0A #FF003C #FF2A6D #05D9E8 #D1F7FF #39FF14 #B967FF #FFFFFF #FF8C1A`; Cổ điển: `#FF8C1A #FFFFFF #39FF14 #FFD400 #FF3B30 #00BFFF #FF66CC #A0A0A0 #00FF99 #FFAA00`.
  - `TrayIcon`: thêm mục `TraySettings` (sự kiện `SettingsRequested`), menu con `TrayCorner` có 4 góc đánh dấu góc đang dùng (sự kiện `CornerRequested(OverlayCorner)`); `void Apply(OverlaySettings settings, bool overlayVisible)` cập nhật chữ theo ngôn ngữ, dấu chọn góc, chữ Ẩn/Hiện.
  - `TrayIconImage.Create()` → `System.Drawing.Icon`: vẽ chữ "FPS" vàng `#FCEE0A` trên nền đen 32×32 lúc chạy.
  - `App`: chỉ một `SettingsWindow` (mở rồi thì `Activate()`); thông báo lỗi dùng `Loc` theo `Current.Language`.

- [ ] **Step 1: Viết `Cyberpunk.xaml`, `SettingsWindow`, `TrayIconImage`, sửa `TrayIcon` và `App`.**
- [ ] **Step 2: Build và test** — `dotnet build` → `Build succeeded`, 0 warning; `dotnet test` → PASS.
- [ ] **Step 3: So với mockup** — chạy cửa sổ cài đặt (có thể tạm mở bằng một cấu hình debug không cần PresentMon), chụp màn hình, so với `cyberpunk-v2`; chữ tiếng Việt cùng một font.
- [ ] **Step 4: Đóng gói** — `dotnet publish src/FpsOverlay -c Release -r win-x64 --self-contained false -o publish` → có `FpsOverlay.exe`, `PresentMon.exe`.
- [ ] **Step 5: Kiểm tra thủ công (người dùng)** — mở Cài đặt: overlay chạy số mẫu đổi đủ ba màu; đổi từng mục thấy ngay trên overlay; đổi ngôn ngữ khi cửa sổ đang mở → mọi chữ và menu khay đổi (Review Focus 4); kéo thanh trượt liên tục rồi xem `settings.json` chỉ được ghi sau khi dừng (Review Focus 5); đóng cửa sổ → về FPS thật; tắt mở app → cài đặt còn nguyên; overlay vẫn bấm xuyên qua khi cửa sổ cài đặt mở.
- [ ] **Step 6: Commit** — `feat: cyberpunk settings window, tray menu, icon and vi/en`
