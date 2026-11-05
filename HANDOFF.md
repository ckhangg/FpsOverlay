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
- `src/FpsOverlay.Core`: logic thuần, có test (`tests/FpsOverlay.Core.Tests`, 350 test).
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
| 5 | Phiên bản **1.1.0**: bảng kiểu + mô hình hàng (refactor); VRAM/CLK/PWR/FAN (mặc định tắt, log thêm 4 cột); cảnh báo quá nhiệt (ngưỡng GPU 85/CPU 90, N giây mặc định 10, nháy bật, bíp tắt, nút "Thử cảnh báo", mục `// CẢNH BÁO`); hai kiểu mới **Neon tím** (Orbitron) và **Terminal xanh** (VT323); bố cục dọc; mục khay "Di chuyển overlay" (kéo thả; Enter/bấm đúp lưu, Esc huỷ, trả focus về game); cửa sổ cài đặt cuộn được | merge vào master, người dùng đã kiểm tay OK (2026-11-03) |
| 6 | Phiên bản **1.2.0**: "Chỉ hiện khi chơi game" (mặc định bật; overlay hiện khi cửa sổ phía trước phủ kín màn hình, hoặc exe nằm trong `\steamapps\common\` hay `\XboxGames\`, hoặc nằm trong "Luôn hiện"; "Không bao giờ hiện" thắng; kiểm mỗi 250 ms); mục khay "Luôn hiện cho …"/"Không bao giờ hiện cho …"; cài đặt: danh sách thông số thu gọn sau nút "Chọn thông số hiển thị ▾" kèm "Đang hiện N/10", danh sách ứng dụng thu gọn có nút Xoá | merge vào master, chưa kiểm tay |
| 7 | Phiên bản **1.3.0**: cài đặt có các mục màu/FPS/cảnh báo thu gọn được và nhãn phiên bản; kho GitHub công khai `ckhangg/FpsOverlay` (README song ngữ, giấy phép MIT); `scripts/release.ps1` phát hành lên GitHub Releases; cài bằng một dòng `curl` | merge vào master, đã phát hành v1.3.0 trên GitHub, chưa kiểm tay |
| 8 | Phiên bản **1.4.0**: cửa sổ cài đặt/cài app/cập nhật theo 4 chủ đề overlay (Cổ điển, Cyberpunk, Neon tím, Terminal xanh) và đổi ngay khi chọn; cột nhãn rộng hơn, nhãn tự xuống dòng; thanh cuộn theo chủ đề; kiểm tra cập nhật từ GitHub Releases khi mở app (im lặng nếu lỗi), nút "Kiểm tra cập nhật" trong cài đặt và khay; hộp thoại CẬP NHẬT / BỎ QUA BẢN NÀY / ĐỂ SAU; `--autostart` chỉ hiện thông báo khay + một dòng đầu menu khay; tải về `<InstallDir>\Temp`, kiểm SHA-256, bàn giao `--update-after <PID>`, cập nhật tự động không hỏi; tác vụ khởi động nay truyền `--autostart` | merge vào master, phát hành v1.4.0 trên GitHub; người dùng đã kiểm tay OK (2026-11-05), kể cả cập nhật thật 1.4.0 → 1.4.1 qua nút cập nhật |

Hoãn theo ý người dùng: **biểu đồ FPS/frametime** (làm sau).

## 4. Tài liệu (đọc theo thứ tự)
- `docs/superpowers/specs/2026-11-02-fps-overlay-design.md` — bản đầu
- `docs/superpowers/specs/2026-11-02-ui-themes-settings-design.md` — đợt UI
- `docs/superpowers/specs/2026-11-03-stats-logging-install-design.md` — đợt 3–4 (kèm mục "Rủi ro còn lại (đã chấp nhận)")
- `docs/superpowers/specs/2026-11-03-gpu-alerts-themes-layout-design.md` — đợt 5
- `docs/superpowers/specs/2026-11-03-game-only-overlay-design.md` — đợt 6
- `docs/superpowers/specs/2026-11-03-collapse-version-install-design.md` — đợt 7
- `docs/superpowers/specs/2026-11-04-themed-ui-updater-design.md` — đợt 8 (chủ đề cho cửa sổ + bộ cập nhật)
- `docs/superpowers/plans/*.md` — kế hoạch tương ứng (đợt 3–4 có thêm Task 9b; đợt 5: `2026-11-03-gpu-alerts-themes-layout.md`; đợt 6: `2026-11-03-game-only-overlay.md`; đợt 7: `2026-11-03-collapse-version-install.md`; đợt 8: `2026-11-04-themed-ui-updater.md`)

## 5. Trạng thái hiện tại
- **Đợt 8 (1.4.0) đã merge vào master và phát hành tại https://github.com/ckhangg/FpsOverlay/releases/tag/v1.4.0; người dùng đã cài 1.4.0 và kiểm tay OK (2026-11-05): đổi 4 chủ đề, điều khiển Terminal, nút kiểm tra cập nhật, khởi động cùng Windows (đã hết lỗi schtasks), cài PawnIO, gỡ cài đặt.** Đã phát hành bản thử v1.4.1 (chỉ đổi số phiên bản) và người dùng cập nhật 1.4.0 → 1.4.1 bằng nút trong app: ổn (nhánh `feat/themed-ui-updater` đã xoá). `dotnet test`: 365/365 pass, `dotnet build src/FpsOverlay`: 0 cảnh báo. Cần kiểm tay mục "Đợt 8" ở mục 9. Người dùng đang ở bản 1.3.0 (chưa có bộ cập nhật) phải chạy lại lệnh cài một dòng một lần để lên 1.4.0. Thử cập nhật thật cần phát hành một bản thử (vd. 1.4.1): chỉ làm khi người dùng đồng ý.
- **Đợt 7 (1.3.0) đã merge vào master và phát hành tại https://github.com/ckhangg/FpsOverlay/releases/tag/v1.3.0, chưa kiểm tay.** Lịch sử git đã đổi tác giả thành `CKhang` (noreply) và xoá danh tính công ty cũ khỏi nội dung; bản sao lưu lịch sử cũ: `%USERPROFILE%\Downloads\FPS-history-backup.bundle` (chỉ trên máy, không đẩy lên). `dotnet test`: 292/292 pass, `dotnet build src/FpsOverlay`: 0 cảnh báo. Cần kiểm tay mục "Đợt 7" ở mục 9.
- **Kho GitHub:** https://github.com/ckhangg/FpsOverlay (công khai, tài khoản `ckhangg`; tác giả mọi commit là `CKhang <39177627+ckhangg@users.noreply.github.com>`). Bản sao lưu lịch sử cũ: `%USERPROFILE%\Downloads\FPS-history-backup.bundle`.
- **Phát hành bản mới:** đổi `<Version>` trong `src/FpsOverlay/FpsOverlay.csproj`, merge vào `master`, rồi chạy `.\scripts\release.ps1` (cần `gh` đã đăng nhập). Script kiểm cây sạch + đang ở master + thẻ chưa có, chạy test, publish, tạo `publish\FpsOverlay.exe.sha256`, đẩy thẻ `vX.Y.Z` rồi tạo release kèm hai file.
- **Lệnh cài một dòng** (cmd, không phải PowerShell; chạy lại để cập nhật):
  ```
  curl -fL -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
  ```
- **Đợt 6 (1.2.0) đã merge vào master, chưa kiểm tay** (nhánh `feat/game-only-overlay` đã xoá; người dùng chọn merge trước khi kiểm tay, lỗi tìm thấy sửa trên nhánh mới từ master). `dotnet test`: 287/287 pass, `dotnet build src/FpsOverlay`: 0 cảnh báo. Cần kiểm tay mục "Đợt 6" ở mục 9.
- **Đợt 5 (1.1.0) đã merge vào master** (nhánh `feat/gpu-alerts-themes-layout` đã xoá; người dùng chọn merge trước khi kiểm tay, lỗi tìm thấy sửa trên nhánh mới từ master). Code xong, `dotnet test`: 256/256 pass, `dotnet build src/FpsOverlay`: 0 cảnh báo, `dotnet publish` chạy được. Người dùng đã cài bản 1.1.0 và kiểm tay đợt 5 trên máy thật: OK (2026-11-03).
- Master (đợt 1–4): đã merge vào master (nhánh `feat/stats-install` đã xoá). `dotnet test` lúc đó: 196/196 pass.
- Review (đợt 1–4): mỗi task có review riêng + review toàn nhánh + một đợt sửa sau review cuối, đã qua.
- **Chưa chạy thật với quyền Admin** (cài vào Program Files, PawnIO, đọc cảm biến thật, gỡ cài đặt). Cần làm danh sách kiểm tay ở mục 9 trước khi dùng thật.
- Người dùng đã chọn merge trước khi kiểm tay (đợt 1–4); nếu kiểm tay phát hiện lỗi thì sửa trên một nhánh mới từ master.

## 6. Cách làm việc với app trên máy mới
1. Cài **.NET 10 SDK** (x64).
2. Clone `https://github.com/ckhangg/FpsOverlay` (hoặc giải nén bản sao), mở terminal ở thư mục dự án:
   ```
   git status
   dotnet test
   dotnet build src/FpsOverlay
   ```
3. Phát hành bản mới: xem mục 5 (`.\scripts\release.ps1`).
   Đóng gói ra **một file**: `dotnet publish src/FpsOverlay -c Release -o publish` → `publish\FpsOverlay.exe` (~158 MB, có sẵn .NET; bản 1.1.0 đo được 158,4 MB).
4. Chạy thử khi phát triển mà không cài: `FpsOverlay.exe --no-install` (chuột phải → Run as administrator). Chạy không tham số thì app đòi cài vào `C:\Program Files\FpsOverlay`.
5. Gỡ: Settings → Apps → FPS Overlay (giữ lại `%AppData%\FpsOverlay\settings.json` và `Documents\FpsOverlay\Logs`).

## 7. Những quyết định đã đưa ra (người dùng có thể đổi)
- **R1/R5** Cảnh báo PresentMon chỉ khi từ lúc (khởi động lại) PresentMon chưa có khung hình nào trong 10 s (desktop đứng yên có thể không vẽ gì).
- **R7** Lấy frametime cho log bằng `CopyFrameTimes(pid, ref cursor, list)` để đếm đúng một lần.
- **R8** Các thông số phụ dùng chữ nhỏ hơn FPS (14/18 so với 18/24) cho dòng gọn — đổi 2 hằng số `StatLabelFontSize`/`StatValueFontSize` nếu muốn bằng nhau.
- **R9** Bộ cài PawnIO chỉ được chạy nếu chữ ký hợp lệ **và** người ký `CN=namazso.eu`, `O=namazso` (kiểm theo tên, không theo mã chứng chỉ, để tác giả gia hạn chứng chỉ vẫn nhận).
- **R10** (người dùng chọn) File mang đi chứa sẵn bản app nhiều file; khi cài chép bản nhiều file vào Program Files để thư viện không nạp từ `%TEMP%`.
- **R11** Biến môi trường profiler của .NET (`CORECLR_*`) không chặn được từ trong app → ghi vào spec là rủi ro đã chấp nhận.
- **Đợt 6** Cửa sổ của chính app và thanh taskbar/khay ẩn/Alt-Tab (cửa sổ phía trước) giữ nguyên quyết định hiện/ẩn trước đó. Nhờ vậy mở menu khay giữa game không làm overlay ẩn, và mục khay "Luôn hiện cho …" nhắm đúng game chứ không phải `explorer.exe`.
- **Đợt 6** "Khôi phục mặc định" giữ nguyên hai danh sách ứng dụng.
- **Đợt 6** "Không bao giờ hiện" thắng mọi điều kiện khác (toàn màn hình, thư mục game, "Luôn hiện").
- **Đợt 6** `\WindowsApps\` cố ý **không** tính là thư mục game (sẽ làm Notepad/Photos/Calculator thành game). Game Store cũ chạy cửa sổ cần bấm "Luôn hiện" một lần.
- **Đợt 6** Overlay tự ẩn mà log tắt thì PresentMon dừng; vào game thì số có thể là `--` trong 1–3 giây đầu.
- **Đợt 5, R5** Góc và khoảng cách lưu đo tới **khung nhìn thấy được** của overlay; kiểu Neon có vùng trong suốt 8 DIP chứa ánh sáng nên được trừ ra, để đổi kiểu không làm overlay nhảy chỗ.
- **Đợt 5** Ánh sáng (glow) của Neon theo màu chữ: đổi màu nhãn thì ánh sáng đổi theo.
- **Đợt 5** Cảnh báo nhiệt kết thúc khi đang xem trước, khi overlay ẩn, hoặc khi tắt hiện nhiệt độ tương ứng.
- **Đợt 5** VRAM không có ô đơn vị riêng. Ở bố cục dọc, CLK/PWR/FAN chưa có giá trị hiện `-- MHz`, `-- W`, `-- RPM`.
- Cảm biến vẫn đọc khi ẩn overlay nếu log đang bật (log cần cột nhiệt độ).
- Hỏi cài PawnIO bằng ô tick trong hộp thoại cài (mặc định trống); không bao giờ hỏi lúc tự khởi động cùng Windows.
- Bản cài setup không bao giờ ghi `settings.json` (tránh ghi đè cài đặt khi đọc lỗi).
- Chuyển thẳng sang game khác → đóng buổi log cũ, mở buổi mới. Alt-tab < 1 phút: cùng buổi, ô FPS trống.

- **Đợt 8** Tiêu đề cửa sổ của Neon tím dùng Chakra Petch vì Orbitron không có tiếng Việt (overlay Neon vẫn dùng Orbitron cho số/nhãn Latin).
- **Đợt 8** Khung cửa sổ là một kiểu trong suốt cố định; mỗi chủ đề có `Ui.WindowTemplate` riêng vẽ nền/viền/hiệu ứng. Đổi chủ đề trong cài đặt áp dụng ngay cho mọi cửa sổ đang mở.
- **Đợt 8** Nút trong cửa sổ viết HOA qua `Loc` (vd. CẬP NHẬT, ĐỂ SAU); mục menu khay giữ chữ thường như câu.
- **Đợt 8** Bấm "Kiểm tra cập nhật" khi lần kiểm tra lúc mở app còn đang chạy thì nhập vào lần đó (chỉ một kết quả, không gọi mạng hai lần).
- **Đợt 8** `--autostart` (tác vụ khởi động cùng Windows) không bao giờ bật hộp thoại: chỉ thông báo bóng khay và một dòng đầu menu khay. Tác vụ cũ của 1.3.0 được sửa để thêm `--autostart`: bản cài tự sửa qua `AutostartService.QueryAndRepair` (gọi từ `RefreshAsync` lúc khởi động, mở menu khay, mở cài đặt), và bản setup sửa qua `Retarget` khi cài/cập nhật (không phải `RepairEntries`, hàm đó chỉ tạo lại lối tắt và mục gỡ cài đặt).
- **Đợt 8** `SkippedVersion` trong `settings.json`: "BỎ QUA BẢN NÀY" nhớ phiên bản; kiểm tự động không nhắc lại đúng bản đó, kiểm bằng tay vẫn báo.
- **Đợt 8** Tải về vào `<InstallDir>\Temp` (chỉ Admin ghi được; bản `--no-install` dùng `%ProgramData%\FpsOverlay\Temp` có ACL Admin). Chỉ nhận HTTPS tới github.com, objects.githubusercontent.com, release-assets.githubusercontent.com (kiểm từng bước chuyển hướng, tối đa 5), tối đa 400 MB, bắt buộc khớp SHA-256. Khi khởi động bản đã cài, xoá `Temp\*.exe` và `Temp\extract` cũ hơn 1 giờ.
- **Đợt 8** Bàn giao: app cũ chạy exe mới với `--update-after <PID>` rồi thoát; exe mới chỉ chờ (tối đa 30 s) nếu PID đúng là bản cài, rồi cài đè không hỏi và mở bản đã cài.
- **Đợt 8** Cập nhật tự động thất bại vẫn mở bản đã cài (nếu còn) để người dùng không mất overlay.
- **Đợt 8** App chạy từ tác vụ khởi động (Task Scheduler) nằm trong Job Object của tác vụ: exe cập nhật (và bản cài nó mở lại) được chạy bằng `CreateProcess` + `CREATE_BREAKAWAY_FROM_JOB` để không bị tắt cùng tác vụ; job không cho tách thì chạy như cũ (ghi Trace) và có thể bị tắt, xem mục 84. Không trong job thì đường chạy không đổi.
- **Đợt 8** Lúc mở app mà game đang ở phía trước (chỉ-hiện-khi-chơi bật, overlay đang hiện, không xem trước): không bật hộp thoại cập nhật, chỉ báo khay (balloon + dòng đầu menu). Ghi chú phát hành hiện chữ thường (bỏ Markdown đơn giản, `ReleaseNotes.ToPlainText`).
- **Đợt 8** Biến `DOTNET_BUNDLE_EXTRACT_BASE_DIR` được trỏ tới `<InstallDir>\Temp\extract` cho exe cập nhật, để thư viện native không bung ra `%TEMP%\.net` (nơi người dùng thường ghi được) khi chạy với quyền Admin.
- **Rủi ro đã biết (đợt 8)** File `.sha256` chứng minh toàn vẹn, **không** chứng minh xuất xứ: exe chưa ký số, nên nếu tài khoản GitHub của chủ kho bị chiếm thì kẻ chiếm đăng được cả file lẫn mã băm. Chủ kho nên bật 2FA. Ghi trong README.
- **Rủi ro đã biết (đợt 8, có từ trước)** Người dùng tự chạy file mang đi (single-file) bằng quyền Admin thì thư viện native bị bung ra `%TEMP%\.net\FpsOverlay\<id>`, thư mục người dùng ghi được và được tái sử dụng: có thể bị cài DLL giả. Đường cập nhật tự động đã tránh (xem trên); chạy tay thì chưa.

## 8. Việc nên làm tiếp
1. **Kiểm tay đợt 8** (mục 9, phần "Đợt 8"), rồi merge và phát hành 1.4.0 (`.\scripts\release.ps1`).
2. **Kiểm tay đợt 6** (mục 9, phần "Đợt 6", mục 45–60) với bản 1.2.0; lỗi tìm thấy sửa trên nhánh mới từ master.
3. ~~Kiểm tay đợt 5~~ — người dùng đã kiểm OK. Kiểm tay phần còn lại của mục 9 (đợt 1–4: gỡ cài đặt, PawnIO, junction...) nếu chưa làm.
4. ~~Sửa nhỏ về bảo mật: đặt `COMSPEC` = `System32\cmd.exe` cho tiến trình cmd tự xoá khi gỡ cài đặt~~ — đã làm và merge vào master (cần kiểm tay mục 24–26).
5. Kiểm tra log khi `Documents` bị chuyển hướng/junction (hiện chỉ chặn một phần; tác động giới hạn ở việc tạo file mới, không ghi đè).
6. Các lỗi nhỏ đã hoãn (không chặn việc dùng): GPU tích hợp bận hơn ở desktop nên nhiệt GPU hiện `--`; dòng ghi chú PawnIO nháy ~1 s lúc mở app; ẩn overlay khi tắt log có thể đứng giao diện vài giây; file `.pdb` trong `publish\`.
7. Tính năng đã hoãn: **biểu đồ FPS/frametime**.

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

**Đợt 6 (1.2.0)**
45. Desktop: overlay ẩn. Vào game borderless: hiện ngay.
46. Mở menu khay giữa game: overlay không chớp tắt. Mục khay "Luôn hiện cho …" nhắm đúng game, không phải `explorer.exe`.
47. Alt-Tab trong game borderless: overlay phải còn hiện (không chớp tắt).
    Phím Win / menu Start trong game: overlay có thể ẩn, nhưng menu khay vẫn mở được và nhắm đúng game.
48. "Luôn hiện cho notepad.exe": overlay hiện khi Notepad ở phía trước.
49. "Không bao giờ hiện" cho trình duyệt rồi xem video toàn màn hình: overlay không hiện.
50. "Ẩn FPS" rồi vào game: vẫn ẩn.
51. "Di chuyển overlay" từ desktop: overlay hiện để kéo.
52. Cửa sổ cài đặt ngắn hơn rõ rệt. Mở/thu gọn cả ô thông số lẫn ô danh sách ứng dụng; tick/bỏ tick đổi overlay ngay; "Đang hiện N/10" đúng.
53. Ghi chú PawnIO vẫn thấy khi ô thông số thu gọn.
54. Danh sách ứng dụng hiện đúng; nút "Xoá" gỡ được một ứng dụng khỏi danh sách; nhóm rỗng hiện "(trống)".
55. Đổi ngôn ngữ: mọi chữ mới đổi theo.
56. Cập nhật đè bản 1.1.0 bằng file mang đi 1.2.0: hộp thoại báo Cập nhật; cài đặt cũ còn nguyên và tuỳ chọn "Chỉ hiện khi chơi game" đang bật.
57. Game Game Pass chạy cửa sổ được nhận ra (overlay hiện).
58. Tắt log, ở desktop: PresentMon biến khỏi Task Manager. Vào game: overlay hiện và có số sau vài giây.
59. Bật tự ẩn taskbar: Explorer hoặc Notepad phóng to (maximize) không làm overlay hiện. Game borderless vẫn hiện.
60. Chơi game, chuyển sang trình duyệt (overlay ẩn), khay "Di chuyển overlay", Enter: focus không quay về game cũ.

**Đợt 7 (1.3.0)**
61. Kiểm tay của Task 1 (cài đặt): các mục màu, FPS và cảnh báo thu gọn/mở ra được, trạng thái đúng, cửa sổ ngắn hơn, nút "Khôi phục mặc định"/"Xong" vẫn hiện.
62. Nhãn phiên bản trong cài đặt hiện đúng "1.3.0"; đổi ngôn ngữ thì chữ mới đổi theo.
63. Chạy lệnh cài một dòng (mục 5) trên một máy khác (cmd): tải được `FpsOverlay.exe`, hộp thoại cài hiện, cài xong chạy được.
64. Chạy lại đúng lệnh đó khi đã có bản cài (hoặc khi có bản mới hơn): hộp thoại báo Cập nhật, cài đặt cũ còn nguyên.
65. Trang Releases có `FpsOverlay.exe` và `FpsOverlay.exe.sha256`; `certutil -hashfile FpsOverlay.exe SHA256` khớp file `.sha256`.

**Đợt 8 (1.4.0)** (cần quyền Admin; mục 66–75 là phần tải/bàn giao; mục 66–71, 83 và 84 cần một bản phát hành thử, mục 73 và 75 cần đã cập nhật ít nhất một lần)
66. Bản cài ở vN, đã có bản vN+1 trên GitHub Releases kèm `FpsOverlay.exe.sha256`: mở hộp thoại cập nhật rồi bấm CẬP NHẬT.
    - Thanh tiến độ đầy dần, phần trăm đổi, nút HUỶ viết hoa ở cả tiếng Việt và tiếng Anh.
    - Xem giao diện ở cả 4 chủ đề.
67. Bấm HUỶ giữa lúc tải: hộp thoại đóng, nhắc nhở ở khay còn, không còn `FpsOverlay_update.*.exe` trong `C:\Program Files\FpsOverlay\Temp`.
68. Đóng bằng [ X ] giữa lúc tải: kết quả như mục 67.
69. Hỏng mã băm (bản thử có `.sha256` không khớp): hiện "Không cập nhật được." kèm "SHA-256 mismatch", file bị xoá, các nút lựa chọn quay lại.
70. Rút mạng giữa lúc tải: trong khoảng 30 giây báo lỗi hết thời gian chờ và file bị xoá.
71. Tải thành công:
    - App thoát (mất biểu tượng khay), exe mới hiện cửa sổ cài tự động: không nút, không ô PawnIO, chữ "Đang cập nhật lên vN+1…".
    - Sau đó mở bản đã cài ở vN+1, cài đặt còn nguyên.
    - Settings → Apps hiện đúng phiên bản mới.
72. Chạy `FpsOverlay_update.x.exe --update-after <PID của notepad>`: không chờ notepad, đi tiếp vào cập nhật tự động (báo đang chạy nếu bản ở khay còn mở).
73. Khởi động bản đã cài hơn 1 giờ sau một lần cập nhật: `Temp\FpsOverlay_update.*.exe` và `Temp\extract` bị xoá; cài PawnIO từ cài đặt vẫn chạy (dùng chung `SecureTemp`).
74. Bản `--no-install`: hộp thoại cập nhật hiện nút MỞ TRANG TẢI, không tải.
75. Khi đang cập nhật, `<InstallDir>\Temp\extract\FpsOverlay\<id>\*.dll` được dùng thay vì `%TEMP%\.net`.
76. Kiểm bằng mắt 4 chủ đề (Cổ điển, Cyberpunk, Neon tím, Terminal xanh) ở cửa sổ cài đặt, cài app và cập nhật, so với bản vẽ thử `themed-settings-v5.html`; đổi chủ đề trong cài đặt thì mọi cửa sổ đổi ngay, không cần mở lại.
77. Terminal xanh: kéo thanh trượt chữ bằng chuột và dùng phím (mũi tên, Home/End) đều chạy đúng.
78. Neon tím: ánh sáng và cuộn mượt, không giật; thanh cuộn đúng màu chủ đề ở cả 4 chủ đề.
79. Bấm "Kiểm tra cập nhật" khoảng 5–6 giây sau khi mở app (lúc kiểm tự động còn chạy): chỉ hiện đúng một kết quả.
80. Tác vụ khởi động của bản 1.3.0 (đã bật "Khởi động cùng Windows"): sau khi chạy 1.4.0 một lần, tác vụ có thêm `--autostart` (xem trong Task Scheduler).
81. Đăng xuất rồi đăng nhập với tác vụ có `--autostart` và đang có bản mới: không hộp thoại, chỉ thông báo khay và một dòng đầu menu khay; bấm dòng đó mở hộp thoại cập nhật.
82. (Tuỳ chọn) File chủ đề hỏng/không hợp lệ: cửa sổ rơi về Cyberpunk, không lỗi.
83. Thử cập nhật thật cần phát hành một bản thử (vd. 1.4.1): chỉ làm khi người dùng đồng ý, không tự phát hành.
84. **(Đường dễ hỏng nhất)** Cập nhật từ bản chạy bởi tác vụ khởi động: bật "Khởi động cùng Windows", đăng xuất rồi đăng nhập, chờ dòng báo bản mới ở khay, bấm dòng đó rồi bấm CẬP NHẬT → bản mới được cài và đang chạy (biểu tượng khay còn, Settings → Apps đúng phiên bản). App này nằm trong Job Object của tác vụ; nếu bản mới hay bản cài không lên, xem Trace "Không tách được … khỏi job" (job không cho tách). Cần bản phát hành thử như mục 83.
85. Mở app (không phải tự khởi động) khi có bản mới, rồi vào game toàn màn hình trong vòng 5 giây (bật chỉ-hiện-khi-chơi): không có hộp thoại cướp focus, chỉ balloon + dòng đầu menu khay; bấm vào thì mở hộp thoại. Ghi chú phát hành trong hộp thoại không còn `**` hay ```.
