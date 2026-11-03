# FPS Overlay: đợt 8, giao diện theo chủ đề và cập nhật qua GitHub — Thiết kế

## Bối cảnh
- Bản 1.3.0 đã phát hành tại `https://github.com/ckhangg/FpsOverlay`. Người dùng khác cài bằng một lệnh `curl`. Phát hành bản mới bằng `scripts/release.ps1`.
- Overlay có 4 phong cách: Cổ điển, Cyberpunk, Neon tím, Terminal xanh. Cửa sổ cài đặt và hộp thoại cài lại luôn mang giao diện Cyberpunk, vì chỉ có một file `Themes/Cyberpunk.xaml` (34 mẫu `Cp.*`) và mọi chỗ dùng đều gắn cố định (`StaticResource`).
- Người dùng muốn hai việc:
  1. **giao diện app đổi song song với chủ đề overlay**;
  2. **người dùng tự nhận bản mới qua nút cập nhật**: mở app là kiểm một lần, có bản mới thì hỏi cập nhật hoặc bỏ qua.

## Phần A. Giao diện app theo chủ đề

### Người dùng đã chọn
- Cả 4 bộ giao diện theo bản vẽ thử `.superpowers/brainstorm/1187-1793714793/content/themed-settings-v5.html` (đã duyệt, kể cả kiểu công tắc dạng chữ của Terminal).
- Cách làm 1: bốn file chủ đề cùng bộ tên mẫu, đổi file khi đổi chủ đề.

### Hành vi
- **Cửa sổ áp dụng:** cửa sổ cài đặt, hộp thoại cài/cập nhật (`InstallWindow`) và hộp thoại cập nhật mới (phần B). Hộp `MessageBox` của Windows và menu khay (WinForms) giữ như cũ.
- **Đổi tức thì:** chọn phong cách trong cài đặt thì overlay và cửa sổ cài đặt cùng đổi ngay, không cần đóng mở lại. Các cửa sổ khác mở sau đó cũng dùng chủ đề mới.
- **Hộp thoại cài lần đầu** (chưa có file cài đặt) dùng Cyberpunk, là phong cách mặc định. Cài đè hay cập nhật thì dùng phong cách đã lưu.
- **Bố cục chung:** cả 4 chủ đề cùng bố cục (cùng hàng, cột, kích thước cửa sổ). Chỉ màu, font, viền, góc và hình dáng điều khiển khác nhau. Chữ không được tràn hay đè nhau ở cả hai ngôn ngữ.

### Bốn bộ giao diện (theo bản vẽ đã duyệt)
| | Cổ điển | Cyberpunk (giữ nguyên hiện tại) | Neon tím | Terminal xanh |
|---|---|---|---|---|
| Nền cửa sổ | `#1B1B1B`, thanh tiêu đề `#111111` | như hiện tại `#0A0A0F`, thanh tiêu đề vàng | `#140620`, viền `#C64BFF` 1.5 phát sáng tím, bo góc 14 | `#000800`, viền `#1FAA44` 1, vạch quét CRT mờ, góc vuông |
| Điểm nhấn | cam `#FF8C1A` | vàng `#FCEE0A` / cyan `#00F0FF` | hồng `#FF4FD8` (phát sáng) | xanh `#33FF66` / `#7CFF9E` |
| Chữ thường | `#E6E6E6` | `#D8D8E0` | `#F3E6FF` | `#33FF66` (phát sáng nhẹ) |
| Font chữ / số | Segoe UI / Consolas | Chakra Petch / JetBrains Mono | Orbitron (tiêu đề) + JetBrains Mono | VT323 cho mọi chữ, cỡ lớn hơn khoảng 1,25 lần |
| Tiêu đề mục | `// GIAO DIỆN` màu cam | như hiện tại | Orbitron, hồng phát sáng | `// GIAO DIỆN` xanh sáng |
| Nút | nền cam, bo 4; nút phụ viền xám | như hiện tại (vát góc) | dạng viên thuốc, hồng phát sáng; nút phụ viền tím | chữ dạng `[ XONG ]`, không nền |
| Công tắc | viên tròn cam | như hiện tại | viên tròn hồng phát sáng | chữ `[BẬT]` / `[TẮT]` (en `[ON]` / `[OFF]`) |
| Thanh trượt | rãnh xám, phần đã chọn cam, núm tròn | như hiện tại | rãnh tím, phần đã chọn chuyển tím→hồng, núm phát sáng | chữ `[######----]` (10 ô) |
| Lựa chọn (phong cách, ngôn ngữ...) | viên bo 4, mục chọn nền cam | như hiện tại | viên tròn, mục chọn viền hồng phát sáng | chữ thường, mục chọn nền xanh chữ đen |
| Ký hiệu mở/thu mục | ▾ / ▴ | ▾ / ▴ | ▾ / ▴ | `[+]` / `[-]` |

- Mỗi chủ đề cung cấp **đủ mọi mẫu** mà cửa sổ đang dùng: màu, font, kiểu nút, công tắc, thanh trượt, lựa chọn, ô màu mẫu, ô nhập mã màu, tiêu đề mục, nút tiêu đề thu gọn, checkbox, thanh cuộn nếu có kiểu riêng, viền focus bàn phím. Thiếu mẫu nào thì cửa sổ sẽ lỗi khi đổi chủ đề, nên phải có kiểm tra (xem Ràng buộc).
- **Terminal:** công tắc, thanh trượt và lựa chọn vẫn là đúng điều khiển WPF (bấm, kéo, phím mũi tên, Tab và Space vẫn dùng được); chỉ phần vẽ là chữ. Thanh trượt dạng chữ vẫn kéo bằng chuột được: bấm hoặc kéo dọc dải chữ đặt giá trị theo vị trí.
- **Chữ phụ thuộc ngôn ngữ trong mẫu** (`[BẬT]`/`[TẮT]`, `[ON]`/`[OFF]`) lấy từ tài nguyên mà app đặt lại mỗi khi đổi ngôn ngữ. Ký hiệu mở/thu mục cũng lấy từ tài nguyên của chủ đề, thay cho ▾/▴ viết cứng trong code.
- **Đổi tên mẫu `Cp.*` thành tên theo vai trò** (`Ui.Accent`, `Ui.Text`, `Ui.Switch`, `Ui.Section`...) *(quyết định của người làm thiết kế: tên `Cp.Yellow` không còn đúng khi chủ đề Cổ điển dùng cam)*.

## Phần B. Cập nhật qua GitHub

### Người dùng đã chọn
- Kiểm một lần mỗi khi mở app.
- Có bản mới thì hỏi với ba nút **Cập nhật** / **Bỏ qua bản này** / **Để sau**.
- Có nút kiểm tra bằng tay ở cài đặt và ở khay.
- Khi app tự chạy lúc đăng nhập Windows: chỉ báo ở khay, không bật hộp thoại.
- Chỉ bản đã cài vào Program Files mới tự cập nhật.
- Cách làm 1: tải bản mới, chạy nó với tham số chờ app cũ thoát rồi tự cài đè, không hỏi lại.

### Kiểm tra
- **Nguồn:** `GET https://api.github.com/repos/ckhangg/FpsOverlay/releases/latest`, kèm header `User-Agent: FpsOverlay/<version>` và `Accept: application/vnd.github+json`.
  - Lấy `tag_name` (dạng `vX.Y.Z`), `body` (ghi chú phát hành), `html_url`, và hai asset tên đúng `FpsOverlay.exe` và `FpsOverlay.exe.sha256` (dùng `browser_download_url`).
  - Thiếu một trong hai asset thì coi như **không có bản cập nhật dùng được**.
  - Chỉ dùng release không phải bản nháp hay thử nghiệm; endpoint `latest` vốn đã như vậy.
- **So phiên bản:** theo số `major.minor.patch`, bỏ chữ `v` đầu và mọi đuôi `-…`/`+…`. Chỉ báo khi bản trên GitHub **lớn hơn** bản đang chạy.
- **Khi nào kiểm:**
  - **Mở app**, trên luồng nền, vài giây sau khi app khởi động xong, để không chậm lúc mở. Không có mạng, quá thời gian 10 giây, GitHub lỗi hay giới hạn lượt gọi: bỏ qua im lặng, ghi Trace.
  - **Bấm tay:** nút "Kiểm tra cập nhật" trong mục "// HỆ THỐNG" của cài đặt và mục "Kiểm tra cập nhật" trong menu khay.
    - Có bản mới (kể cả bản đã bỏ qua) → hộp thoại cập nhật.
    - Đã mới nhất → báo "Bạn đang dùng bản mới nhất (vX.Y.Z)".
    - Lỗi → báo "Không kiểm tra được cập nhật" kèm lý do ngắn.
  - Không kiểm định kỳ trong lúc app đang chạy.
- **"Bỏ qua bản này":** lưu `SkippedVersion` (ví dụ `"1.4.0"`) trong `settings.json`.
  - Lần kiểm tự động sau không báo bản đó nữa; bản lớn hơn thì vẫn báo.
  - **Khôi phục mặc định** không xoá giá trị này.
- **"Để sau":** đóng hộp thoại, không lưu gì; lần mở app sau hỏi lại.

### Khi app tự chạy lúc đăng nhập Windows
- App phải biết mình được Task Scheduler bật:
  - tác vụ khởi động cùng Windows chạy exe kèm tham số **`--autostart`**;
  - tác vụ cũ (tạo từ bản trước, không có tham số) được **tự sửa** như cơ chế "trỏ lại tác vụ" hiện có: so cả đường dẫn lẫn tham số.
- Khi chạy với `--autostart` và có bản mới chưa bị bỏ qua:
  - **không** mở hộp thoại;
  - hiện **thông báo khay** (balloon) "Có bản FPS Overlay vX.Y.Z — bấm để xem", bấm vào thì mở hộp thoại cập nhật;
  - thêm dòng **"Có bản vX.Y.Z…"** đầu menu khay, bấm vào cũng mở hộp thoại.

  Dòng này cũng hiện khi người dùng chọn "Để sau" ở lần mở app thường, cho tới khi tắt app.

### Hộp thoại cập nhật (cửa sổ mới, theo chủ đề)
- **Nội dung:**
  - tiêu đề "Có bản cập nhật";
  - "Phiên bản hiện tại: vA.B.C → Bản mới: vX.Y.Z";
  - ghi chú phát hành: văn bản thuần, cuộn được, giới hạn khoảng 20 dòng chiều cao;
  - ba nút: **Cập nhật** (chính) / **Bỏ qua bản này** / **Để sau**.
- **Bản chạy `--no-install`** (không phải bản đã cài): nút chính là **"Mở trang tải"**, mở `html_url` bằng trình duyệt mặc định, gọi `explorer.exe` bằng đường dẫn đầy đủ như chỗ mở thư mục log.
- **Bấm "Cập nhật":**
  1. Hộp thoại chuyển sang chế độ tải: thanh tiến trình theo byte, nút **Huỷ**.
  2. Tải `FpsOverlay.exe.sha256` rồi `FpsOverlay.exe` vào **`C:\Program Files\FpsOverlay\Temp`**.
     - Thư mục này chỉ Administrators/SYSTEM ghi được; dùng lại hàm tạo thư mục và ACL của PawnIO.
     - Tên file ngẫu nhiên, đuôi `.exe`. Giới hạn dung lượng 400 MB. Chỉ theo chuyển hướng HTTPS.
  3. So SHA-256 của file tải về với giá trị trong file `.sha256` (chuỗi hex đầu tiên, không phân biệt hoa thường). Sai thì xoá file, báo lỗi, app cũ chạy tiếp.
  4. Chạy file vừa tải với tham số **`--update-after <PID của app hiện tại>`**, rồi app hiện tại tự thoát: ghi nốt log, cài đặt, dừng PresentMon như `OnExit`.
- **Lỗi bất kỳ** (mạng, hết chỗ đĩa, huỷ): xoá file dở, báo lỗi ngắn, app cũ tiếp tục bình thường.

### Bản mới chạy với `--update-after <PID>`
- Đây là exe tải về, nằm trong `Program Files\FpsOverlay\Temp`, tức ngoài thư mục cài, nên đi vào nhánh cài đặt hiện có.
- **Chờ tiến trình `<PID>` thoát** (tối đa 30 giây; chỉ chờ nếu PID đó đúng là `FpsOverlay.exe` của bản cài). Quá thời gian thì báo "FPS Overlay cũ vẫn đang chạy" như hiện nay và thoát.
- Sau đó **tự cài đè, không hiện hộp hỏi**:
  - chỉ hiện một cửa sổ tiến trình nhỏ theo chủ đề đã lưu, nội dung "Đang cập nhật lên vX.Y.Z…";
  - **không** hỏi PawnIO;
  - quyết định cài dùng `Installer.Decide()` như cũ: chỉ cập nhật khi bản này mới hơn hoặc cùng bản khác nội dung; không thì chạy bản đã cài.
- Xong thì mở bản đã cài (như sau khi cài thường) và thoát. Lỗi thì báo lỗi như hộp thoại cài hiện nay.
- **Dọn file:** bản đã cài, mỗi lần khởi động, xoá các file `*.exe` trong `Program Files\FpsOverlay\Temp` cũ hơn 1 giờ (best-effort, lỗi bỏ qua). Lý do: exe tải về không tự xoá được chính nó.

### Bảo mật (giữ các nguyên tắc của đợt 3–4)
- Chỉ nói chuyện qua HTTPS với `api.github.com` và các link tải GitHub trả về (`github.com`, `objects.githubusercontent.com`, `release-assets.githubusercontent.com`). Từ chối URL không phải HTTPS.
- Không bao giờ chạy với quyền Admin một file nằm ở chỗ người dùng thường ghi được.
- Mã băm chỉ chống file hỏng hoặc tải dở. Nó **không** chống trường hợp tài khoản GitHub bị chiếm. Ghi rõ điều này trong README như rủi ro đã biết. Khuyên chủ kho bật xác thực hai lớp.

## Phiên bản và tài liệu
- `<Version>` = **1.4.0**.
- **README** (vi và en): thêm mục "Cập nhật" (tự kiểm khi mở app, nút kiểm tra tay, bỏ qua bản này) và ghi chú bảo mật về mã băm.
- **HANDOFF:** lịch sử thêm đợt 8, các quyết định mới, danh sách kiểm tay.
  - Kiểm tay phải gồm **cập nhật thật**: phát hành một bản thử (ví dụ `1.4.1`) rồi cập nhật từ 1.4.0 bằng nút. Bước này cần người dùng đồng ý phát hành bản thử.

## Không làm
- Ký số file exe; kiểm chữ ký.
- Cập nhật im lặng không hỏi; kiểm định kỳ khi app đang chạy.
- Cập nhật từng phần (delta); quay về bản cũ (rollback) tự động.
- winget.
- Đổi giao diện menu khay WinForms hoặc `MessageBox` theo chủ đề.
- Hiển thị Markdown đẹp cho ghi chú phát hành (chỉ văn bản thuần).

## Cách làm đã chọn
- **Giao diện:**
  - **Bốn file chủ đề cùng bộ tên + gắn động** (chọn). Làm được đúng 4 bộ đã duyệt, kể cả điều khiển dạng chữ của Terminal và nút viên thuốc của Neon.
  - Một file chung + màu tính bằng code (không chọn): không làm được hình dáng điều khiển khác nhau.
- **Cập nhật:**
  - **Tải bản mới rồi chạy nó với `--update-after <PID>` để tự cài đè** (chọn). Dùng lại cơ chế cài đè đã có, chỉ một file exe.
  - Chương trình cập nhật riêng `Updater.exe` (không chọn): thêm một file phải đóng gói và bảo trì, lợi ích không đáng kể.

## Ràng buộc
- **Phần thuần trong Core, có test:**
  - đọc JSON release của GitHub (tag, ghi chú, URL hai asset; thiếu asset → không có cập nhật);
  - so phiên bản;
  - quyết định có báo hay không (theo `SkippedVersion`, tự động hay bấm tay);
  - đọc file `.sha256`;
  - đọc tham số `--update-after` và `--autostart`;
  - XML tác vụ có tham số `--autostart` và phép so "tác vụ cũ cần sửa".
- **Kiểm tra chủ đề tự động:** một test (trong Core, đọc các file XAML như văn bản/XML) xác nhận 4 file chủ đề định nghĩa **cùng một tập khoá**, và mọi khoá `Ui.*` mà `SettingsWindow.xaml`, `InstallWindow.xaml`, cửa sổ cập nhật dùng đều có trong mỗi file.
- Mọi chữ mới có tiếng Việt và tiếng Anh. Build 0 cảnh báo, mọi test qua.
- Đường FPS mỗi khung hình không đổi, không cấp phát. Overlay ngoài chế độ di chuyển vẫn bấm xuyên, không chiếm focus.
- Kiểm tra cập nhật không được chặn luồng UI. App vẫn mở và chạy bình thường khi không có mạng.

## Để người làm quyết định
- Tên cụ thể của từng mẫu `Ui.*` và cách chia file XAML.
- Cách vẽ viền phát sáng và bo góc của cửa sổ Neon (có thể cần cửa sổ trong suốt), và vạch quét của Terminal.
- Dùng `HttpClient` hay cách khác để tải, cách báo tiến trình.
- Bố cục chi tiết hộp thoại cập nhật, theo kiểu hộp thoại cài hiện có.
