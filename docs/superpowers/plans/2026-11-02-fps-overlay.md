# FPS Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** App Windows hiện số FPS (chữ xanh lá, góc trên bên trái) của game đang ở phía trước, bật/tắt qua biểu tượng khay hệ thống.

**Architecture:** Hai project. `FpsOverlay.Core` (thư viện thuần, có test) đọc CSV của PresentMon và tính FPS theo cửa sổ trượt. `FpsOverlay` (WPF) chạy PresentMon ngầm, lấy ID tiến trình của cửa sổ phía trước, vẽ con số trên một cửa sổ trong suốt bấm xuyên qua, và quản lý biểu tượng khay hệ thống. Phần đo chỉ đưa ra con số; phần hiển thị không biết PresentMon tồn tại.

**Tech Stack:** C# / .NET 10, WPF, `System.Windows.Forms.NotifyIcon` (khay hệ thống), xUnit, PresentMon 2.x (console, Intel, MIT).

**Spec:** `docs/superpowers/specs/2026-11-02-fps-overlay-design.md`

## Global Constraints

- Target `net10.0` (Core, Tests) và `net10.0-windows` (app).
- App chạy với quyền Administrator (`requireAdministrator` trong `app.manifest`).
- Không chèn code vào game, không hook DirectX.
- Cửa sổ FPS: trong suốt, luôn trên cùng, chuột bấm xuyên qua, không hiện trên taskbar, không chiếm focus.
- Vị trí góc **trên bên trái** màn hình chính, chữ **màu xanh lá** có viền tối.
- FPS vẽ lại mỗi khung hình của WPF (`CompositionTarget.Rendering`); FPS tính trên cửa sổ trượt **0,5 giây** (hằng số `FpsCalculator.WindowSeconds`).
- Không có khung hình nào của tiến trình phía trước → hiện `--`.
- Menu khay: **Hiện FPS / Ẩn FPS**, **Thoát**.
- Hỏi người dùng trước khi cài .NET SDK hoặc tải PresentMon.

## Review Focus

1. **App chưa chạy quyền Admin hoặc thiếu `PresentMon.exe`** → người dùng phải thấy thông báo rõ ràng (MessageBox) rồi app thoát gọn, không treo im lặng. Test: Task 4, kiểm tra thủ công bằng cách đổi tên `PresentMon.exe`.
2. **App bị tắt đột ngột (End task)** → PresentMon không được chạy mồ côi. Dùng Job Object `KILL_ON_JOB_CLOSE` và `--stop_existing_session`. Test: Task 4, kiểm tra thủ công bằng Task Manager.
3. **Dòng CSV hỏng, cụt, hoặc giá trị `NA`** → bỏ qua dòng đó, không crash. Test: Task 2.
4. **Alt-tab ra desktop hoặc sang app khác** → hiện `--` ngay khi tiến trình phía trước không còn khung hình trong cửa sổ trượt. Test: Task 3.
5. **Game borderless đè lên cửa sổ FPS** → đặt lại topmost định kỳ (mỗi 1 giây). Test: Task 4, kiểm tra thủ công với game.

---

### Task 1: Môi trường và khung project

**Files:**
- Create: `FpsOverlay.sln`, `src/FpsOverlay.Core/FpsOverlay.Core.csproj`, `src/FpsOverlay/FpsOverlay.csproj`, `src/FpsOverlay/app.manifest`, `tests/FpsOverlay.Core.Tests/FpsOverlay.Core.Tests.csproj`, `.gitignore`

**Interfaces:**
- Produces: solution build được; app project tham chiếu Core; test project tham chiếu Core.

- [ ] **Step 1: Hỏi người dùng, rồi cài .NET 10 SDK**

Run: `winget install Microsoft.DotNet.SDK.10`
Expected: `dotnet --list-sdks` có dòng bắt đầu bằng `10.`

- [ ] **Step 2: Hỏi người dùng, rồi tải PresentMon**

Tải bản console x64 mới nhất từ `https://github.com/GameTechDev/PresentMon/releases` vào `tools/PresentMon.exe`. Chạy một lần (cửa sổ Admin) để xem tên cột:
Run: `tools\PresentMon.exe --output_stdout --no_console_stats --v1_metrics --stop_existing_session --session_name FpsOverlay --timed 2`
Expected: dòng đầu là header CSV có các cột `ProcessID` và `TimeInSeconds`. Nếu tên cột khác, ghi lại và báo vào Task 2.

- [ ] **Step 3: Tạo git repo, solution và ba project**

`git init`; `dotnet new sln`; `dotnet new classlib -o src/FpsOverlay.Core -f net10.0`; `dotnet new wpf -o src/FpsOverlay -f net10.0-windows`; `dotnet new xunit -o tests/FpsOverlay.Core.Tests -f net10.0`; thêm cả ba vào sln, thêm project reference. Trong `FpsOverlay.csproj`: `<UseWindowsForms>true</UseWindowsForms>`, `<ApplicationManifest>app.manifest</ApplicationManifest>`, và copy `..\..\tools\PresentMon.exe` ra output (`CopyToOutputDirectory=PreserveNewest`). `app.manifest` đặt `requestedExecutionLevel level="requireAdministrator"`. `.gitignore` từ `dotnet new gitignore`.

- [ ] **Step 4: Build**

Run: `dotnet build` rồi `dotnet test`
Expected: `Build succeeded`, test mẫu pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: scaffold FPS overlay solution"
```

---

### Task 2: Đọc CSV của PresentMon

**Files:**
- Create: `src/FpsOverlay.Core/FrameSample.cs`, `src/FpsOverlay.Core/PresentMonCsvParser.cs`
- Test: `tests/FpsOverlay.Core.Tests/PresentMonCsvParserTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct FrameSample(int ProcessId, double TimeSeconds);`
  - `public sealed class PresentMonCsvParser` với `bool TryReadHeader(string line)` (tìm vị trí cột `ProcessID` và `TimeInSeconds` theo tên, trả `false` nếu thiếu) và `bool TryParse(string line, out FrameSample sample)` (trả `false` nếu chưa đọc header, dòng thiếu cột, hoặc giá trị không parse được).
  - `public static Task ReadFramesAsync(TextReader reader, Action<FrameSample> onFrame, CancellationToken ct)`: dòng đầu là header, các dòng sau gọi `onFrame`; dòng không hợp lệ bị bỏ qua.

- [ ] **Step 1: Viết test**

```csharp
const string Header = "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents";

[Fact] public void Parses_frame_by_column_name()
{
    var p = new PresentMonCsvParser();
    Assert.True(p.TryReadHeader(Header));
    Assert.True(p.TryParse("game.exe,1234,0x1,10.5,16.6", out var s));
    Assert.Equal(new FrameSample(1234, 10.5), s);
}

[Fact] public void Header_without_required_columns_is_rejected()
    => Assert.False(new PresentMonCsvParser().TryReadHeader("Application,Foo"));

[Theory]
[InlineData("game.exe,1234")]            // cụt
[InlineData("game.exe,NA,0x1,NA,NA")]    // NA
[InlineData("")]
public void Bad_lines_are_skipped(string line)
{
    var p = new PresentMonCsvParser();
    p.TryReadHeader(Header);
    Assert.False(p.TryParse(line, out _));
}

[Fact] public async Task ReadFramesAsync_emits_valid_frames_only()
{
    var csv = $"{Header}\na.exe,1,0x1,1.0,16\nbroken\na.exe,1,0x1,1.016,16\n";
    var frames = new List<FrameSample>();
    await PresentMonCsvParser.ReadFramesAsync(new StringReader(csv), frames.Add, default);
    Assert.Equal([new(1, 1.0), new(1, 1.016)], frames);
}
```

Parse số bằng `CultureInfo.InvariantCulture` (máy tiếng Việt dùng dấu phẩy thập phân).

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test --filter PresentMonCsvParserTests`
Expected: FAIL (lỗi biên dịch, chưa có type).

- [ ] **Step 3: Viết `FrameSample` và `PresentMonCsvParser`**

- [ ] **Step 4: Chạy test, xác nhận pass**

Run: `dotnet test --filter PresentMonCsvParserTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: parse PresentMon CSV frames"
```

---

### Task 3: Tính FPS theo cửa sổ trượt

**Files:**
- Create: `src/FpsOverlay.Core/FpsCalculator.cs`
- Test: `tests/FpsOverlay.Core.Tests/FpsCalculatorTests.cs`

**Interfaces:**
- Consumes: `FrameSample` (Task 2).
- Produces: `public sealed class FpsCalculator` (thread-safe: `Add` gọi từ luồng đọc, `GetFps` từ luồng UI):
  - `public const double WindowSeconds = 0.5;`
  - `void Add(FrameSample frame)`: lưu khung hình, cập nhật `LatestTime` = thời điểm lớn nhất từng thấy (mọi tiến trình), bỏ khung hình cũ hơn `LatestTime - WindowSeconds`.
  - `double? GetFps(int processId)`: với các khung hình của `processId` trong `[LatestTime - WindowSeconds, LatestTime]`: nếu ít hơn 2 thì `null`; ngược lại `(n - 1) / (tLast - tFirst)`.
  - `public static string Format(double? fps)`: `null` → `"--"`, ngược lại số làm tròn, không phần thập phân (`"144"`).

"Bây giờ" là `LatestTime` của PresentMon, không phải đồng hồ máy, để cùng một hệ thời gian.

- [ ] **Step 1: Viết test**

```csharp
static void Feed(FpsCalculator c, int pid, double start, double interval, int count)
{ for (int i = 0; i < count; i++) c.Add(new(pid, start + i * interval)); }

[Fact] public void Steady_60fps()
{ var c = new FpsCalculator(); Feed(c, 1, 0, 1 / 60.0, 60); Assert.Equal(60, c.GetFps(1)!.Value, 1); }

[Fact] public void Only_counts_requested_process()
{
    var c = new FpsCalculator();
    Feed(c, 1, 0, 1 / 60.0, 60); Feed(c, 2, 0, 1 / 144.0, 144);
    Assert.Equal(60, c.GetFps(1)!.Value, 1);
    Assert.Equal(144, c.GetFps(2)!.Value, 1);
}

[Fact] public void Unknown_process_is_null() => Assert.Null(new FpsCalculator().GetFps(99));

[Fact] public void Process_that_stopped_presenting_becomes_null()   // Review Focus 4
{
    var c = new FpsCalculator();
    Feed(c, 1, 0, 1 / 60.0, 60);       // game tới t≈1.0
    Feed(c, 2, 1.0, 1 / 60.0, 60);     // desktop/app khác tới t≈2.0
    Assert.Null(c.GetFps(1));
}

[Fact] public void Format_values()
{ Assert.Equal("--", FpsCalculator.Format(null)); Assert.Equal("144", FpsCalculator.Format(143.6)); }
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test --filter FpsCalculatorTests`
Expected: FAIL (chưa có type).

- [ ] **Step 3: Viết `FpsCalculator`** (một `lock` quanh hàng đợi khung hình là đủ).

- [ ] **Step 4: Chạy toàn bộ test**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: rolling-window FPS calculator"
```

---

### Task 4: App WPF: PresentMon, cửa sổ FPS, khay hệ thống

**Files:**
- Create: `src/FpsOverlay/PresentMonRunner.cs`, `src/FpsOverlay/ForegroundProcess.cs`, `src/FpsOverlay/OverlayWindow.xaml(.cs)`, `src/FpsOverlay/TrayIcon.cs`
- Modify: `src/FpsOverlay/App.xaml(.cs)` (bỏ `StartupUri`, xoá `MainWindow.xaml` mẫu)

**Interfaces:**
- Consumes: `PresentMonCsvParser.ReadFramesAsync`, `FpsCalculator` (Task 2, 3).
- Produces (nội bộ app):
  - `sealed class PresentMonRunner : IDisposable` — `void Start(Action<FrameSample> onFrame)`: chạy `PresentMon.exe` (cạnh file exe của app) với tham số như Task 1 Step 2 nhưng bỏ `--timed`; `RedirectStandardOutput`, `CreateNoWindow`; gán process vào Job Object có `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`; đọc stdout bằng `ReadFramesAsync` trên luồng nền. Ném `FileNotFoundException` nếu thiếu exe. Có event `Exited` nếu PresentMon tự thoát. `Dispose` kill process.
  - `static class ForegroundProcess` — `int? GetId()`: `GetForegroundWindow` + `GetWindowThreadProcessId` (P/Invoke), `null` nếu không có cửa sổ.
  - `OverlayWindow`: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`, `ShowActivated=False`, vị trí `(8, 8)` màn hình chính. Một `TextBlock` chữ `LimeGreen`, cỡ 24, font đậm, viền tối bằng `DropShadowEffect` đen (`ShadowDepth=0`, `BlurRadius=4`). Trong `SourceInitialized` thêm `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`. Method `void SetText(string text)`. `DispatcherTimer` 1 giây gọi lại `SetWindowPos(HWND_TOPMOST, ..., SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)` (Review Focus 5).
  - `sealed class TrayIcon : IDisposable` — `NotifyIcon` với icon ứng dụng mặc định, tooltip `"FPS Overlay"`, menu chuột phải: mục bật/tắt (chữ đổi giữa `"Ẩn FPS"` / `"Hiện FPS"`) và `"Thoát"`. Event `ToggleRequested`, `ExitRequested`.
  - `App.OnStartup`: tạo calculator, runner, overlay, tray. Trong `CompositionTarget.Rendering`: `overlay.SetText(FpsCalculator.Format(pid is null ? null : calc.GetFps(pid.Value)))` với `pid = ForegroundProcess.GetId()`. Nếu `Start` ném lỗi hoặc PresentMon thoát bất thường → `MessageBox` giải thích (thiếu `PresentMon.exe` / cần chạy quyền Admin) rồi `Shutdown()` (Review Focus 1). `OnExit`: dispose tray và runner.

- [ ] **Step 1: Viết các class trên và nối dây trong `App`**

- [ ] **Step 2: Build và chạy toàn bộ test**

Run: `dotnet build` rồi `dotnet test`
Expected: `Build succeeded`, mọi test PASS.

- [ ] **Step 3: Kiểm tra thủ công trên máy**

Chạy `dotnet run --project src/FpsOverlay` (chấp nhận UAC). Kiểm tra:
- Ở desktop: góc trên trái hiện `--` màu xanh lá; biểu tượng có ở khay.
- Mở một game borderless: hiện FPS, số thay đổi liên tục; bấm chuột vào chỗ có chữ vẫn trúng game; con số không bị game đè sau vài phút.
- Alt-tab ra desktop: về `--`.
- Menu khay "Ẩn FPS" → chữ biến mất, mục đổi thành "Hiện FPS"; "Thoát" → app tắt, `PresentMon` biến khỏi Task Manager.
- End task app trong Task Manager → `PresentMon` cũng biến mất (Review Focus 2).
- Đổi tên `PresentMon.exe` trong thư mục output → mở app thấy thông báo lỗi rõ ràng rồi app thoát.

- [ ] **Step 4: Đóng gói**

Run: `dotnet publish src/FpsOverlay -c Release -r win-x64 --self-contained false -o publish`
Expected: thư mục `publish/` có `FpsOverlay.exe` và `PresentMon.exe`; mở `FpsOverlay.exe` chạy giống Step 3.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: WPF overlay with tray toggle"
```
