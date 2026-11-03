# FPS Overlay: đợt 5, thông số GPU, cảnh báo nhiệt, phong cách mới, bố cục dọc và kéo thả — Thiết kế

## Bối cảnh
FPS Overlay trên master đã có:
- overlay một dòng ngang, hai phong cách (Cổ điển / Cyberpunk);
- FPS, 1% low, GPU/CPU (tải, nhiệt), RAM, mỗi mục một công tắc;
- chọn 4 góc, kích cỡ, độ mờ nền, bộ 3 màu cho mỗi phong cách;
- log mỗi buổi chơi, cài một file, khay hệ thống.

Xem `2026-11-02-fps-overlay-design.md`, `2026-11-02-ui-themes-settings-design.md` và `2026-11-03-stats-logging-install-design.md`.

Người dùng tự viết app để **tuỳ chỉnh theo ý mình** (thích overlay của MSI Afterburner, phong cách Cyberpunk 2077). Máy của họ: laptop Ryzen 7 4800H, GTX 1650 + Radeon tích hợp. Họ chơi game ở chế độ borderless. Đợt này họ chọn bốn phần từ danh sách đề xuất: thêm thông số GPU, cảnh báo nhiệt, phong cách làm sẵn mới, kéo thả vị trí cùng bố cục dọc.

Bản vẽ thử người dùng đã xem và đồng ý nằm trong `.superpowers/brainstorm/392-1793687877/content/` (`styles-layout.html`, `vertical-v2.html`; thư mục này không commit).

## Mục tiêu
Năm phần, làm theo thứ tự: 0 → A → B → C → D.

### 0. Dọn khung overlay (người dùng chọn làm trước)
Mục đích: thêm phong cách hay thông số về sau chỉ là thêm một mô tả. Hai bố cục ngang/dọc dùng chung một nguồn dữ liệu.
- **Bảng mô tả phong cách.** Gom mọi thứ phụ thuộc phong cách (hiện rải trong `switch` ở `OverlayTheme.cs`, `OverlayWindow.xaml.cs`, `StyleColors.DefaultFor`) vào một mô tả cho mỗi phong cách. Mô tả gồm:
  - font nhãn/giá trị;
  - màu nền;
  - bo góc hoặc góc vát;
  - viền (màu, độ dày, chỉ trái hay cả bốn cạnh, phát sáng hay không);
  - hiệu ứng chữ: viền đen (Cổ điển), phát sáng số FPS (Cyberpunk), phát sáng toàn bộ chữ (Terminal);
  - màu 3 mức FPS tốt/vừa/kém;
  - màu "nóng";
  - bộ 3 màu mặc định (nhãn, giá trị, FPS);
  - dải màu mẫu (swatch) trong cài đặt;
  - dấu ngăn giữa các mục (" · " hoặc " | ");
  - dấu nhắc trước mục đầu (chỉ Terminal: "> ");
  - cỡ khoảng cách giữa nhãn và số.

  Phần không phụ thuộc WPF (màu dạng chuỗi hex, ký tự ngăn, dấu nhắc) đặt trong `FpsOverlay.Core` để test được.
- **Danh sách dòng.** Overlay dựng từ một danh sách mục theo thứ tự cố định:
  FPS, 1%, GPU (tải, nhiệt), VRAM, CLK, PWR, FAN, CPU (tải, nhiệt), RAM.
  - Mỗi mục có nhãn và một hoặc hai ô giá trị. Ô thứ hai là nhiệt độ của GPU/CPU, hoặc đơn vị của CLK/PWR/FAN ở bố cục dọc.
  - Hiện/ẩn theo công tắc như hiện nay. Hai bố cục chỉ khác cách xếp, cùng đọc từ danh sách này.
- **Ràng buộc giữ nguyên:**
  - Bố cục ngang ở phong cách Cổ điển và Cyberpunk phải **trông như cũ** (so ảnh chụp trước/sau ở cùng kích cỡ).
  - Đường cập nhật FPS mỗi khung hình vẫn **không cấp phát** và chỉ gán chữ/màu khi chúng đổi, như `SetFps` hiện nay.
  - Số cảm biến vẫn cập nhật mỗi giây và chỉ gán khi đổi.
  - 196 test hiện có vẫn qua (sửa test chỉ khi chính API được đổi tên, không đổi ý nghĩa).

### A. Thêm thông số GPU
- Bốn mục mới, đọc từ **cùng card GPU đang bận nhất** như GPU tải/nhiệt hiện nay (`SensorMath.BusiestGpu`):

  | Nhãn | Ý nghĩa | Hiển thị | Cảm biến LibreHardwareMonitor (thử theo thứ tự) |
  |---|---|---|---|
  | VRAM | bộ nhớ GPU đang dùng | `3.1G` (GB, 1 số lẻ, như RAM) | `SmallData` "GPU Memory Used" (MB) → `SmallData` "D3D Dedicated Memory Used" |
  | CLK | xung nhân | `1590MHz` (ngang) / `1590` + cột `MHz` (dọc) | `Clock` "GPU Core" |
  | PWR | công suất | `48W` / `48` + `W` | `Power` "GPU Package" → "GPU Power" → "GPU Board Power" |
  | FAN | tốc độ quạt | `2400RPM` / `2400` + `RPM` | `Fan` "GPU Fan" hoặc tên bắt đầu bằng "GPU Fan" (lấy cái đầu tiên) |

  Tên cảm biến lấy từ chuỗi trong `LibreHardwareMonitorLib.dll` 0.9.6. Người làm cần chạy thử trên máy thật và in danh sách cảm biến (Trace) để xác nhận.
- **Không đọc được thì `--`.** Laptop GTX 1650 thường không có cảm biến quạt nên FAN có thể luôn là `--`; đó là đúng, không phải lỗi.
  - CLK và PWR có giá trị ≤ 0 thì coi là không đọc được (card rời đang ngủ), giống quy tắc nhiệt độ hiện nay.
  - FAN = 0 là quạt đứng thật, hiện `0RPM`.
- **Mỗi mục một công tắc** trong mục "// THÔNG SỐ" của cài đặt, nằm sau công tắc GPU °C. **Mặc định tắt cả bốn**, để overlay không tự dài ra sau khi cập nhật *(đề xuất của người làm thiết kế, người dùng đã đồng ý)*.
- Màu: dùng màu "giá trị" của phong cách. Không có ngưỡng nóng cho các mục này.
- **Log:** thêm 4 cột vào **cuối** CSV (giữ nguyên thứ tự cột cũ): `GpuMemUsedGb` (F2), `GpuClockMhz` (F0), `GpuPowerW` (F0), `GpuFanRpm` (F0). Ghi không phụ thuộc công tắc hiện/ẩn, như các cột cảm biến hiện nay. File tóm tắt không đổi.
- Xem trước (cửa sổ cài đặt đang mở) dùng số mẫu cố định cho bốn mục mới, ví dụ 3.1G, 1590, 48, 2400.

### B. Cảnh báo nhiệt độ
Người dùng đồng ý cách sau:
- **Ngưỡng tự đặt:** GPU (mặc định 85 °C) và CPU (mặc định 90 °C), khoảng cho phép 50–110 °C. Thay hằng số `HeatLevels.GpuHotCelsius`/`CpuHotCelsius`.
- **Đổi màu:** như hiện nay, số nhiệt chuyển màu "nóng" ngay khi ≥ ngưỡng (so trên giá trị đã làm tròn như hiển thị), nhưng theo ngưỡng tự đặt.
- **Nhấp nháy:** khi nhiệt ≥ ngưỡng **liên tục N giây** (mặc định 10, khoảng 0–60; 0 = ngay), **chỉ số nhiệt đó** nhấp nháy, không phải cả overlay.
  - Nháy là luân phiên hiện/mờ, khoảng 2 lần mỗi giây, tiếp tục cho tới khi hết đợt nóng.
  - Hiện = màu "nóng"; mờ = cùng màu, độ mờ khoảng 25%.
  - Không đổi kích thước overlay khi nháy, nên không được ẩn hẳn chữ theo kiểu làm dải co lại.
- **Một đợt nóng** bắt đầu khi bắt đầu nháy. Đợt kết thúc khi nhiệt xuống **dưới ngưỡng − 3 °C**, hoặc khi không đọc được nhiệt (null). Khoảng đệm 3 °C tránh nháy và bíp liên tục ở sát ngưỡng *(quyết định của người làm thiết kế)*. Đợt đã kết thúc thì phải lại ≥ ngưỡng liên tục N giây mới có đợt mới.
- **Tiếng bíp (tuỳ chọn, mặc định tắt):** phát âm cảnh báo sẵn của Windows (`SystemSounds.Exclamation`) **một lần** lúc mỗi đợt nóng bắt đầu. Không nhúng file âm thanh.
- **Bật/tắt:** công tắc "Nhấp nháy khi quá nóng" (mặc định bật) và "Phát tiếng" (mặc định tắt), dùng chung cho GPU và CPU.
- **Phạm vi** *(quyết định của người làm thiết kế)*:
  - Cảnh báo chỉ chạy khi overlay đang hiện **và** số nhiệt tương ứng đang bật hiển thị. Tắt hiện GPU °C thì không cảnh báo GPU, kể cả tiếng.
  - Overlay ẩn thì không cảnh báo và đợt đang có kết thúc.
  - Trong lúc xem trước (số mẫu) không đánh giá cảnh báo, và đợt đang có **kết thúc** như khi ẩn overlay (bộ theo dõi chỉ cần "đặt lại", không cần "tạm dừng"). Tắt hiện số nhiệt giữa đợt cũng vậy. Quay lại game thì phải chờ đủ N giây mới nháy và bíp lại; chấp nhận được vì cửa sổ cài đặt hiếm khi mở giữa trận *(quyết định của người làm thiết kế)*.
- **Nút "Thử cảnh báo"** trong cài đặt: overlay nháy số nhiệt GPU và CPU trong 3 giây, và phát tiếng nếu công tắc tiếng đang bật. Mục đích là người dùng thấy cảnh báo trông thế nào mà không cần làm máy nóng *(đề xuất của người làm thiết kế)*.
  - Trong 3 giây đó, cả hai số nhiệt **tạm hiện** dù công tắc hiện/ẩn của chúng đang tắt, để luôn có thứ để nhìn.
  - Lúc này overlay đang ở chế độ xem trước (số mẫu), nên nháy trên số mẫu, dùng màu "nóng".
- **Logic thuần trong Core, có test:** một bộ theo dõi cho mỗi cảm biến. Mỗi giây nhận (nhiệt, ngưỡng, N, thời điểm) và trả trạng thái: bình thường / nóng (đổi màu) / đang báo (nháy), kèm cờ "vừa bắt đầu đợt" để phát tiếng.
- Cài đặt nằm trong mục mới "// CẢNH BÁO" (en "// ALERTS"): ngưỡng GPU, ngưỡng CPU, thời gian N, hai công tắc, nút thử. Thanh trượt giống các thanh hiện có.

### C. Hai phong cách làm sẵn mới
Người dùng chọn: **phong cách làm sẵn**, không làm phần tự chỉnh font hay màu viền. Thêm `NeonPurple` và `TerminalGreen` vào `OverlayStyle`. Trong cài đặt có 4 nút: Cổ điển, Cyberpunk, Neon tím (Neon Purple), Terminal xanh (Terminal Green).

| | Neon tím | Terminal xanh |
|---|---|---|
| Nền | `#1A0828` | `#000800`, kèm vạch quét ngang mờ kiểu CRT (vạch tối 1 px, cách 3 px) |
| Viền | 1.5 DIP cả bốn cạnh, `#C64BFF`, **phát sáng** tím ra ngoài; bo góc 10 | không viền, góc vuông |
| Font nhãn / số | **Orbitron** (mới, OFL) / JetBrains Mono | **VT323** (mới, OFL) cho cả hai |
| Hiệu ứng chữ | nhãn và số FPS phát sáng hồng; số phụ không phát sáng | toàn bộ chữ phát sáng xanh nhẹ |
| Màu mặc định (nhãn, giá trị, FPS) | `#FF4FD8`, `#F3E6FF`, `#FF4FD8` | `#1FAA44`, `#33FF66`, `#7CFF9E` |
| FPS tốt / vừa / kém | `#FF4FD8` / `#FFB86C` / `#FF2E4D` | `#7CFF9E` / `#E5FF4F` / `#FF5F45` |
| Màu "nóng" | `#FF2E4D` | `#FF5F45` |
| Dấu ngăn (ngang) | " · " | " \| " |
| Dấu nhắc | không | "> " trước nhãn FPS |

- Màu và hình dáng theo bản vẽ thử người dùng đã xem. Màu mức FPS và màu "nóng" là đề xuất của người làm thiết kế.
- **Màu phát sáng theo màu chữ** *(quyết định của người làm thiết kế)*: chữ phát sáng lấy đúng màu hiện tại của chính chữ đó, kể cả màu mức FPS và màu "nóng". Đây là cách Cyberpunk đang làm với số FPS. Người dùng đổi màu nhãn thì ánh sáng đổi theo. Riêng viền Neon phát sáng theo màu viền cố định `#C64BFF`.
- Cỡ chữ: VT323 nhỏ và hẹp, nên Terminal dùng cỡ lớn hơn (khoảng 1,33 lần, như bản vẽ: số FPS 32, nhãn 24, số phụ 24, nhãn phụ 20) để cao tương đương các phong cách khác. Orbitron rộng, nên nhãn Neon nhỏ hơn khoảng 1 điểm. Cỡ cuối cùng do người làm chỉnh trên app thật.
- Mỗi phong cách mới có **bộ 3 màu riêng, đổi được** bằng ô màu như hiện nay (cài đặt có thêm `NeonPurpleColors`, `TerminalGreenColors`), kèm dải 10 màu mẫu hợp phong cách.
- **Font mới:** Orbitron và VT323, đều giấy phép OFL, kèm file `OFL.txt` của từng font.
  - VT323 lấy từ kho chính thức `google/fonts` trên GitHub (`ofl/vt323`).
  - Orbitron trong `google/fonts` hiện chỉ có bản variable. WPF không đọc trục độ đậm, nên chữ Bold sẽ là đậm giả. Vì vậy cần lấy **bản tĩnh Orbitron Bold** từ kho gốc của font (The League of Moveable Type, `theleagueof/orbitron`) hoặc từ một commit cũ của `google/fonts` còn bản tĩnh. Cùng giấy phép OFL *(quyết định của người làm thiết kế)*.
  - Nếu không lấy được bản tĩnh thì dùng bản variable với đậm giả, và ghi lại trong báo cáo. Đặt vào `Assets/Fonts/` thành `OFL-Orbitron.txt`, `OFL-VT323.txt`, như hai font hiện có. Thêm hai dòng vào `THIRD-PARTY-NOTICES.txt`.
- Cửa sổ cài đặt vẫn giữ giao diện Cyberpunk, không đổi theo phong cách overlay.

### D. Bố cục dọc và kéo thả vị trí
**Bố cục**
- Cài đặt mới `Layout`: **Ngang** (mặc định, như hiện nay) / **Dọc**. Hai nút trong mục "// GIAO DIỆN", cạnh chọn góc.
- **Dọc = bảng gọn** (người dùng chọn phương án A trên bản vẽ):
  - Mỗi mục một dòng, 3 cột: nhãn | giá trị căn phải | ô thứ hai.
  - GPU/CPU: tải ở cột 2, nhiệt ở cột 3. Khi chỉ bật nhiệt thì nhiệt ở cột 2.
  - CLK/PWR/FAN: số ở cột 2, đơn vị (MHz, W, RPM) cỡ nhãn phụ ở cột 3.
  - Dòng FPS dùng cỡ chữ to như hiện nay.
- Ở bố cục dọc không có dấu ngăn. Dấu nhắc "> " của Terminal chỉ ở dòng FPS.
- Viền và vát theo phong cách vẫn bao cả khối: viền trái Cyberpunk cao bằng khối, vát góc phải dưới; viền phát sáng của Neon bao cả khối.
- Kích cỡ (Scale), độ mờ nền, màu, cảnh báo nhấp nháy áp như bố cục ngang.

**Vị trí**
- Bốn nút chọn góc (cài đặt và khay) giữ như cũ. Chọn góc thì đặt lại khoảng cách về mặc định 8 DIP.
- Cài đặt mới: `OffsetX`, `OffsetY` (DIP, ≥ 0, mặc định 8), là khoảng cách từ mép của **góc đang chọn** tới overlay. Thay hằng `ScreenMargin`.
- **Chế độ di chuyển:**
  - Mục mới trong menu khay: "Di chuyển overlay" (en "Move overlay"), có dấu tick khi đang bật. Mục mờ đi khi overlay đang ẩn.
  - Khi bật:
    - overlay bỏ tạm cờ bấm xuyên (`WS_EX_TRANSPARENT`), nhận chuột, con trỏ thành mũi tên 4 chiều;
    - hiện viền đứt nét quanh overlay để biết đang ở chế độ này;
    - kéo bằng chuột trái (cơ chế kéo cửa sổ sẵn của Windows);
    - việc tự đặt vị trí mỗi giây (`UpdatePosition` trong `KeepOnTop`) tạm dừng.
  - **Kết thúc:**
    - bấm lại mục ở khay, nhấn **Enter**, hoặc bấm đúp vào overlay: lưu vị trí;
    - nhấn **Esc**: huỷ, về vị trí cũ.

    Để nhận phím, overlay được phép nhận focus trong chế độ này: tạm bỏ `WS_EX_NOACTIVATE` và kích hoạt cửa sổ khi bật.
  - Kết thúc bằng cách nào cũng trả lại đủ cờ bấm xuyên và không chiếm focus. Ẩn overlay hoặc mở cài đặt khi đang di chuyển thì coi như **lưu**.
  - **Focus và màn hình khi di chuyển** *(quyết định của người làm thiết kế)*:
    - Khi bật chế độ di chuyển, nhớ cửa sổ đang ở phía trước (thường là game). Khi kết thúc, cố gắng trả focus về cửa sổ đó (`SetForegroundWindow`). Windows chặn thì thôi, người dùng tự bấm lại vào game.
    - Trong lúc di chuyển, overlay ở yên trên màn hình nó đang đứng, không nhảy về màn hình chính dù chính app đang ở phía trước.
    - Kéo overlay sang màn hình khác với game thì chỉ góc và khoảng cách được lưu. Lần đặt vị trí sau, overlay vẫn về màn hình có game (không có vị trí riêng cho từng màn hình, xem "Không làm").
- **Lưu theo góc gần nhất:**
  - Khi lưu, lấy màn hình chứa tâm overlay và chọn góc gần tâm overlay nhất (so tâm overlay với tâm màn hình theo từng trục).
  - Khoảng cách từ hai mép của góc đó tới overlay, đổi ra DIP theo DPI của màn hình đó, thành `OffsetX/OffsetY`. Ghi `Corner` theo góc mới.
  - Nhờ vậy khi game ở màn hình khác hoặc đổi độ phân giải, overlay vẫn bám đúng góc với cùng khoảng cách.
- **Không ra ngoài màn hình:** khi đặt vị trí, kẹp để overlay nằm trọn trong màn hình (cả bốn mép). Hiện code chỉ kẹp trái/trên.
- Toàn bộ phép tính góc gần nhất, offset ↔ toạ độ và kẹp nằm trong `OverlayPlacement` (Core) và có test.

## Cài đặt mới (tóm tắt)
| Thuộc tính | Mặc định | Khoảng |
|---|---|---|
| `Layout` | `Horizontal` | Horizontal / Vertical |
| `OffsetX`, `OffsetY` | 8 | 0–4000 DIP (kẹp thật theo màn hình lúc đặt) |
| `ShowGpuMemory`, `ShowGpuClock`, `ShowGpuPower`, `ShowGpuFan` | false | |
| `GpuHotThreshold` | 85 | 50–110 |
| `CpuHotThreshold` | 90 | 50–110 |
| `HotAlertSeconds` | 10 | 0–60 |
| `HotAlertBlink` | true | |
| `HotAlertSound` | false | |
| `NeonPurpleColors`, `TerminalGreenColors` | theo bảng phần C | |

- `SettingsStore.Load` đọc thêm các trường này; file cũ thiếu trường thì lấy mặc định. `Normalize` kẹp mọi số.
- **Khôi phục mặc định** đặt lại mọi thứ ở trên. Riêng bộ màu thì như hiện nay: chỉ đặt lại bộ màu của phong cách đang chọn, giữ bộ màu của ba phong cách kia. Vẫn giữ ngôn ngữ, phong cách, ghi log, cờ PawnIO.
- Mọi chuỗi mới có đủ tiếng Việt và tiếng Anh trong `Loc`. Nhãn thông số (VRAM, CLK, PWR, FAN, MHz, W, RPM) giống nhau ở hai ngôn ngữ.

## Khác
- Tăng `<Version>` trong csproj lên **1.1.0**, để file mang đi nhận ra là bản mới và cập nhật bản đã cài.
- Bản vẽ thử dùng font web. App thật dùng font nhúng, nên người làm so bằng mắt với bản vẽ, không cần khớp từng pixel.

## Không làm
- Tự chọn font hay màu viền phát sáng (người dùng chọn chỉ làm phong cách sẵn).
- Biểu đồ FPS/frametime (vẫn hoãn).
- Phím tắt, kể cả phím bật chế độ di chuyển (người dùng đã bỏ phím tắt từ đợt 2).
- Cảnh báo cho thông số khác (tụt FPS, VRAM đầy...), nhiều kiểu âm thanh, âm thanh tự chọn.
- Ngưỡng nóng cho VRAM, CLK, PWR, FAN.
- Thêm thông số mới vào file tóm tắt buổi chơi.
- Hồ sơ riêng cho từng game; vị trí riêng cho từng màn hình.
- Đổi giao diện cửa sổ cài đặt theo phong cách overlay.

## Ràng buộc
- C# / .NET 10, WPF; giữ cấu trúc `FpsOverlay.Core` (logic thuần, có test) và `FpsOverlay` (WPF).
- `dotnet build src/FpsOverlay` 0 cảnh báo; mọi test qua. Logic mới trong Core có test: bảng phong cách, thứ tự và hiện/ẩn dòng, định dạng số mới, bộ theo dõi cảnh báo, góc gần nhất và offset, đọc/ghi và Normalize các cài đặt mới, cột log mới.
- Overlay ngoài chế độ di chuyển: vẫn không bắt chuột, không chiếm focus, không chớp `--`.
- Cảm biến vẫn đọc trên luồng nền mỗi giây. Đọc thêm bốn cảm biến không được làm chậm luồng UI. Mọi ngoại lệ từ thư viện vẫn bị nuốt, số thành `--`.
- Không cần thêm quyền hay driver mới.

## Phần để người làm quyết định
- Cách dựng hai bố cục trong WPF từ danh sách dòng, miễn đạt ràng buộc "trông như cũ" và "không cấp phát mỗi khung hình". Ví dụ: một dải Run cho ngang và một Grid cho dọc, cùng dùng lại đối tượng chữ của từng mục.
- Cách vẽ viền phát sáng của Neon (ví dụ một Border riêng phía sau có hiệu ứng, để chữ không bị nhoè) và vạch CRT của Terminal (ví dụ DrawingBrush lặp).
- Cỡ chữ cụ thể của hai phong cách mới.
- Bố cục các control mới trong cửa sổ cài đặt, theo kiểu các mục hiện có.
- Dải 10 màu mẫu của hai phong cách mới.
