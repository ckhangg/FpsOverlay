# FPS Overlay: đợt 1, giao diện (phong cách, màn hình cài đặt) — Thiết kế

## Bối cảnh
App FPS Overlay bản đầu tiên đã chạy ổn (xem `2026-11-02-fps-overlay-design.md`). Bản đó hiện số FPS màu xanh lá ở góc trên trái, bật/tắt và thoát qua menu khay. Người dùng muốn làm tất cả các đề xuất mở rộng, chia thành 4 đợt, **giao diện làm trước**:

1. **Giao diện** (tài liệu này)
2. Tiện lợi: phím tắt, tự khởi động cùng Windows, game Store/Game Pass, các lỗi nhỏ còn tồn
3. Thông số: GPU, CPU, RAM, nhiệt độ, 1% low
4. Nâng cao: ghi log FPS, nhiều màn hình

Lý do người dùng tự viết app thay vì dùng Steam/Game Bar: **tuỳ chỉnh theo ý mình**. Họ thích giao diện overlay của MSI Afterburner và phong cách **Cyberpunk 2077**.

## Mục tiêu
- Overlay **một dòng gọn** (kiểu "C" đã chọn trên mockup): các mục nằm ngang trên một dải nền tối mờ. Mỗi mục gồm **nhãn** và **giá trị**. Đợt này chỉ có mục FPS, nhưng bố cục phải chứa được nhiều mục để đợt 3 nối thêm GPU, CPU, RAM vào cùng hàng.
- Hai **phong cách overlay**, chuyển qua lại trong cài đặt:
  - **Cổ điển:** font Consolas đậm, nhãn cam `#FF8C1A`, giá trị trắng, nền đen mờ bo góc 4px, viền tối quanh chữ.
  - **Cyberpunk:** font Chakra Petch cho nhãn, JetBrains Mono cho số; nhãn cyan `#00F0FF`, giá trị trắng, FPS vàng neon `#FCEE0A` phát sáng nhẹ; nền `#0A0A0F` mờ, viền vàng 3px bên trái, góc phải dưới vát chéo.
- **Đổi màu theo FPS** (bật/tắt được):
  - Cổ điển: xanh lá khi FPS ≥ mốc xanh, vàng khi ở giữa, đỏ khi FPS < mốc đỏ.
  - Cyberpunk: cyan `#00F0FF` / vàng `#FCEE0A` / đỏ `#FF003C` theo cùng quy tắc.
  - Mặc định: mốc xanh 60, mốc đỏ 30.
  - Khi tắt: FPS dùng màu giá trị FPS mặc định của phong cách (Cổ điển xanh lá, Cyberpunk vàng neon).
  - `--` (không có dữ liệu) luôn dùng màu giá trị thường, không đổi màu.
- **Màn hình cài đặt** phong cách Cyberpunk (theo mockup `cyberpunk-v2`), mở từ mục **"Cài đặt..."** trong menu khay. Chỉnh được:
  - Phong cách: Cổ điển / Cyberpunk
  - Góc hiển thị: trên trái / trên phải / dưới trái / dưới phải
  - **Kích cỡ:** 50% đến 300%, phóng to/thu nhỏ cả dải overlay (chữ, khoảng cách, nền) cùng lúc
  - Độ mờ nền: 0% (trong suốt hẳn) đến 100% (đặc)
  - Màu nhãn, màu giá trị
  - Bật/tắt đổi màu theo FPS, mốc xanh, mốc đỏ
  - Nút **"Khôi phục mặc định"** và **"Xong"**
- **Xem trước trực tiếp:** mọi thay đổi áp dụng ngay lên overlay ngoài màn hình, không cần bấm lưu, không cần khởi động lại app.
- **Số mẫu khi đang mở cài đặt** (người dùng chọn): trong lúc cửa sổ cài đặt mở, app đứng phía trước nên không có FPS thật để xem. Overlay khi đó hiện một số FPS mẫu tự chạy lên xuống (khoảng 20 → 144 → 20, lặp lại vài giây một vòng) để thấy đủ ba màu theo các mốc đang chỉnh. Đóng cửa sổ cài đặt thì overlay quay về FPS thật.
- **Lưu tự động** và giữ lại cho lần mở sau.
- Menu khay thêm: **"Cài đặt..."** và menu con **"Góc hiển thị"** (4 góc, đánh dấu góc đang dùng) để đổi nhanh khi đang chơi.
- **Icon khay riêng:** chữ "FPS" nhỏ, dễ nhận ra, thay icon mặc định của Windows.
- **Hai ngôn ngữ: tiếng Việt và tiếng Anh** (người dùng yêu cầu). Chọn trong cài đặt; menu khay, cửa sổ cài đặt, tooltip và mọi thông báo lỗi đổi theo ngay lập tức, không cần khởi động lại. Nhãn trên overlay (FPS, sau này GPU, CPU, RAM) giống nhau ở cả hai ngôn ngữ.

## Không làm ở đợt này
- Biểu đồ FPS/frametime (người dùng bỏ, có thể làm sau).
- Kéo thả hay kéo giãn overlay trực tiếp bằng chuột trên màn hình (người dùng chỉ muốn chỉnh trong cài đặt để không chạm nhầm khi chơi).
- Mọi tính năng của đợt 2, 3, 4.
- Thêm phong cách thứ ba, hay cho người dùng tự tạo phong cách.

## Không được phép (dù vẫn "chạy được")
- Overlay bắt chuột hay chiếm focus của game, kể cả khi cửa sổ cài đặt đang mở.
- Chữ tiếng Việt bị trộn font (một từ có chữ từ hai font khác nhau). Đây là lỗi đã gặp ở mockup đầu tiên với Rajdhani; mọi font dùng cho chữ tiếng Việt phải có đủ bộ ký tự tiếng Việt.
- Đổi cài đặt mà phải khởi động lại app mới thấy.
- File cài đặt hỏng hoặc thiếu làm app không mở được.
- Mất các bản sửa của đợt trước: không chớp `--` giữa các đợt dữ liệu, FPS cũ về `--` khi game treo, hộp lỗi nổi trên game, một bản chạy duy nhất.

## Ràng buộc
- C# / .NET 10 / WPF như hiện tại, giữ kiến trúc tách lõi (`FpsOverlay.Core`, có test) và app.
- **Không dùng WPF-UI** hay thư viện giao diện có sẵn: tự viết kiểu (style/template) cho các control dùng trong cửa sổ cài đặt.
- **Đóng gói font vào app** dưới dạng resource: Chakra Petch và JetBrains Mono (giấy phép SIL OFL, kèm file giấy phép). Người dùng không phải cài font.
- Overlay vẫn chỉ hiện ở màn hình chính (đợt 4 mới làm nhiều màn hình).

## Các cách đã cân nhắc
| Cách | Đánh đổi |
|---|---|
| WPF + thư viện WPF-UI | Control đẹp sẵn kiểu Windows 11, nhưng bo tròn, màu nhẹ; muốn ra Cyberpunk phải ghi đè gần hết, thư viện thành gánh nặng. |
| **WPF thuần, tự tạo kiểu** ✅ (đã chọn) | Không phụ thuộc thư viện; vẽ thẳng phong cách Cyberpunk cho khoảng 6 loại control. Tốn công tạo kiểu hơn. |
| Không có màn hình cài đặt, sửa file cấu hình | Nhanh nhất, nhưng không xem trước được, dễ gõ sai, đi ngược mục tiêu tuỳ chỉnh dễ dàng. |

Người dùng ban đầu chọn WPF-UI, rồi chuyển sang WPF thuần khi chọn phong cách Cyberpunk, vì WPF-UI theo phong cách Windows 11 sẽ cản trở.

## Cách hoạt động
- **Lõi (`FpsOverlay.Core`, có test):**
  - Mô hình cài đặt: phong cách, góc, kích cỡ, độ mờ nền, màu nhãn, màu giá trị, bật đổi màu, mốc xanh, mốc đỏ.
  - Giá trị mặc định cho từng phong cách.
  - Kiểm tra và kẹp giá trị về khoảng hợp lệ (kích cỡ 50–300%, độ mờ 0–100%, màu là mã hex hợp lệ, mốc đỏ < mốc xanh).
  - Đọc/ghi file JSON. File thiếu, hỏng hoặc có giá trị lạ → dùng mặc định cho phần hỏng, app vẫn chạy.
  - Quy tắc mức FPS: từ FPS và hai mốc trả ra mức tốt / trung bình / kém (hoặc "không có dữ liệu" khi `--`).
- **App:** overlay đọc cài đặt hiện tại và vẽ lại ngay khi cài đặt đổi. Phong cách được tách thành bộ tài nguyên riêng (font, màu, hình dạng nền) để chuyển qua lại. Cửa sổ cài đặt sửa cài đặt hiện tại; mỗi lần đổi là lưu.

## Các quyết định do Claude tự đề xuất (người dùng có thể sửa)
- **Cửa sổ cài đặt luôn theo phong cách Cyberpunk**, kể cả khi overlay đang dùng phong cách Cổ điển. Người dùng chỉ xem mockup cài đặt kiểu Cyberpunk và đã duyệt nó.
- **Không có mục chọn font riêng.** Font gắn với phong cách (Cổ điển: Consolas; Cyberpunk: Chakra Petch + JetBrains Mono). Lý do: phong cách Cyberpunk phụ thuộc vào font, và chọn font tự do dễ gặp lại lỗi trộn font tiếng Việt.
- **Ba màu chỉnh được cho mỗi phong cách:** màu nhãn, màu giá trị (cho `--` và các mục của đợt 3), và **màu FPS** (dùng khi tắt đổi màu theo FPS; mặc định Cổ điển xanh lá, Cyberpunk vàng neon). Cửa sổ cài đặt có ba dòng màu tương ứng. Lý do: đợt này FPS là mục duy nhất, nên nếu không có dòng "màu FPS" riêng thì bạn không đổi được màu con số chính.
- **Kích cỡ 100%:** con số FPS cao bằng hiện tại (24px), nhãn nhỏ hơn theo tỉ lệ của mockup (khoảng 3/4). Lý do: giữ cỡ bạn đã quen dùng, còn tỉ lệ và hình dạng theo mockup đã duyệt.
- **Mỗi phong cách giữ bộ màu riêng.** Màu nhãn/màu giá trị bạn chỉnh ở Cyberpunk không ảnh hưởng Cổ điển và ngược lại; chuyển qua lại không mất tuỳ chỉnh. Góc, kích cỡ, độ mờ nền, đổi màu theo FPS và các mốc là chung cho cả hai.
- **"Khôi phục mặc định"** chỉ khôi phục phong cách đang chọn và các cài đặt chung, không đụng bộ màu của phong cách còn lại.
- **Chọn màu:** một bảng khoảng 10 ô màu gợi ý hợp phong cách, kèm ô nhập mã hex. WPF không có sẵn bộ chọn màu, và bảng màu gợi ý giữ được chất của phong cách.
- **Khoảng của mốc màu FPS:** 10 đến 240, mốc đỏ luôn nhỏ hơn mốc xanh (kéo mốc này vượt mốc kia thì mốc kia bị đẩy theo).
- **Lề overlay:** cách mép màn hình 8px theo cả bốn góc, tính trên toàn màn hình chính (không trừ thanh taskbar, vì game toàn màn hình che taskbar).
- **Nơi lưu cài đặt:** `%AppData%\FpsOverlay\settings.json`.
- **Mặc định khi cài đặt lần đầu:** phong cách Cyberpunk, góc trên trái, kích cỡ 100%, độ mờ nền 72%, đổi màu theo FPS bật, ngôn ngữ tiếng Việt.
- **Ngôn ngữ là cài đặt chung**, nằm đầu mục "Giao diện" trong cửa sổ cài đặt; "Khôi phục mặc định" không đổi ngôn ngữ (để người dùng không bị đẩy sang ngôn ngữ mình không đọc được). Chuỗi chữ của hai ngôn ngữ nằm ở một chỗ, có test kiểm tra mỗi chuỗi đều có đủ bản Việt và Anh.
- **Cửa sổ cài đặt:** chỉ mở một cửa sổ; bấm "Cài đặt..." khi đã mở thì đưa nó lên trước. Cửa sổ có thanh tiêu đề tự vẽ kiểu Cyberpunk, kéo được để di chuyển, nút `[ X ]` đóng (giống "Xong").
- **Icon khay:** chữ "FPS" màu vàng neon trên nền đen.

## Để người viết code tự quyết
- Cách tổ chức bộ tài nguyên phong cách (ResourceDictionary, cách hoán đổi lúc chạy).
- Cách cài đặt báo thay đổi cho overlay (sự kiện, binding...).
- Cách vẽ góc vát chéo và hiệu ứng phát sáng sao cho nhẹ (overlay là cửa sổ layered, hiệu ứng nặng có thể tốn GPU).
- Cách tạo icon khay (file `.ico` hay vẽ lúc chạy).
- Giới hạn tần suất ghi file khi kéo thanh trượt (ví dụ chỉ ghi sau khi dừng kéo một lúc).
