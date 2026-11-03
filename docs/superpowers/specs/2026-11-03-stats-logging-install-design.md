# FPS Overlay: đợt 3–4, thông số, log, nhiều màn hình, cài đặt một file — Thiết kế

## Bối cảnh
FPS Overlay đã có (trên master): overlay một dòng hai phong cách (Cổ điển / Cyberpunk), cửa sổ cài đặt Cyberpunk, tiếng Việt/Anh, menu khay, tự khởi động cùng Windows qua Task Scheduler, đo game Store/UWP. Xem `2026-11-02-fps-overlay-design.md` và `2026-11-02-ui-themes-settings-design.md`.

Người dùng muốn làm hết phần còn lại trong danh sách đề xuất ban đầu, cộng thêm việc mang app sang máy khác chỉ bằng một file. Máy của họ: laptop Ryzen 7 4800H, GTX 1650 + Radeon tích hợp, một màn hình. Họ chơi game Steam và Game Pass ở chế độ borderless. Lý do họ tự viết app là để **tuỳ chỉnh theo ý mình**.

## Mục tiêu
Năm phần, làm theo thứ tự:

### 1. Báo lỗi khi PresentMon không gửi dữ liệu
- Nếu PresentMon đã chạy hơn **10 giây** mà không gửi khung hình nào của bất kỳ tiến trình nào (kể cả màn hình Windows), menu khay hiện **một dòng cảnh báo** (mục không bấm được, đầu menu) kèm lý do ngắn, và tooltip khay đổi theo. **Không hiện hộp thông báo** (không làm phiền lúc chơi).
- Có dữ liệu trở lại thì cảnh báo tự biến mất.
- Trường hợp PresentMon thoát hẳn vẫn giữ như cũ (hộp lỗi rồi thoát app).

### 2. Tiết kiệm tài nguyên khi ẩn FPS
- Khi ẩn overlay: gỡ hẳn việc cập nhật theo từng khung hình của WPF (không chỉ bỏ qua bên trong).
- Nếu **ghi log đang bật**: PresentMon và việc đọc cảm biến **vẫn chạy** (log cần cả FPS lẫn nhiệt độ, không được đứt quãng). Nếu ghi log tắt: dừng PresentMon và ngừng đọc cảm biến; hiện lại thì chạy lại. *(Quyết định của người làm thiết kế: người dùng nói "ngừng đọc cảm biến khi ẩn", nhưng log có cột nhiệt độ nên khi log bật thì cảm biến phải chạy tiếp.)*

### 3. Thông số trên overlay
Vẫn **một dòng ngang** (người dùng chọn). Mẫu:

`FPS 144 · 1% 98 · GPU 67% 71° · CPU 34% 65° · RAM 9.2G`

- Thứ tự cố định: FPS, 1% low, GPU (mức dùng, nhiệt độ), CPU (mức dùng, nhiệt độ), RAM (đã dùng, GB, một chữ số thập phân).
- Nhãn giống nhau ở cả hai ngôn ngữ (như FPS hiện nay).
- **Mỗi thông số có công tắc riêng** trong cửa sổ cài đặt (một mục mới "// THÔNG SỐ"): 1% low, GPU %, GPU °C, CPU %, CPU °C, RAM. FPS luôn hiện. Tắt mục nào thì mục đó biến khỏi dòng; tắt cả hai số của GPU thì nhãn GPU cũng biến (CPU tương tự). Mặc định: bật hết.
- Màu:
  - Đổi màu theo FPS áp cho **FPS và 1% low** (cùng mốc, cùng quy tắc làm tròn).
  - Các số khác dùng màu "giá trị" của phong cách.
  - Nhiệt độ chuyển **đỏ của phong cách** khi nóng: GPU ≥ 85°C, CPU ≥ 90°C (hằng số, không chỉnh trong cài đặt).
  - Nhãn GPU/CPU/RAM/1% dùng màu nhãn như nhãn FPS.
- Không có dữ liệu (cảm biến không đọc được, chưa cài PawnIO, máy không có card đó...) thì số đó hiện `--`, các số khác vẫn chạy.
- **1% low** trên overlay: tính trên **10 giây gần nhất** của tiến trình đang đo FPS, cập nhật mỗi giây. Định nghĩa: lấy thời gian giữa các khung hình liên tiếp của tiến trình đó, lấy giá trị ở phân vị 99 (khung chậm thứ 1%), đổi ra FPS (`1 / frametime`). Ít hơn 2 giây dữ liệu thì `--`.
- **GPU** hiện là GPU đang có mức dùng cao nhất (laptop hai GPU: thường là card rời khi chơi game). Nhiệt độ là nhiệt độ lõi của chính GPU đó.
- **Cảm biến đọc mỗi giây một lần trên luồng riêng.** Việc cập nhật FPS theo từng khung hình không được chậm lại hay cấp phát thêm vì phần này.
- Dùng thư viện **LibreHardwareMonitorLib 0.9.6** (NuGet) cho GPU và CPU (mức dùng và nhiệt độ). RAM đọc thẳng từ Windows. Thư viện lỗi hay ném ngoại lệ thì số tương ứng hiện `--`, app không được sập.

### 4. Ghi log FPS ra file
Người dùng chọn: **tự ghi mỗi khi vào game, dừng khi thoát game, mỗi buổi chơi một file.**
- **Thế nào là đang chơi game:** cửa sổ phía trước **phủ kín cả màn hình chứa nó** (fullscreen hoặc borderless) và tiến trình đang đo **liên tục có FPS hơn 10 giây**. Cách này loại trình duyệt và video không toàn màn hình.
- **Buổi chơi kết thúc** khi tiến trình game thoát, hoặc khi game không còn ở phía trước / không có FPS **quá 60 giây**. Alt-tab ra dưới 1 phút vẫn là cùng một buổi (các giây đó ghi trống số FPS).
- Đang trong 60 giây chờ mà **một game khác** đạt điều kiện "đang chơi" thì đóng ngay buổi cũ (ghi tóm tắt) và mở buổi mới cho game đó.
- **File:** `Documents\FpsOverlay\Logs\<TênTiếnTrình>_<yyyy-MM-dd_HH-mm>.csv` (tên tiến trình bỏ ký tự không hợp lệ trong tên file).
  - Mỗi giây một dòng. Cột: `Time` (giờ địa phương ISO 8601), `Fps`, `OnePercentLow`, `GpuLoad`, `GpuTemp`, `CpuLoad`, `CpuTemp`, `RamUsedGb`. `Fps` là số khung hình của game trong đúng giây đó; `OnePercentLow` là giá trị 10 giây gần nhất giống trên overlay; các cột cảm biến là lần đọc gần nhất. Số dùng dấu chấm thập phân (văn hoá bất biến). Không có giá trị thì để trống.
  - Ghi ra đĩa theo lô (ví dụ mỗi 10 giây) trên luồng nền; app tắt đột ngột thì mất tối đa vài giây cuối, phần trước vẫn dùng được.
  - Mở được bằng Excel.
- **Tóm tắt cuối buổi:** file `<cùng tên>_summary.txt` cạnh file CSV: thời lượng, FPS trung bình cả buổi, 1% low cả buổi (cùng định nghĩa phân vị 99, trên toàn bộ khung hình của buổi), FPS thấp nhất/cao nhất theo giây, nhiệt độ GPU/CPU cao nhất. Viết bằng ngôn ngữ app đang chọn.
- **Công tắc "Ghi log FPS"** trong cài đặt (mục "// HỆ THỐNG"), mặc định bật. Có nút/đường dẫn mở thư mục log.
- Cửa sổ cài đặt đang mở (số mẫu) không ảnh hưởng log, vì lúc đó game không ở phía trước.

### 5. Nhiều màn hình
- Overlay **tự sang màn hình đang chứa cửa sổ phía trước** (game), ở đúng góc người dùng chọn, cách mép 8 DIP như hiện nay. Không có cửa sổ phía trước (desktop) thì về màn hình chính.
- Các màn hình có DPI khác nhau thì overlay vẫn đúng cỡ và đúng góc trên màn hình đó.

### 6. Một file mang đi máy khác, tự cài
Người dùng muốn: **mang sang máy khác chỉ cần chạy một file, các thư viện và công cụ tự cài**, và chọn **tự cài vào `C:\Program Files\FpsOverlay`, tạo lối tắt trong Start Menu, từ đó chạy từ chỗ đã cài** (an toàn hơn cho "Khởi động cùng Windows").
- **Một file `FpsOverlay.exe`** tự chứa: .NET 10, LibreHardwareMonitorLib, PresentMon.exe, font, giấy phép font (OFL) và giấy phép của các thư viện kèm theo. Máy mới không cần cài .NET trước.
- **Chạy file từ chỗ khác `C:\Program Files\FpsOverlay`:**
  - Hỏi bằng một hộp thoại kiểu Cyberpunk: cài vào máy này.
  - Cài: chép exe vào `C:\Program Files\FpsOverlay\`, bung PresentMon.exe và các file giấy phép vào đó, tạo lối tắt "FPS Overlay" trong Start Menu (cho mọi người dùng), đăng ký mục gỡ cài đặt trong Settings → Apps (tên, phiên bản, biểu tượng, lệnh gỡ). Rồi chạy bản đã cài và thoát bản đang chạy.
  - Đã có bản cài: nếu file đang chạy **mới hơn** (theo phiên bản), hoặc **cùng phiên bản nhưng khác nội dung** (so mã băm), thì cập nhật bản cài rồi chạy nó; cũ hơn hoặc giống hệt thì chạy luôn bản đã cài, không hỏi. Phiên bản lấy từ `<Version>` trong csproj, tăng tay mỗi lần phát hành (bắt đầu 1.0.0).
  - Cài hoặc cập nhật xong mà tác vụ "Khởi động cùng Windows" đang có thì trỏ lại nó vào `C:\Program Files\FpsOverlay\FpsOverlay.exe` (tác vụ hiện tại trên máy người dùng đang trỏ vào thư mục build).
  - Bản cài đang chạy thì báo người dùng thoát nó trước (qua thông báo, không tự giết).
  - Từ chối cài thì thoát (không có chế độ chạy tại chỗ cho người dùng).
- **Chạy từ chỗ đã cài:** chạy bình thường. Nếu PresentMon.exe thiếu hoặc khác bản kèm trong exe thì bung lại.
- **Gỡ cài đặt** (từ Settings → Apps): hỏi xác nhận; xoá tác vụ khởi động cùng Windows, lối tắt, mục đăng ký, thư mục `C:\Program Files\FpsOverlay`. **Giữ lại** cài đặt (`%AppData%\FpsOverlay`) và log (`Documents\FpsOverlay`) vì đó là dữ liệu của người dùng. Không gỡ PawnIO (nó là phần mềm riêng, có mục gỡ riêng).
- **Lối thoát cho lúc phát triển:** tham số dòng lệnh `--no-install` chạy tại chỗ, không cài (dùng khi build/debug). Ở chế độ này **"Khởi động cùng Windows" bị khoá** (ô tick mờ, không tạo, không sửa tác vụ), vì exe nằm ở chỗ người dùng ghi được. Nói chung: chỉ bản chạy từ `C:\Program Files\FpsOverlay` mới được tạo hoặc sửa tác vụ.
- **PawnIO** (driver để đọc nhiệt độ CPU; người dùng đồng ý dùng):
  - Không đóng gói kèm (giấy phép không ghi rõ cho phép đóng gói lại).
  - **Hỏi ở hộp thoại cài/cập nhật** (lúc người dùng đang ngồi trước máy, không phải lúc mở game): nếu máy chưa có PawnIO, hộp thoại có thêm ô tick "Cài PawnIO để đọc nhiệt độ CPU", mặc định bỏ trống (người dùng tự tick). Không bao giờ hỏi lúc app tự khởi động cùng Windows. Đồng ý: tải `https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe` vào thư mục chỉ Admin ghi được, **kiểm tra chữ ký số hợp lệ và đúng người ký của PawnIO**, chạy `-install -silent`, xử lý mã "cần khởi động lại". Xong thì khởi tạo lại phần đọc cảm biến (không cần khởi động lại app nếu driver đã chạy).
  - Không tick: không hỏi lại ở các lần cập nhật sau (nhớ trong file cài đặt ở `%AppData%`, file này còn nguyên khi cài đè). Trong cài đặt luôn có nút "Cài PawnIO" để cài sau, và dòng ghi chú khi chưa có PawnIO.
  - Không có mạng, tải lỗi, chữ ký sai: báo lỗi ngắn, xoá file tải về, nhiệt độ CPU vẫn `--`.

## Không làm
- Biểu đồ FPS/frametime (người dùng hoãn).
- Phím tắt (người dùng bỏ).
- Chọn font tuỳ ý, chỉnh mốc nhiệt độ nóng trong cài đặt.
- Overlay nhiều dòng.
- Chế độ chạy tại chỗ không cài cho người dùng (chỉ có `--no-install` cho lúc phát triển).
- Tự cập nhật qua mạng.
- Gỡ PawnIO cùng app.

## Không được phép (dù vẫn "chạy được")
- Cài PawnIO hay bất kỳ driver nào mà không hỏi người dùng.
- Chạy với quyền Admin bất kỳ file nào nằm ở chỗ người dùng thường ghi được (thư mục tạm, Downloads, AppData): PresentMon, bộ cài PawnIO, bản exe được Task Scheduler chạy đều phải nằm trong thư mục chỉ Admin ghi được lúc chạy.
- Overlay khựng hay chậm cập nhật FPS vì đọc cảm biến, ghi log, tải PawnIO.
- Thư viện cảm biến lỗi làm sập app hoặc làm mất FPS.
- File log bị hỏng cả file khi app tắt đột ngột.
- Mất các điểm đã có: overlay không bắt chuột/không chiếm focus, không chớp `--`, một bản chạy duy nhất, tiếng Việt/Anh cho mọi chữ mới (cả menu, cảnh báo, hộp thoại cài/gỡ, file tóm tắt), thay đổi cài đặt thấy ngay.
- Cài đè làm mất cài đặt hay tác vụ khởi động cùng Windows của người dùng (tác vụ đã trỏ sẵn đúng `C:\Program Files\FpsOverlay\FpsOverlay.exe`).

## Ràng buộc
- C# / .NET 10 / WPF, giữ tách lõi `FpsOverlay.Core` (có test) và app. Logic thuần (1% low, nhận biết buổi chơi, định dạng dòng overlay, CSV, tóm tắt, so sánh phiên bản) nằm trong Core và có test.
- Đóng gói: self-contained, single-file, win-x64.
- Thêm phụ thuộc: chỉ LibreHardwareMonitorLib (và các phụ thuộc của nó). Không thêm thư viện giao diện.
- App vẫn chạy với quyền Admin (`requireAdministrator`).

## Các cách đã cân nhắc
| Cách | Đánh đổi | Kết quả |
|---|---|---|
| **A. LibreHardwareMonitor cho GPU + CPU, RAM từ Windows** | Một thư viện lo mọi hãng (NVIDIA, AMD, Intel); nặng thêm vài MB, khởi tạo 1–2 giây (chạy nền) | **Người dùng chọn** |
| B. Tự đọc từng nguồn (NVML cho NVIDIA, Windows cho CPU/RAM), LHM chỉ cho nhiệt độ CPU | Nhẹ hơn chút nhưng vẫn phải kèm LHM; phải tự viết cho từng hãng GPU nếu mang sang máy khác | Loại |
| C. Tách phần cảm biến ra chương trình riêng | Cách ly lỗi nhưng phức tạp gấp đôi | Loại |

Về "một file": bung PresentMon/PawnIO vào thư mục tạm của người dùng bị loại vì app chạy với quyền Admin (chương trình thường có thể tráo file). Gói kèm PawnIO bị loại vì giấy phép không rõ; tải về lúc cần, có kiểm tra chữ ký.

## Phần người dùng đã quyết
- Một dòng ngang; mỗi thông số bật/tắt riêng.
- Cài PawnIO để có nhiệt độ CPU.
- Log tự động theo buổi chơi, mỗi buổi một file.
- Cách A.
- Một file tự cài vào `C:\Program Files\FpsOverlay`, có lối tắt Start Menu.
- Đồng ý các đề xuất: nhận biết game (phủ màn hình + FPS > 10 giây), alt-tab dưới 1 phút cùng buổi, CSV mỗi giây + tóm tắt, công tắc tắt log, 1% low 10 giây trên overlay và cả buổi trong tóm tắt, overlay theo màn hình có game, cảnh báo PresentMon ở menu khay, khi ẩn thì ngừng cập nhật và đọc cảm biến (PresentMon chạy tiếp nếu log bật).

## Phần để người làm quyết định
- Bố cục cụ thể các hàng mới trong cửa sổ cài đặt (theo phong cách hiện có).
- Cách lấy khung hình cho 1% low (mở rộng `FpsCalculator` hay bộ đếm riêng), miễn không làm chậm đường cập nhật từng khung hình.
- Cách tạo lối tắt và mục gỡ cài đặt (COM `IShellLink`, registry `HKLM\...\Uninstall`).
- Cách xoá exe đang chạy khi gỡ.
- Cách báo bản cài đang chạy thoát khi cập nhật.
- Người ký chính xác của bộ cài PawnIO để kiểm tra (tra lúc làm, ghi cứng vào code).

## Quyết định của người làm thiết kế (sau khi reviewer đọc thử)
Các điểm sau người dùng chưa nói tới; đây là lựa chọn của người làm thiết kế, người dùng có thể đổi:
- Ẩn overlay mà log đang bật thì cảm biến vẫn đọc.
- Bản chạy ngoài `C:\Program Files\FpsOverlay` (`--no-install`) không được đụng tới tác vụ khởi động cùng Windows; cài/cập nhật thì trỏ tác vụ cũ về bản đã cài.
- Hỏi cài PawnIO bằng ô tick trong hộp thoại cài/cập nhật, mặc định bỏ trống.
- Phiên bản tăng tay; cùng phiên bản mà khác nội dung vẫn cập nhật.
- Cột `Fps` trong CSV là số khung hình trong đúng giây đó; `OnePercentLow` giống overlay (10 giây).
- Chuyển thẳng sang game khác thì đóng buổi cũ, mở buổi mới.

## Rủi ro còn lại (đã chấp nhận)
Hai điểm sau trái tinh thần mục "Không được phép" (chạy quyền Admin mà nạp thứ người dùng thường ghi được) nhưng không sửa được trong phạm vi app; đã chấp nhận:
- **Biến môi trường profiler của .NET** (`CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, `CORECLR_PROFILER_PATH`, và dạng `DOTNET_...` tương ứng) đặt ở `HKCU\Environment` (người dùng thường ghi được) làm runtime nạp dll của profiler vào tiến trình Admin, kể cả bản cài do tác vụ khởi động cùng Windows chạy. Runtime đọc các biến này trước khi code của app chạy nên không chặn được từ bên trong; bọc app trong một lệnh cmd xoá biến rồi mới chạy cũng không được chọn (thêm một lớp chạy quyền Admin, dễ sai, tác vụ khởi động phải trỏ thẳng vào exe). `DOTNET_STARTUP_HOOKS` thì đã tắt (`StartupHookSupport=false`, runtimeconfig có `System.StartupHookProvider.IsSupported: false`, áp cả bản cài).
- **Bản mang đi (exe single-file) bung thư viện native vào `%TEMP%`** lúc chạy (`IncludeNativeLibrariesForSelfExtract`), với quyền Admin, khi cài/cập nhật hoặc chạy `--no-install`: chương trình thường có thể tráo dll trong thư mục đó. Bản cài nhiều file trong `C:\Program Files\FpsOverlay` (bản tác vụ khởi động chạy) không bung gì; chỉ bản người dùng tự mở (và tự xác nhận UAC) còn rủi ro này.
