# FPS Overlay: đợt 6, "Chỉ hiện khi chơi game" — Thiết kế ngắn

## Vì sao
Khi không chơi game, overlay vẫn nằm trên desktop. Lúc đó FPS là `--` hoặc là số của ứng dụng thường, nên overlay chủ yếu chiếm chỗ trên màn hình. Người dùng muốn overlay **tự ẩn khi không chơi game** và **hiện ngay khi vào game**, kể cả game chạy ở chế độ cửa sổ.

## Mục tiêu
- Tuỳ chọn mới **"Chỉ hiện khi chơi game"** (en "Only show while gaming"): một công tắc trong "// GIAO DIỆN", mặc định **bật**.
- Khi bật, overlay hiện khi cửa sổ phía trước là game. Bỏ qua cửa sổ của chính app, desktop và shell (`Progman`, `WorkerW`, shell window). Một cửa sổ là game khi thoả một trong ba điều kiện:
  1. cửa sổ **phủ kín màn hình** chứa nó (cùng phép kiểm đang dùng cho log: `SessionLogger.ForegroundCoversMonitor`);
  2. file `.exe` của tiến trình sở hữu cửa sổ nằm trong thư mục game: đường dẫn chứa `\steamapps\common\`, `\XboxGames\` hoặc `\WindowsApps\` (không phân biệt hoa thường);
  3. tên `.exe` có trong danh sách **"Luôn hiện"**.

  Danh sách **"Không bao giờ hiện"** thắng cả ba điều kiện.
- Game Store/UWP cũ chạy trong khung `ApplicationFrameHost`. Với những cửa sổ này, điều kiện 2 và 3 xét **mọi** tiến trình trong `ForegroundProcess.GetCandidates()`, không chỉ tiến trình khung. Danh sách "Không bao giờ hiện" cũng xét theo danh sách này: chỉ cần một tiến trình bị cấm là cấm.
- **Hiện/ẩn gần như ngay:** kiểm mỗi **250 ms** bằng một `DispatcherTimer` (người dùng chọn cách 1). Nhớ đường dẫn exe theo PID, nên mỗi nhịp chỉ tốn vài lệnh Windows nhẹ.
- **Danh sách tự quản lý:**
  - Menu khay có hai mục mới, áp cho **cửa sổ bên ngoài gần nhất** trước khi mở menu (dùng `_lastExternalForeground` có sẵn trong `OverlayWindow`):
    - "Luôn hiện cho `<tên.exe>`";
    - "Không bao giờ hiện cho `<tên.exe>`".

    Chưa có cửa sổ nào thì hai mục mờ đi. Thêm vào danh sách này thì tự xoá khỏi danh sách kia.
  - Cài đặt có danh sách hai nhóm, mỗi dòng gồm tên exe và nút xoá. Lưu theo **tên file exe**, không phân biệt hoa thường, trong `settings.json`.
- **Ưu tiên** (cao xuống thấp):
  1. "Ẩn FPS" bằng tay → ẩn (giữ như cũ).
  2. Chế độ di chuyển đang bật → hiện. Mục "Di chuyển overlay" dùng được cả khi overlay đang tự ẩn; nó chỉ mờ khi người dùng đã ẩn bằng tay.
  3. Cửa sổ cài đặt đang được xem trước → hiện.
  4. Tuỳ chọn tắt → hiện.
  5. Đúng là game → hiện; không phải → ẩn.
- **Tài nguyên** (người dùng chấp nhận chờ vài giây):
  - Overlay tự ẩn và log tắt: dừng PresentMon và cảm biến, giống "Ẩn FPS".
  - Log bật: PresentMon và cảm biến vẫn chạy như hiện nay.
  - Khi overlay hiện lại sau lúc PresentMon đã dừng, FPS có thể là `--` khoảng 1–3 giây.
- **Cảnh báo nhiệt:** overlay tự ẩn được xử lý như ẩn thường: đợt nóng kết thúc, hết nháy (đã đúng theo đợt 5).
- **Log không đổi.**
- **Ẩn/hiện tự động không đổi chữ "Ẩn FPS"/"Hiện FPS" ở khay.** Chữ đó phản ánh lựa chọn bằng tay của người dùng.

## Gọn cửa sổ cài đặt (người dùng đề xuất, đã đồng ý)
- **Chọn thông số:** trong "// THÔNG SỐ", 10 công tắc thông số (1%, GPU %, GPU °C, VRAM, CLK, PWR, FAN, CPU %, CPU °C, RAM) được thay bằng:
  - một nút **"Chọn thông số hiển thị ▾"** (en "Choose stats ▾"), cạnh nút có dòng tóm tắt "Đang hiện N/10" (en "Showing N/10");
  - bấm nút thì **mở ngay bên dưới** một ô gồm 10 **checkbox**, xếp **2 cột**, theo nhóm: 1%; GPU (tải, nhiệt, VRAM, xung, công suất, quạt); CPU (tải, nhiệt); RAM;
  - bấm lại thì thu gọn. Mỗi lần mở cửa sổ cài đặt, ô bắt đầu ở trạng thái **thu gọn**.
- Tick hay bỏ tick có tác dụng ngay, như công tắc hiện nay. Tên thuộc tính cài đặt giữ nguyên.
- Ghi chú PawnIO và nút "Cài PawnIO" vẫn hiện dưới nút khi chưa đọc được nhiệt độ CPU, **kể cả khi ô đang thu gọn**.
- **Hai danh sách "Luôn hiện / Không bao giờ hiện"** dùng cùng kiểu: nút "Danh sách ứng dụng ▾" kèm tóm tắt số mục, bấm thì mở tại chỗ.
- **Không dùng tab riêng hay cửa sổ bật lên**, để vẫn thấy overlay xem trước đổi khi tick.

## Không làm
- Đoán game theo mức dùng GPU.
- Nhận diện Epic, GOG, giả lập theo thư mục (người dùng tự thêm vào "Luôn hiện").
- Hồ sơ cài đặt riêng cho từng game.
- Nghe sự kiện Windows (`SetWinEventHook`).

## Cách làm đã chọn
1. **Kiểm định kỳ 250 ms** (chọn): đơn giản, bắt được cả lúc game chuyển sang toàn màn hình, trễ tối đa ¼ giây.
2. Nghe sự kiện `SetWinEventHook` (không chọn): trễ gần 0, nhưng vẫn cần nhịp kiểm phụ cho lúc đổi kích thước, và code phức tạp hơn.

## Ràng buộc
- Logic "có phải game không" là hàm thuần trong `FpsOverlay.Core`, có test: thư mục game, so tên không phân biệt hoa thường, thứ tự ưu tiên danh sách, nhiều tiến trình ứng viên.
- Cài đặt mới:
  - `ShowOnlyInGame` (true);
  - `AlwaysShowApps`, `NeverShowApps` (danh sách chuỗi, mặc định rỗng).

  Các trường này có trong `Normalize`, `SettingsStore.Load` và `Defaults`. `Normalize` bỏ dòng rỗng và trùng, và một tên không được nằm ở cả hai danh sách. Tên có ở cả hai thì giữ ở "Không bao giờ hiện".
- **Khôi phục mặc định** không xoá hai danh sách: đó là dữ liệu người dùng, như cờ ghi log.
- Mọi chữ mới có đủ tiếng Việt và tiếng Anh trong `Loc`.
- Ngoài lúc ẩn/hiện, overlay vẫn bấm xuyên và không chiếm focus.
- Đường cập nhật FPS mỗi khung hình vẫn không cấp phát.
- Build 0 cảnh báo, mọi test qua. Tăng `<Version>` lên `1.2.0`.

## Để người làm quyết định
- Tên file và tên lớp trong Core.
- Cách lấy đường dẫn exe (`QueryFullProcessImageName` với `PROCESS_QUERY_LIMITED_INFORMATION`); lỗi thì coi là không nằm trong thư mục game.
- Bố cục danh sách trong cửa sổ cài đặt, theo kiểu Cyberpunk hiện có.
