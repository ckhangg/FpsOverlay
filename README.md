# FPS Overlay

**Tiếng Việt** | [English](#english)

## Tiếng Việt

FPS Overlay là app Windows hiện FPS và các thông số máy (1% low, tải và nhiệt độ GPU/CPU, RAM, VRAM, xung nhân, công suất, quạt GPU) ngay trên game, giống overlay của MSI Afterburner nhưng tuỳ chỉnh theo ý mình.

- Nhiều phong cách: Cổ điển, Cyberpunk, Neon tím, Terminal xanh; bố cục ngang hoặc dọc; chỉnh cỡ, độ mờ, màu, màu theo FPS.
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
curl -L -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
```

Hộp thoại cài sẽ hiện (và đòi quyền Admin). Chạy lại đúng lệnh này để cập nhật lên bản mới nhất; cài đặt của bạn được giữ nguyên.

Lệnh dùng `curl` và cmd. Trong PowerShell, `curl` là tên khác của lệnh khác, hãy dùng cmd hoặc `curl.exe`.

### Tải tay
Vào trang [Releases](https://github.com/ckhangg/FpsOverlay/releases), tải `FpsOverlay.exe` (và `FpsOverlay.exe.sha256` nếu muốn kiểm mã băm), rồi chạy file.

Khi tải bằng trình duyệt, Windows SmartScreen có thể cảnh báo "Windows protected your PC" vì file chưa được ký số. Bấm **More info** rồi **Run anyway** nếu bạn tin nguồn này. Tải bằng lệnh `curl` ở trên thường không bị cảnh báo này.

### Nhiệt độ CPU (tuỳ chọn)
Đọc nhiệt độ CPU cần driver [PawnIO](https://pawnio.eu/). Hộp thoại cài có ô tick "Cài PawnIO" (mặc định trống); có thể cài sau bằng nút trong cài đặt. Không cài thì nhiệt độ CPU hiện `--`, các thông số khác vẫn chạy.

### Gỡ cài đặt
Settings → Apps → FPS Overlay → Uninstall. Cài đặt (`%AppData%\FpsOverlay\settings.json`) và log (`Documents\FpsOverlay\Logs`) được giữ lại.

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

FPS Overlay is a Windows app that shows FPS and system stats (1% low, GPU/CPU load and temperature, RAM, VRAM, clocks, power, GPU fan) on top of your games, like the MSI Afterburner overlay but fully customisable.

- Several styles: Classic, Cyberpunk, Neon purple, Green terminal; horizontal or vertical layout; size, opacity, colours, FPS-based colours.
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
curl -L -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
```

The installer dialog appears (and asks for Administrator rights). Run the same command again to update to the latest version; your settings are kept.

The command uses `curl` and cmd syntax. In PowerShell, `curl` is an alias for a different command, so use cmd or `curl.exe`.

### Manual download
Go to the [Releases](https://github.com/ckhangg/FpsOverlay/releases) page, download `FpsOverlay.exe` (and `FpsOverlay.exe.sha256` if you want to verify the hash), then run it.

When downloaded with a browser, Windows SmartScreen may show "Windows protected your PC" because the file is not code-signed. Click **More info**, then **Run anyway** if you trust the source. Downloading with the `curl` command above usually avoids this warning.

### CPU temperature (optional)
Reading CPU temperature needs the [PawnIO](https://pawnio.eu/) driver. The installer has an unchecked "Install PawnIO" box; you can also install it later from the settings window. Without it, CPU temperature shows `--` and everything else works.

### Uninstall
Settings → Apps → FPS Overlay → Uninstall. Your settings (`%AppData%\FpsOverlay\settings.json`) and logs (`Documents\FpsOverlay\Logs`) are kept.

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
