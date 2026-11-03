# FPS Overlay: tóm tắt để làm tiếp trên máy khác

> Dành cho cả người dùng lẫn Claude Code ở phiên mới. Claude: đọc hết file này, rồi đọc spec/plan được nhắc tới trước khi làm gì.
> Người dùng nói tiếng Việt: luôn trả lời bằng tiếng Việt.

## 1. Người dùng và mục tiêu
- Tự viết app hiện FPS khi chơi game vì muốn **tuỳ chỉnh theo ý mình** (thích overlay của MSI Afterburner, phong cách **Cyberpunk 2077**).
- Chơi game ở chế độ **borderless fullscreen**; game Steam (Assassin's Creed, Lies of P...) và **Game Pass**.
- Máy gốc: laptop **Ryzen 7 4800H**, **GTX 1650** + Radeon tích hợp, 1 màn hình, Windows 11.
- Muốn mang app sang máy khác (cả máy Intel) chỉ bằng **một file** tự cài.

## 2. Công nghệ và cấu trúc
- C# / **.NET 10**, WPF (+ WinForms `NotifyIcon` cho khay), xUnit.
- `src/FpsOverlay.Core`: logic thuần, có test (`tests/FpsOverlay.Core.Tests`, 196 test).
- `src/FpsOverlay`: app WPF. Chạy với quyền **Administrator** (`app.manifest` requireAdministrator) vì PresentMon cần ETW.
- Đo FPS: **PresentMon 2.6.0** (`tools/PresentMon.exe`), cửa sổ trượt 0,5 s, độ trễ cho phép 1,5 s (dữ liệu ETW về theo đợt ~1 s).
- Cảm biến: **LibreHardwareMonitorLib 0.9.6** (GPU/CPU tải và nhiệt), RAM qua `GlobalMemoryStatusEx`. Nhiệt độ CPU cần driver **PawnIO** (người dùng tự đồng ý cài).
- Font nhúng: Chakra Petch, JetBrains Mono (OFL).

## 3. Lịch sử các đợt (tất cả do người dùng chọn)
| Đợt | Nội dung | Trạng thái |
|---|---|---|
| 1 | Bản đầu: FPS xanh lá góc trái, `--` ở desktop, khay bật/tắt/thoát | merge vào master |
| UI | Overlay 1 dòng, 2 phong cách Cổ điển/Cyberpunk, cửa sổ cài đặt Cyberpunk, kích cỡ/độ mờ/màu/màu theo FPS, xem trước số mẫu, tiếng Việt/Anh, icon khay | merge vào master |
| 2 | Khởi động cùng Windows (Task Scheduler, quyền cao nhất), đo game Store/UWP (ApplicationFrameHost), dọn lỗi nhỏ. **Bỏ phím tắt** (người dùng chỉnh ở khay) | merge vào master |
| 3–4 | Cảnh báo PresentMon, tiết kiệm khi ẩn, 1% low, GPU/CPU/RAM/nhiệt độ, log mỗi buổi chơi, overlay theo màn hình có game, **một file tự cài vào Program Files**, PawnIO | nhánh **`feat/stats-install`**, CHƯA merge |

Hoãn theo ý người dùng: **biểu đồ FPS/frametime** (làm sau).

## 4. Tài liệu (đọc theo thứ tự)
- `docs/superpowers/specs/2026-11-02-fps-overlay-design.md` — bản đầu
- `docs/superpowers/specs/2026-11-02-ui-themes-settings-design.md` — đợt UI
- `docs/superpowers/specs/2026-11-03-stats-logging-install-design.md` — đợt 3–4 (kèm mục "Rủi ro còn lại (đã chấp nhận)")
- `docs/superpowers/plans/*.md` — kế hoạch tương ứng (đợt 3–4 có thêm Task 9b)

## 5. Trạng thái hiện tại (nhánh `feat/stats-install`)
- 22 commit trên master. `dotnet test`: 196/196 pass. `dotnet build src/FpsOverlay`: 0 cảnh báo.
- Review: mỗi task có review riêng + review toàn nhánh + một đợt sửa sau review cuối, đã qua.
- **Chưa chạy thật với quyền Admin** (cài vào Program Files, PawnIO, đọc cảm biến thật, gỡ cài đặt). Cần làm danh sách kiểm tay ở mục 9 trước khi merge.
- Việc còn chờ người dùng quyết: **merge `feat/stats-install` vào master** hay giữ nhánh (sau khi kiểm tay).

## 6. Cách làm việc với app trên máy mới
1. Cài **.NET 10 SDK** (x64).
2. Giải nén, mở terminal ở thư mục dự án:
   ```
   git status
   git checkout feat/stats-install
   dotnet test
   dotnet build src/FpsOverlay
   ```
3. Đóng gói ra **một file**: `dotnet publish src/FpsOverlay -c Release -o publish` → `publish\FpsOverlay.exe` (~158 MB, có sẵn .NET).
4. Chạy thử khi phát triển mà không cài: `FpsOverlay.exe --no-install` (chuột phải → Run as administrator). Chạy không tham số thì app đòi cài vào `C:\Program Files\FpsOverlay`.
5. Gỡ: Settings → Apps → FPS Overlay (giữ lại `%AppData%\FpsOverlay\settings.json` và `Documents\FpsOverlay\Logs`).

## 7. Những quyết định đã đưa ra (người dùng có thể đổi)
- **R1/R5** Cảnh báo PresentMon chỉ khi từ lúc (khởi động lại) PresentMon chưa có khung hình nào trong 10 s (desktop đứng yên có thể không vẽ gì).
- **R7** Lấy frametime cho log bằng `CopyFrameTimes(pid, ref cursor, list)` để đếm đúng một lần.
- **R8** Các thông số phụ dùng chữ nhỏ hơn FPS (14/18 so với 18/24) cho dòng gọn — đổi 2 hằng số `StatLabelFontSize`/`StatValueFontSize` nếu muốn bằng nhau.
- **R9** Bộ cài PawnIO chỉ được chạy nếu chữ ký hợp lệ **và** người ký `CN=namazso.eu`, `O=namazso` (kiểm theo tên, không theo mã chứng chỉ, để tác giả gia hạn chứng chỉ vẫn nhận).
- **R10** (người dùng chọn) File mang đi chứa sẵn bản app nhiều file; khi cài chép bản nhiều file vào Program Files để thư viện không nạp từ `%TEMP%`.
- **R11** Biến môi trường profiler của .NET (`CORECLR_*`) không chặn được từ trong app → ghi vào spec là rủi ro đã chấp nhận.
- Cảm biến vẫn đọc khi ẩn overlay nếu log đang bật (log cần cột nhiệt độ).
- Hỏi cài PawnIO bằng ô tick trong hộp thoại cài (mặc định trống); không bao giờ hỏi lúc tự khởi động cùng Windows.
- Bản cài setup không bao giờ ghi `settings.json` (tránh ghi đè cài đặt khi đọc lỗi).
- Chuyển thẳng sang game khác → đóng buổi log cũ, mở buổi mới. Alt-tab < 1 phút: cùng buổi, ô FPS trống.

## 8. Việc nên làm tiếp
1. **Kiểm tay** theo mục 9 trên máy thật (hoặc máy ảo), rồi quyết merge.
2. Sửa nhỏ về bảo mật (một dòng): đặt `COMSPEC` = `System32\cmd.exe` cho tiến trình cmd tự xoá khi gỡ cài đặt (`Installer.cs`, chỗ chạy `SelfDeleteCommand`).
3. Kiểm tra log khi `Documents` bị chuyển hướng/junction (hiện chỉ chặn một phần; tác động giới hạn ở việc tạo file mới, không ghi đè).
4. Các lỗi nhỏ đã hoãn (không chặn việc dùng): GPU tích hợp bận hơn ở desktop nên nhiệt GPU hiện `--`; dòng ghi chú PawnIO nháy ~1 s lúc mở app; ẩn overlay khi tắt log có thể đứng giao diện vài giây; file `.pdb` trong `publish\`.
5. Tính năng đã hoãn: **biểu đồ FPS/frametime**.

## 9. Danh sách kiểm tay (cần quyền Admin)
**Cài lần đầu và PawnIO**
1. Chạy `publish\FpsOverlay.exe` trên máy chưa cài, để tắt ô "Cài PawnIO".
   - Không tải gì.
   - `C:\Program Files\FpsOverlay` có đủ cây nhiều file và `payload.sha256`. Không còn thư mục `.staging-*`.
   - Có lối tắt Start Menu và mục trong Settings → Apps.
   - `%AppData%\FpsOverlay\settings.json` có `"PawnIoOffered": true`.
   - Cập nhật lần sau không hiện ô PawnIO.
2. Cài lần đầu, bật ô PawnIO.
   - Hộp thoại hiện "Đang tải và cài PawnIO..." và không đóng được. Có thông báo kết quả rồi app chạy.
   - Có nhiệt độ CPU, hoặc báo khởi động lại khi mã thoát là 3010.
   - `C:\Program Files\FpsOverlay\Temp` rỗng.
   - Settings → Apps có PawnIO. Lần cập nhật sau ô PawnIO ẩn, nút trong cài đặt cũng ẩn.
3. Bật ô PawnIO nhưng rút mạng: báo "Không tải được PawnIO...", app vẫn cài xong, nhiệt độ hiện `--`.
4. Nút "Cài PawnIO" trong cài đặt của bản cài chưa có PawnIO: giao diện không đứng, dòng trạng thái báo tiến độ rồi kết quả. Ghi chú nhiệt độ CPU biến mất trong khoảng 1 giây sau khi thành công.
5. Nút "Cài PawnIO" ở bản `--no-install`: tạo `%ProgramData%\FpsOverlay\Temp`. Chạy `icacls` trên thư mục đó: chủ sở hữu là Administrators, chỉ Administrators và SYSTEM có quyền, không có quyền thừa hưởng.
6. Đổi ngôn ngữ trong hộp thoại cài: nhãn PawnIO đổi theo.
7. Chạy lại cùng file mang đi: mở luôn bản đã cài, không hiện hộp thoại.
8. Cập nhật đè bản single-file cũ (bản trước 9b): hộp thoại báo Cập nhật.
   - `Temp\` vẫn còn.
   - Exe giờ là apphost nhỏ, thư viện native nằm cạnh exe.
   - File thừa `FpsOverlay.exe.*.new` đã bị xoá.
9. Cập nhật khi bản cài đang chạy: báo "thoát FPS Overlay đang chạy" như trước.
10. Chép `FpsOverlay.exe` của bản cài ra chỗ khác rồi chạy, hoặc chạy exe của `dotnet build` không có tham số: báo "không mang gói cài", không cài gì.
11. Cài dở dang: cài xong thì xoá lối tắt và khoá `HKLM\...\Uninstall\FpsOverlay`, rồi mở `C:\Program Files\FpsOverlay\FpsOverlay.exe`. Cả hai được tạo lại.

**Overlay**
12. Overlay hiện đủ mục: FPS, 1% low, GPU tải và nhiệt, CPU tải và nhiệt, RAM.
    - Mỗi công tắc trong cài đặt bật/tắt đúng mục của nó và thấy ngay.
    - Màu "nóng" đổi đúng ngưỡng.
    - Overlay không bắt chuột, không chiếm focus, không chớp `--`.
13. Màn hình ngoài: chơi game trên màn hình phụ thì overlay theo màn hình có game.
14. Treo PresentMon hoặc để nó không có dữ liệu: menu khay hiện "⚠ PresentMon không gửi dữ liệu. Thử thoát và mở lại app." (en: "Try exiting and reopening the app.").

**Log**
15. Chơi game toàn màn hình hơn 10 giây rồi thoát game.
    - `Documents\FpsOverlay\Logs` có `<game>_yyyy-MM-dd_HH-mm.csv` (một dòng mỗi giây) và `..._summary.txt`.
    - Thời lượng và FPS trong tóm tắt hợp lý.
16. Alt-tab ra ngoài dưới 1 phút rồi quay lại: vẫn cùng một file, cùng một buổi. Các giây ở ngoài có ô `Fps` và `OnePercentLow` **trống**, dù game vẫn vẽ ở nền. FPS trung bình trong tóm tắt không bị các giây đó kéo lệch.
17. Alt-tab quá 1 phút: buổi kết thúc. Quay lại game hơn 10 giây thì có file mới.
18. Ẩn FPS:
    - Khi log bật: PresentMon vẫn còn trong Task Manager và log vẫn ghi.
    - Khi log tắt: PresentMon hết trong Task Manager, cảm biến ngừng đọc.
19. Biến `Documents\FpsOverlay\Logs` thành junction (`mklink /J`) trỏ chỗ khác rồi chơi hơn 10 giây: không ghi file nào qua junction (ghi vết Trace). Xoá junction thì log ghi lại bình thường.
20. GPU báo nhiệt 0 (vd. GPU rời đang ngủ): overlay và log hiện trống/`--`, không hiện 0.

**Khởi động cùng Windows**
21. Bật khởi động cùng Windows từ bản cài, đăng xuất rồi đăng nhập.
    - App tự chạy, không hiện UAC, không hỏi hay tải PawnIO, PresentMon chạy.
    - Không tạo hay dùng `%TEMP%\.net\FpsOverlay\`: kiểm bằng Process Monitor, hoặc xoá thư mục đó trước rồi xem có xuất hiện lại không.
22. Mở từ Start Menu: cũng không dùng `%TEMP%\.net`.
23. Đặt `DOTNET_STARTUP_HOOKS` trỏ tới một dll bất kỳ trong `HKCU\Environment`, đăng nhập lại: app vẫn chạy bình thường, dll không được nạp. Xoá biến sau khi thử.

**Gỡ cài đặt**
24. Gỡ từ Settings → Apps, bản cài không chạy.
    - Hỏi xác nhận, rồi hiện "Đã gỡ".
    - Chỉ sau khi bấm OK (app thoát), trong vài giây thư mục `C:\Program Files\FpsOverlay` (cả `Temp` và thư mục con) biến mất, **rồi** mục trong Settings → Apps mới mất.
    - Tác vụ khởi động và lối tắt đã bị xoá. `%AppData%\FpsOverlay\settings.json` và `Documents\FpsOverlay\Logs` vẫn còn.
25. Để hộp "Đã gỡ" mở hơn 1–2 phút rồi mới bấm OK: thư mục vẫn bị xoá sau khi thoát (cmd chỉ bắt đầu sau hộp thông báo).
26. Gỡ khi một file trong thư mục cài bị khoá (vd. mở `PresentMon.exe` từ phiên khác): rmdir không xong trong khoảng 90 giây thì thư mục còn và **mục gỡ trong Settings → Apps vẫn còn** để gỡ lại.
27. Gỡ khi bản cài đang chạy ở khay: báo thoát nó trước, không gỡ gì.
28. Bản `--no-install` vẫn bung PresentMon cạnh exe và chạy được.

