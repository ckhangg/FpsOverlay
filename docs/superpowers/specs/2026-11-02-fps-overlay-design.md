# FPS Overlay — Thiết kế bản đầu tiên

## Mục đích
Một app Windows tự viết, hiện số FPS ở góc màn hình khi chơi game. Người dùng muốn app của riêng mình thay vì Steam overlay / Xbox Game Bar để sau này tuỳ chỉnh giao diện và thêm tính năng theo ý mình. Máy: laptop GTX 1650 + AMD onboard, Windows 11, chơi game ở chế độ **toàn màn hình không viền (borderless)**.

## Mục tiêu (bản đầu tiên)
- Hiện số FPS của game đang chơi (cửa sổ đang ở phía trước) ở góc màn hình.
- Biểu tượng nhỏ ở khay hệ thống (góc phải thanh taskbar). Chuột phải mở menu: **Hiện FPS / Ẩn FPS** và **Thoát**.

## Không làm ở bản này
- Nhiệt độ, mức dùng CPU/GPU, đồng hồ.
- Màn hình cài đặt, đổi màu/vị trí/cỡ chữ trong app.
- Phím tắt, tự khởi động cùng Windows.
- Game chạy **Exclusive Fullscreen**.

## Không được phép (dù app vẫn "chạy được")
- Chèn code vào game hay hook DirectX: có nguy cơ bị anti-cheat ban tài khoản.
- Con số FPS chặn chuột hoặc chiếm focus của game.
- Làm game giật hay tụt FPS thấy rõ.

## Ràng buộc
- App cần chạy với quyền Administrator thì mới đọc được dữ liệu khung hình (ETW).
- Phải cài .NET SDK và tải PresentMon (mã nguồn mở của Intel, giấy phép MIT). Hỏi người dùng trước khi tải/cài.

## Các cách đã cân nhắc
| Cách | Đánh đổi |
|---|---|
| **C# + WPF** ✅ (đã chọn) | Làm giao diện tuỳ chỉnh, cửa sổ cài đặt, thêm tính năng đều dễ. Cần cài .NET SDK. |
| C++ | Nhẹ nhất, nhưng mỗi tính năng giao diện tốn công hơn nhiều lần, khó chỉnh sửa về sau. |
| Python | Code ngắn nhất, nhưng cửa sổ trong suốt và đóng gói `.exe` chắp vá, file nặng, hay bị diệt virus báo nhầm. |

Người dùng chọn C# + WPF vì muốn mở rộng tính năng và giao diện về sau.

## Cách hoạt động
- **Đo FPS:** app chạy PresentMon ngầm (dạng dòng lệnh, xuất CSV ra stdout, theo dõi mọi tiến trình). Mỗi dòng là một khung hình kèm ID tiến trình và thời gian giữa hai khung hình. App lấy ID tiến trình của cửa sổ đang ở phía trước và tính FPS từ các khung hình của tiến trình đó.
- **Hiển thị:** một cửa sổ WPF trong suốt, luôn nằm trên cùng, chuột bấm xuyên qua, không hiện trên taskbar, không chiếm focus.
- **Tách lớp:** phần đo FPS và phần hiển thị tách riêng (phần đo chỉ đưa ra con số), để sau này thêm thông số mới hoặc đổi giao diện mà không phải sửa phần đo.

## Quyết định của người dùng
- Vị trí góc **trên bên trái** màn hình, chữ **màu xanh lá**.
- Số FPS cập nhật **nhanh nhất có thể**.
- Khi cửa sổ phía trước không vẽ khung hình nào (ví dụ đang ở desktop), hiện `--`.
- Dùng .NET 10.

## Các quyết định do Claude tự đề xuất (người dùng có thể sửa)
- Chữ đủ to để liếc là thấy, có viền tối để đọc được trên nền game sáng.
- "Nhanh nhất có thể": con số được vẽ lại mỗi khi có dữ liệu khung hình mới từ PresentMon (giới hạn bởi tần số màn hình). FPS tính trên cửa sổ trượt khoảng 0,5 giây gần nhất, vì FPS của từng khung hình riêng lẻ nhảy loạn, không đọc được. Độ dài cửa sổ trượt là một hằng số dễ chỉnh.
- Kết quả là một thư mục chứa file `.exe` của app kèm `PresentMon.exe`.

## Để người viết code tự quyết
- Thư viện cho biểu tượng khay hệ thống (WPF không có sẵn: dùng `NotifyIcon` của WinForms hoặc thư viện như H.NotifyIcon).
- Tham số dòng lệnh và cột CSV cụ thể của phiên bản PresentMon được dùng.
- Cách giữ cửa sổ luôn nằm trên game borderless (ví dụ đặt lại "topmost" định kỳ nếu bị game đè).
- Dọn dẹp: tắt PresentMon khi app thoát, kể cả khi app bị đóng đột ngột.
