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
- `src/FpsOverlay.Core`: logic thuần, có test (`tests/FpsOverlay.Core.Tests`, 256 test).
- `src/FpsOverlay`: app WPF. Chạy với quyền **Administrator** (`app.manifest` requireAdministrator) vì PresentMon cần ETW.
- Đo FPS: **PresentMon 2.6.0** (`tools/PresentMon.exe`), cửa sổ trượt 0,5 s, độ trễ cho phép 1,5 s (dữ liệu ETW về theo đợt ~1 s).
- Cảm biến: **LibreHardwareMonitorLib 0.9.6** (GPU/CPU tải và nhiệt, VRAM, xung nhân, công suất, quạt GPU), RAM qua `GlobalMemoryStatusEx`. Nhiệt độ CPU cần driver **PawnIO** (người dùng tự đồng ý cài).
- Font nhúng: Chakra Petch, JetBrains Mono (OFL).

## 3. Lịch sử các đợt (tất cả do người dùng chọn)
| Đợt | Nội dung | Trạng thái |
|---|---|---|
| 1 | Bản đầu: FPS xanh lá góc trái, `--` ở desktop, khay bật/tắt/thoát | merge vào master |
| UI | Overlay 1 dòng, 2 phong cách Cổ điển/Cyberpunk, cửa sổ cài đặt Cyberpunk, kích cỡ/độ mờ/màu/màu theo FPS, xem trước số mẫu, tiếng Việt/Anh, icon khay | merge vào master |
| 2 | Khởi động cùng Windows (Task Scheduler, quyền cao nhất), đo game Store/UWP (ApplicationFrameHost), dọn lỗi nhỏ. **Bỏ phím tắt** (người dùng chỉnh ở khay) | merge vào master |
| 3–4 | Cảnh báo PresentMon, tiết kiệm khi ẩn, 1% low, GPU/CPU/RAM/nhiệt độ, log mỗi buổi chơi, overlay theo màn hình có game, **một file tự cài vào Program Files**, PawnIO | merge vào master |
| 5 | Phiên bản **1.1.0**: bảng kiểu + mô hình hàng (refactor); VRAM/CLK/PWR/FAN (mặc định tắt, log thêm 4 cột); cảnh báo quá nhiệt (ngưỡng GPU 85/CPU 90, N giây mặc định 10, nháy bật, bíp tắt, nút "Thử cảnh báo", mục `// CẢNH BÁO`); hai kiểu mới **Neon tím** (Orbitron) và **Terminal xanh** (VT323); bố cục dọc; mục khay "Di chuyển overlay" (kéo thả; Enter/bấm đúp lưu, Esc huỷ, trả focus về game); cửa sổ cài đặt cuộn được | merge vào master, chưa kiểm tay |

Hoãn theo ý người dùng: **biểu đồ FPS/frametime** (làm sau).

## 4. Tài liệu (đọc theo thứ tự)
- `docs/superpowers/specs/2026-11-02-fps-overlay-design.md` — bản đầu
- `docs/superpowers/specs/2026-11-02-ui-themes-settings-design.md` — đợt UI
- `docs/superpowers/specs/2026-11-03-stats-logging-install-design.md` — đợt 3–4 (kèm mục "Rủi ro còn lại (đã chấp nhận)")
- `docs/superpowers/specs/2026-11-03-gpu-alerts-themes-layout-design.md` — đợt 5
- `docs/superpowers/plans/*.md` — kế hoạch tương ứng (đợt 3–4 có thêm Task 9b; đợt 5: `2026-11-03-gpu-alerts-themes-layout.md`)

## 5. Trạng thái hiện tại
- **Đợt 5 (1.1.0) đã merge vào master** (nhánh `feat/gpu-alerts-themes-layout` đã xoá; người dùng chọn merge trước khi kiểm tay, lỗi tìm thấy sửa trên nhánh mới từ master). Code xong, `dotnet test`: 256/256 pass, `dotnet build src/FpsOverlay`: 0 cảnh báo, `dotnet publish` chạy được. Chưa kiểm tay (mục 9, phần đợt 5) và chưa chạy thật với quyền Admin.
- Master (đợt 1–4): đã merge vào master (nhánh `feat/stats-install` đã xoá). `dotnet test` lúc đó: 196/196 pass.
- Review (đợt 1–4): mỗi task có review riêng + review toàn nhánh + một đợt sửa sau review cuối, đã qua.
- **Chưa chạy thật với quyền Admin** (cài vào Program Files, PawnIO, đọc cảm biến thật, gỡ cài đặt). Cần làm danh sách kiểm tay ở mục 9 trước khi dùng thật.
- Người dùng đã chọn merge trước khi kiểm tay (đợt 1–4); nếu kiểm tay phát hiện lỗi thì sửa trên một nhánh mới từ master.

## 6. Cách làm việc với app trên máy mới
1. Cài **.NET 10 SDK** (x64).
2. Giải nén, mở terminal ở thư mục dự án:
   ```
   git status
   dotnet test
   dotnet build src/FpsOverlay
   ```
3. Đóng gói ra **một file**: `dotnet publish src/FpsOverlay -c Release -o publish` → `publish\FpsOverlay.exe` (~158 MB, có sẵn .NET; bản 1.1.0 đo được 158,4 MB).
4. Chạy thử khi phát triển mà không cài: `FpsOverlay.exe --no-install` (chuột phải → Run as administrator). Chạy không tham số thì app đòi cài vào `C:\Program Files\FpsOverlay`.
5. Gỡ: Settings → Apps → FPS Overlay (giữ lại `%AppData%\FpsOverlay\settings.json` và `Documents\FpsOverlay\Logs`).

## 7. Những quyết định đã đưa ra (người dùng có thể đổi)
- **R1/R5** Cảnh báo PresentMon chỉ khi từ lúc (khởi động lại) PresentMon chưa có khung hình nào trong 10 s (desktop đứng yên có thể không vẽ gì).
- **R7** Lấy frametime cho log bằng `CopyFrameTimes(pid, ref cursor, list)` để đếm đúng một lần.
- **R8** Các thông số phụ dùng chữ nhỏ hơn FPS (14/18 so với 18/24) cho dòng gọn — đổi 2 hằng số `StatLabelFontSize`/`StatValueFontSize` nếu muốn bằng nhau.
- **R9** Bộ cài PawnIO chỉ được chạy nếu chữ ký hợp lệ **và** người ký `CN=namazso.eu`, `O=namazso` (kiểm theo tên, không theo mã chứng chỉ, để tác giả gia hạn chứng chỉ vẫn nhận).
- **R10** (người dùng chọn) File mang đi chứa sẵn bản app nhiều file; khi cài chép bản nhiều file vào Program Files để thư viện không nạp từ `%TEMP%`.
- **R11** Biến môi trường profiler của .NET (`CORECLR_*`) không chặn được từ trong app → ghi vào spec là rủi ro đã chấp nhận.
- **Đợt 5, R5** Góc và khoảng cách lưu đo tới **khung nhìn thấy được** của overlay; kiểu Neon có vùng trong suốt 8 DIP chứa ánh sáng nên được trừ ra, để đổi kiểu không làm overlay nhảy chỗ.
- **Đợt 5** Ánh sáng (glow) của Neon theo màu chữ: đổi màu nhãn thì ánh sáng đổi theo.
- **Đợt 5** Cảnh báo nhiệt kết thúc khi đang xem trước, khi overlay ẩn, hoặc khi tắt hiện nhiệt độ tương ứng.
- **Đợt 5** VRAM không có ô đơn vị riêng. Ở bố cục dọc, CLK/PWR/FAN chưa có giá trị hiện `-- MHz`, `-- W`, `-- RPM`.
- Cảm biến vẫn đọc khi ẩn overlay nếu log đang bật (log cần cột nhiệt độ).
- Hỏi cài PawnIO bằng ô tick trong hộp thoại cài (mặc định trống); không bao giờ hỏi lúc tự khởi động cùng Windows.
- Bản cài setup không bao giờ ghi `settings.json` (tránh ghi đè cài đặt khi đọc lỗi).
- Chuyển thẳng sang game khác → đóng buổi log cũ, mở buổi mới. Alt-tab < 1 phút: cùng buổi, ô FPS trống.

## 8. Việc nên làm tiếp
1. **Kiểm tay đợt 5** (mục 9, phần "Đợt 5") trên máy thật; lỗi tìm thấy sửa trên một nhánh mới từ master. Sau đó kiểm tay phần còn lại của mục 9 nếu chưa làm.
2. ~~Sửa nhỏ về bảo mật: đặt `COMSPEC` = `System32\cmd.exe` cho tiến trình cmd tự xoá khi gỡ cài đặt~~ — đã làm và merge vào master (cần kiểm tay mục 24–26).
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

**Đợt 5 (1.1.0)**
29. Cổ điển và Cyberpunk ở bố cục ngang trông như bản 1.0.0.
30. VRAM/CLK/PWR/FAN đúng trên GTX 1650 khi chơi. Ở desktop là `--` hoặc số của GPU tích hợp. FAN là `--` nếu máy không báo.
31. Log mới có 4 cột cuối (VRAM/CLK/PWR/FAN).
32. Cảnh báo:
    - ngưỡng thấp + 0 giây: nháy và bíp một lần;
    - nhiệt dao động sát ngưỡng không bíp lại;
    - tắt hiện nhiệt: hết nháy.
33. Nút "Thử cảnh báo" chạy được khi đã tắt hai số nhiệt.
34. Neon tím và Terminal xanh ở cả hai bố cục, so với bản vẽ thử. Đổi màu nhãn thì ánh sáng đổi theo.
35. Bố cục dọc: tắt/bật từng mục; chỉ bật nhiệt; Scale 50% và 300%.
36. Kéo thả: Enter lưu, Esc huỷ, bấm đúp lưu, bấm lại mục khay "Di chuyển overlay" kết thúc.
37. Kết thúc chế độ di chuyển bằng cách ẩn overlay hoặc mở cài đặt: overlay lại bấm xuyên, không chiếm focus.
38. Sau khi kéo: focus về game. Overlay theo game sang màn hình phụ với cùng góc và khoảng cách.
39. Cập nhật đè bản 1.0.0 bằng file mang đi 1.1.0: hộp thoại báo Cập nhật; cài đặt cũ (màu đã chỉnh) còn nguyên.
40. Xem Trace (DebugView hoặc Trace listener) trên GTX 1650: tên cảm biến GPU mà VRAM/CLK/PWR/FAN đọc từ đó.
41. Terminal xanh: các đường quét (scanline) đều ở Windows scaling 125%/150% và Scale khác 100%.
42. Terminal xanh với mọi hàng bật: CPU/GPU của app vẫn thấp.
43. Đổi Neon ↔ kiểu khác: overlay không nhảy thấy được ở góc dưới/phải.
44. Cửa sổ cài đặt trên màn hình thấp: cuộn được, nút "Khôi phục mặc định" / "Xong" vẫn hiện.
