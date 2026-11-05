# EzFPS

**Tiếng Việt** | [English](#english)

## Tiếng Việt

EzFPS là app Windows hiện FPS và các thông số máy (1% low, tải và nhiệt độ GPU/CPU, RAM, VRAM, xung nhân, công suất, quạt GPU) ngay trên game, giống overlay của MSI Afterburner nhưng tuỳ chỉnh theo ý mình.

- Nhiều phong cách: Cổ điển, Cyberpunk, Neon tím, Terminal xanh; bố cục ngang hoặc dọc; chỉnh cỡ, độ mờ, màu, màu theo FPS. Cửa sổ cài đặt, cài đặt app và cập nhật cũng đổi theo phong cách đã chọn.
- Cảnh báo quá nhiệt GPU/CPU (nháy và bíp).
- Ghi log mỗi buổi chơi (CSV mỗi giây và file tóm tắt) vào `Documents\FpsOverlay\Logs`.
- Chỉ hiện khi chơi game (mặc định bật): game toàn màn hình/borderless, game trong thư mục Steam hoặc Xbox/Game Pass, hoặc ứng dụng bạn chọn "Luôn hiện".
- Đo FPS bằng PresentMon, cảm biến bằng LibreHardwareMonitor. Có tiếng Việt và tiếng Anh.

### Yêu cầu
- Windows 10 hoặc 11, 64-bit.
- Quyền Administrator (PresentMon cần ETW). Không cần cài .NET riêng.

### Cài đặt (một dòng)
Mở **Command Prompt** (cmd), dán lệnh sau rồi Enter:

```
curl -fL -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
```

Hộp thoại cài sẽ hiện (và đòi quyền Admin). Chạy lại đúng lệnh này cũng cập nhật được lên bản mới nhất; cài đặt của bạn được giữ nguyên.

Lệnh dùng `curl` và cmd. Trong PowerShell, `curl` là tên khác của lệnh khác, hãy dùng cmd hoặc `curl.exe`.

### Cập nhật
Từ bản 1.4.0, app tự kiểm tra bản mới trên GitHub Releases mỗi lần mở (lỗi mạng thì im lặng) và có nút **Kiểm tra cập nhật** trong cài đặt và trên menu khay. Khi có bản mới, hộp thoại hiện ba nút: **CẬP NHẬT**, **BỎ QUA BẢN NÀY**, **ĐỂ SAU**. Bấm CẬP NHẬT thì app tải bản mới, kiểm mã băm SHA-256, tự cài đè và mở lại, cài đặt được giữ nguyên, không hỏi thêm gì. Khi app tự chạy cùng Windows, nó chỉ báo bằng thông báo ở khay và một dòng trên menu khay, không bật hộp thoại.

Nếu bạn đang dùng **1.3.0**, bản đó chưa có bộ cập nhật: hãy chạy lại lệnh cài một dòng ở trên một lần để lên bản mới; từ đó trở đi app tự cập nhật.

Sau khi cập nhật, Windows Search/Start có thể còn giữ icon cũ (bộ nhớ đệm icon): hãy đăng xuất rồi đăng nhập lại để thấy icon mới.

Ghi chú bảo mật: file tải về chỉ được lấy từ HTTPS của GitHub (github.com và các máy chủ file của GitHub), tối đa 400 MB, và phải khớp mã băm SHA-256 trong `FpsOverlay.exe.sha256`. Mã băm này chứng minh file không bị hỏng hay cắt cụt, **không** chứng minh người phát hành là chủ kho: file chưa được ký số, nên nếu tài khoản GitHub của chủ kho bị chiếm, kẻ chiếm có thể đăng cả file lẫn mã băm. Chủ kho nên bật xác thực hai lớp (2FA) cho tài khoản.

### Tải tay
Vào trang [Releases](https://github.com/ckhangg/FpsOverlay/releases), tải `FpsOverlay.exe` (và `FpsOverlay.exe.sha256` nếu muốn kiểm mã băm), rồi chạy file.

Khi tải bằng trình duyệt, Windows SmartScreen có thể cảnh báo "Windows protected your PC" vì file chưa được ký số. Bấm **More info** rồi **Run anyway** nếu bạn tin nguồn này. Tải bằng lệnh `curl` ở trên thường không bị cảnh báo này.

### Nhiệt độ CPU (tuỳ chọn)
Đọc nhiệt độ CPU cần driver [PawnIO](https://pawnio.eu/). Hộp thoại cài có ô tick "Cài PawnIO" (mặc định trống); có thể cài sau bằng nút trong cài đặt. Không cài thì nhiệt độ CPU hiện `--`, các thông số khác vẫn chạy.

### Gỡ cài đặt
Settings → Apps → EzFPS → Uninstall. Cài đặt (`%AppData%\FpsOverlay\settings.json`) và log (`Documents\FpsOverlay\Logs`) được giữ lại.

### Build từ mã nguồn
Cần .NET 10 SDK (x64).

```
dotnet test
dotnet build src/FpsOverlay
dotnet publish src/FpsOverlay -c Release -o publish
```

File một-cho-tất-cả nằm ở `publish\FpsOverlay.exe`. Phát hành bản mới (chủ kho): `.\scripts\release.ps1`.

### Giấy phép
MIT, xem [LICENSE](LICENSE). Các thành phần bên thứ ba (PresentMon, LibreHardwareMonitor, .NET, HidSharp, Mono.Posix) giữ giấy phép riêng, xem [THIRD-PARTY-NOTICES.txt](src/FpsOverlay/Assets/Licenses/THIRD-PARTY-NOTICES.txt) và các file trong [src/FpsOverlay/Assets/Licenses](src/FpsOverlay/Assets/Licenses). Font nhúng (Chakra Petch, JetBrains Mono, Orbitron, VT323) theo SIL OFL, xem các file `OFL-*.txt` trong [src/FpsOverlay/Assets/Fonts](src/FpsOverlay/Assets/Fonts).

---

## English

EzFPS is a Windows app that shows FPS and system stats (1% low, GPU/CPU load and temperature, RAM, VRAM, clocks, power, GPU fan) on top of your games, like the MSI Afterburner overlay but fully customisable.

- Several styles: Classic, Cyberpunk, Neon purple, Green terminal; horizontal or vertical layout; size, opacity, colours, FPS-based colours. The settings, install and update windows follow the chosen style too.
- GPU/CPU overheat alerts (blink and beep).
- Per-session logs (one CSV row per second plus a summary file) in `Documents\FpsOverlay\Logs`.
- Shown only while gaming (on by default): fullscreen/borderless games, games under Steam or Xbox/Game Pass folders, or apps you mark "Always show".
- FPS is measured with PresentMon, sensors with LibreHardwareMonitor. Vietnamese and English UI.

### Requirements
- Windows 10 or 11, 64-bit.
- Administrator rights (PresentMon needs ETW). No separate .NET install needed.

### Install (one line)
Open **Command Prompt** (cmd), paste this and press Enter:

```
curl -fL -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
```

The installer dialog appears (and asks for Administrator rights). Running the same command again also updates to the latest version; your settings are kept.

The command uses `curl` and cmd syntax. In PowerShell, `curl` is an alias for a different command, so use cmd or `curl.exe`.

### Updating
From version 1.4.0 the app checks GitHub Releases for a newer version every time it starts (silently if the network fails), and has a **Check for updates** button in settings and on the tray menu. When a new version exists, a dialog offers **UPDATE**, **SKIP THIS VERSION** and **LATER**. UPDATE downloads the new version, verifies its SHA-256 hash, installs over the old one and restarts; your settings are kept and there are no further prompts. When the app starts with Windows, it only shows a tray notification and a line on the tray menu, with no dialog.

If you are on **1.3.0**, that version has no updater: run the one-line install command above once more to get it; from then on the app updates itself.

After updating, Windows Search/Start may keep the old icon until you sign out and back in (icon cache).

Security note: the download is only accepted from GitHub over HTTPS (github.com and GitHub's file hosts), up to 400 MB, and must match the SHA-256 hash in `FpsOverlay.exe.sha256`. The hash proves the file is not corrupted or truncated, but it does **not** prove who published it: the exe is not code-signed, so if the repository owner's GitHub account were hijacked, the attacker could publish both the file and its hash. The owner should enable two-factor authentication (2FA) on the account.

### Manual download
Go to the [Releases](https://github.com/ckhangg/FpsOverlay/releases) page, download `FpsOverlay.exe` (and `FpsOverlay.exe.sha256` if you want to verify the hash), then run it.

When downloaded with a browser, Windows SmartScreen may show "Windows protected your PC" because the file is not code-signed. Click **More info**, then **Run anyway** if you trust the source. Downloading with the `curl` command above usually avoids this warning.

### CPU temperature (optional)
Reading CPU temperature needs the [PawnIO](https://pawnio.eu/) driver. The installer has an unchecked "Install PawnIO" box; you can also install it later from the settings window. Without it, CPU temperature shows `--` and everything else works.

### Uninstall
Settings → Apps → EzFPS → Uninstall. Your settings (`%AppData%\FpsOverlay\settings.json`) and logs (`Documents\FpsOverlay\Logs`) are kept.

### Build from source
Requires the .NET 10 SDK (x64).

```
dotnet test
dotnet build src/FpsOverlay
dotnet publish src/FpsOverlay -c Release -o publish
```

The self-contained single file is `publish\FpsOverlay.exe`. Publishing a release (maintainer): `.\scripts\release.ps1`.

### Licence
MIT, see [LICENSE](LICENSE). Bundled third-party components (PresentMon, LibreHardwareMonitor, .NET, HidSharp, Mono.Posix) keep their own licences: see [THIRD-PARTY-NOTICES.txt](src/FpsOverlay/Assets/Licenses/THIRD-PARTY-NOTICES.txt) and the files in [src/FpsOverlay/Assets/Licenses](src/FpsOverlay/Assets/Licenses). Embedded fonts (Chakra Petch, JetBrains Mono, Orbitron, VT323) are under the SIL OFL, see the `OFL-*.txt` files in [src/FpsOverlay/Assets/Fonts](src/FpsOverlay/Assets/Fonts).
