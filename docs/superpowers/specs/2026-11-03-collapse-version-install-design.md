# FPS Overlay: đợt 7, thu gọn cài đặt, số phiên bản, cài bằng một lệnh — Thiết kế ngắn

## Vì sao
- Cửa sổ cài đặt vẫn dài.
- Người dùng không biết mình đang chạy bản nào.
- Muốn đưa app cho người khác thì phải gửi file 150 MB qua lại.

## Mục tiêu

### 1. Thu gọn thêm ba mục trong cửa sổ cài đặt (người dùng yêu cầu)
- Ba mục **"// MÀU SẮC"**, **"// MÀU THEO FPS"** và **"// CẢNH BÁO"** dùng cùng kiểu với ô "Chọn thông số hiển thị" của đợt 6:
  - tiêu đề mục vẫn hiện;
  - toàn bộ nội dung của mục nằm trong một phần xổ ra, **mặc định thu gọn** mỗi lần mở cửa sổ;
  - bấm vào thanh tiêu đề (có mũi tên ▾ khi thu gọn, ▴ khi mở) thì xổ ra hoặc thu lại.
- Khi thu gọn, cạnh tiêu đề có một dòng tóm tắt ngắn *(đề xuất của người làm thiết kế)*:
  - **Màu sắc:** ba ô màu nhỏ (nhãn, giá trị, FPS) của phong cách đang chọn;
  - **Màu theo FPS:** "Bật · 60/30" hoặc "Tắt" (en "On · 60/30" / "Off");
  - **Cảnh báo:** "GPU 85° · CPU 90° · 10s".
- Mục "// GIAO DIỆN", "// THÔNG SỐ" và "// HỆ THỐNG" giữ nguyên.
- Nút "Thử cảnh báo" nằm trong phần xổ ra của mục Cảnh báo.

### 2. Số phiên bản
- Góc **dưới bên trái** cửa sổ cài đặt, cùng hàng với nút "Khôi phục mặc định" / "Xong": chữ nhỏ, mờ, dạng `v1.3.0`.
- Lấy từ phiên bản của chính file app (`<Version>` trong csproj), bỏ phần đuôi `+<mã commit>`.

### 3. Cài bằng một lệnh (người dùng chọn cách 1)
- **Kho GitHub công khai** `ckhangg/FpsOverlay`, chứa cả mã nguồn (người dùng đồng ý).
- **Lịch sử git** (người dùng chọn hướng 1):
  - đổi tác giả và người commit của mọi commit thành `CKhang <39177627+ckhangg@users.noreply.github.com>`;
  - đặt cấu hình git riêng cho kho này (`user.name`, `user.email`) như trên;
  - danh tính công ty cũ (email và tên tài khoản) không còn trong lịch sử đẩy lên.
  - Trước khi đổi, lưu một bản sao toàn bộ lịch sử cũ thành file `%USERPROFILE%\Downloads\FPS-history-backup.bundle` (ngoài thư mục dự án) để khôi phục được nếu cần.
- **Phát hành:** mỗi bản là một GitHub Release có tag `vX.Y.Z`, kèm hai file:
  - `FpsOverlay.exe` (bản đóng gói một file);
  - `FpsOverlay.exe.sha256` (mã băm).

  Làm bằng một script `scripts/release.ps1` chạy tại máy này. Script:
  1. kiểm cây git sạch và đang ở master;
  2. chạy test;
  3. `dotnet publish`;
  4. tính SHA-256;
  5. `gh release create v<Version> …` với ghi chú ngắn.
- **Lệnh cài cho người khác** (dán vào cmd, Windows 10/11 có sẵn `curl`):
  ```
  curl -fL -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
  ```
  `start` mở bằng ShellExecute nên Windows hỏi UAC, rồi hiện hộp thoại cài như hiện nay. Chạy lại lệnh này về sau là cập nhật lên bản mới nhất.
- **README.md** (tiếng Việt và tiếng Anh) ở gốc kho:
  - app làm gì;
  - yêu cầu: Windows 10/11 64-bit, quyền Admin;
  - lệnh cài ở trên;
  - cách tải tay từ trang Releases;
  - cách gỡ;
  - ghi chú về PawnIO.
- **Giấy phép mã nguồn: MIT** *(đề xuất của người làm thiết kế; không có giấy phép thì người khác không được phép dùng lại mã)*. File `LICENSE` ở gốc kho. Giấy phép của các thành phần kèm theo (PresentMon, LibreHardwareMonitor, font...) giữ nguyên như hiện nay.
- Phiên bản: `1.3.0`.

## Không làm
- winget (để đợt sau; cần thêm chế độ cài im lặng).
- Kiểm mã băm trong lệnh cài.
- Tự build trên GitHub Actions.
- Ký số file exe.
- App tự kiểm tra bản mới.

## Cách làm đã chọn
- **Cách 1: lệnh `curl` trong cmd** (chọn). Một dòng, chạy trong cmd như người dùng muốn, luôn tải bản mới nhất.
- Cách 2: script PowerShell `irm … | iex` có kiểm mã băm (không chọn lúc này). An toàn hơn, nhưng phải dùng PowerShell và bảo trì thêm script.
- Cách 3: winget (để sau). Cần duyệt từ Microsoft và chế độ cài im lặng.

## Ràng buộc
- **Đổi lịch sử git** chỉ làm trên master, sau khi merge nhánh của đợt này, **trước** lần đẩy đầu tiên. Kiểm `git log` không còn danh tính công ty cũ (email, tên tài khoản). Test vẫn qua sau khi đổi.
- **Tạo kho, đẩy mã, tạo release:** dùng tài khoản `ckhangg` (đã đăng nhập `gh`, đang active).
- **Không đưa lên kho:** thư mục `publish/`, `.superpowers/`, file `.pdb`. Kiểm `.gitignore` trước khi đẩy.
- Mọi chữ mới trong app có tiếng Việt và tiếng Anh. Build 0 cảnh báo, mọi test qua.

## Để người làm quyết định
- Bố cục dòng tóm tắt, cỡ và màu của số phiên bản, theo kiểu Cyberpunk hiện có.
- Nội dung ghi chú release.
