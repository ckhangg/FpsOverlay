# EzFPS: tên hiển thị và logo mới — Thiết kế ngắn

## Vì sao
- Tìm "FPS" trong Windows thì app hiện icon mặc định của Windows, vì exe chưa nhúng icon.
- Người dùng muốn app có tên **EzFPS** và logo riêng.

## Người dùng đã chọn
- **Đổi tên hiển thị** thành **EzFPS**. Không đổi tên kỹ thuật.
- **Logo:** mẫu **D. Màn hình 60**, theo bản vẽ thử `.superpowers/brainstorm/1136-1793851431/content/logo-concepts.html`:
  - màn hình nền `#140620`, viền hồng neon `#FF4FD8`, bo góc;
  - chân đế hồng;
  - số **60** màu trắng, font đơn cách đậm.
- Dùng logo ở **mọi chỗ**:
  - icon của exe (ô tìm kiếm Windows, lối tắt Start Menu, Settings → Apps);
  - icon khay;
  - icon các cửa sổ của app.
- **Bản 16 px đơn giản hoá:** viền dày hơn, số to hơn, bỏ chân đế. Các cỡ lớn giữ đủ chi tiết.

## Làm gì
- **File icon:** tạo `src/FpsOverlay/Assets/EzFPS.ico` gồm các cỡ 16, 24, 32, 48, 64, 128 và 256 px (256 lưu dạng PNG nén trong ico).
  - Vẽ bằng một script tái tạo được, đặt trong kho, ví dụ `scripts/make-icon.ps1` hoặc một công cụ .NET nhỏ trong `tools/`, để sau này chỉnh logo thì chạy lại script.
  - Script vẽ từ hình học của mẫu D. Bản 16 px (và 24 px nếu cần cho dễ đọc) vẽ theo dạng đơn giản hoá.
- **Nhúng vào exe:** `<ApplicationIcon>Assets\EzFPS.ico</ApplicationIcon>` trong `src/FpsOverlay/FpsOverlay.csproj`. Nó phải có mặt ở cả bản đã cài (nhiều file) lẫn file mang đi (một file).
- **Khay và cửa sổ:** dùng chính `EzFPS.ico`, nạp từ tài nguyên nhúng, thay cho `TrayIconImage` đang vẽ chữ "FPS" lúc chạy. Bỏ code vẽ cũ; `SettingsWindow.CreateWindowIcon` và các chỗ tương tự dùng icon mới.
- **Tên hiển thị "EzFPS"** thay cho "FPS Overlay" ở:
  - lối tắt Start Menu: `EzFPS.lnk`, mô tả "EzFPS";
  - mục gỡ cài đặt: `DisplayName` và `Publisher` là "EzFPS";
  - mọi chuỗi giao diện và tiêu đề cửa sổ (`Loc`, tiêu đề cửa sổ, chú thích khay, tiêu đề `MessageBox`, thông báo khay);
  - README (tiêu đề và mô tả; lệnh cài và đường dẫn tải giữ nguyên);
  - ghi chú phát hành do `scripts/release.ps1` sinh ra, ví dụ "EzFPS 1.5.0".
- **Lối tắt cũ:** khi cài hoặc cập nhật, xoá lối tắt cũ `FPS Overlay.lnk` trong Start Menu chung nếu có, để không còn hai lối tắt. Gỡ cài đặt cũng xoá cả hai tên.
- **Phiên bản:** `1.5.0`.

## Giữ nguyên (không đổi)
- Tên file `FpsOverlay.exe` trong release và trong thư mục cài. Bộ cập nhật 1.4.x đã cài trên các máy tìm đúng tên asset này.
- Kho `ckhangg/FpsOverlay` và URL API.
- Thư mục cài `Program Files\FpsOverlay`, `%AppData%\FpsOverlay`, `Documents\FpsOverlay\Logs`.
- Tên tác vụ Task Scheduler, khoá registry gỡ cài đặt `...\Uninstall\FpsOverlay`, tên mutex.
- Namespace và tên dự án trong code.

## Cách làm đã chọn
Nhúng file `.ico` nhiều cỡ vào exe (`ApplicationIcon`). Đây là cách duy nhất để Windows hiện icon ở ô tìm kiếm và Start Menu. Không chọn cách vẽ icon lúc chạy, vì Windows không đọc được icon kiểu đó.

## Ràng buộc
- Build 0 cảnh báo, mọi test qua.
- Có test (Core hoặc test đọc file) xác nhận:
  - `EzFPS.ico` có đủ 7 cỡ;
  - csproj có `ApplicationIcon`;
  - không còn chuỗi "FPS Overlay" trong chuỗi giao diện, trừ chỗ cố ý giữ (chú thích "trước đây là FPS Overlay" nếu có).
- Kiểm tay:
  - cập nhật từ 1.4.1 lên 1.5.0 bằng nút trong app: lối tắt đổi tên, chỉ còn một lối tắt;
  - tìm "EzFPS" và "FPS" trong Windows đều ra app với icon mới (có thể cần đăng xuất/đăng nhập vì bộ nhớ đệm icon);
  - icon khay và icon cửa sổ là logo mới.

## Để người làm quyết định
- Công cụ vẽ icon (System.Drawing trong script PowerShell hoặc một công cụ .NET nhỏ).
- Chi tiết pixel của bản 16 và 24 px.
