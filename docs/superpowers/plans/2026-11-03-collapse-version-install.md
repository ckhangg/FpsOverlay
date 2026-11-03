# Đợt 7: thu gọn cài đặt, số phiên bản, cài bằng một lệnh — Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:**
- Thu gọn ba mục Màu sắc / Màu theo FPS / Cảnh báo trong cửa sổ cài đặt.
- Hiện `vX.Y.Z` ở góc cửa sổ.
- Đưa dự án lên kho GitHub công khai `ckhangg/FpsOverlay` (lịch sử đã đổi tác giả), có release và lệnh `curl` cài một dòng.

**Architecture:**
- Phần thuần (chuỗi phiên bản) nằm trong Core và có test. WPF dùng lại kiểu ô xổ ra của đợt 6.
- Script `scripts/release.ps1` đóng gói và đăng release bằng `gh`.
- Các bước git/GitHub (merge, đổi tác giả, tạo kho, đẩy, phát hành) là Task 3, do người điều phối làm trực tiếp, không giao subagent.

**Tech Stack:** C# / .NET 10, WPF, xUnit, PowerShell 5.1, git, GitHub CLI.

**Spec:** `docs/superpowers/specs/2026-11-03-collapse-version-install-design.md`

## Global Constraints
- Ba mục `// MÀU SẮC`, `// MÀU THEO FPS`, `// CẢNH BÁO`: tiêu đề luôn hiện, nội dung mặc định **thu gọn** mỗi lần mở cửa sổ; bấm thanh tiêu đề để xổ ra (▾ khi thu gọn, ▴ khi mở).
- Tóm tắt khi thu gọn:
  - Màu sắc: 3 ô màu (nhãn, giá trị, FPS);
  - Màu theo FPS: "Bật · 60/30" / "Tắt" (en "On · 60/30" / "Off");
  - Cảnh báo: "GPU 85° · CPU 90° · 10s".
- `// GIAO DIỆN`, `// THÔNG SỐ`, `// HỆ THỐNG` giữ nguyên.
- Số phiên bản ở góc dưới trái, cùng hàng nút Khôi phục mặc định / Xong: dạng `v1.3.0`, bỏ đuôi `+…`.
- Phiên bản `1.3.0`.
- Kho `ckhangg/FpsOverlay`, công khai. Tác giả và người commit của mọi commit: `CKhang <39177627+ckhangg@users.noreply.github.com>`. Bản sao lưu lịch sử cũ: `%USERPROFILE%\Downloads\FPS-history-backup.bundle`.
- Release: tag `vX.Y.Z`, file `FpsOverlay.exe` và `FpsOverlay.exe.sha256`.
- Lệnh cài:
  ```
  curl -L -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
  ```
- Giấy phép MIT.
- Không đưa `publish/`, `.superpowers/`, `*.pdb` lên kho.
- Mọi chữ mới có tiếng Việt và tiếng Anh. Build 0 cảnh báo, mọi test qua.

## Review Focus
1. **Chuỗi phiên bản có đuôi mã commit** (`1.3.0+7f042db…`) hoặc không đọc được: phải hiện `v1.3.0` hoặc `v?`, không lỗi. Test trong Task 1 (`Strips_commit_suffix`, `Missing_version_shows_question_mark`).
2. **Đổi ngôn ngữ khi ba mục đang thu gọn:** dòng tóm tắt đổi theo. Kiểm bằng mắt trong Task 1.
3. **Lịch sử sau khi đổi tác giả còn sót email công ty** (trong người commit hoặc ghi chú `Co-Authored-By` không liên quan): kiểm bằng lệnh trong Task 3, bước 4.
4. **Đẩy nhầm file build hay file tạm** lên kho công khai: kiểm `git status --ignored` và danh sách file đã theo dõi trước khi đẩy (Task 3, bước 6).
5. **Link `releases/latest/download/FpsOverlay.exe` không tải được** (release là bản nháp, sai tên file): kiểm bằng `curl -I` sau khi phát hành (Task 3, bước 8).

---

### Task 1: Thu gọn ba mục và số phiên bản

**Files:**
- Create: `src/FpsOverlay.Core/AppVersion.cs`, `tests/FpsOverlay.Core.Tests/AppVersionTests.cs`
- Modify: `src/FpsOverlay/SettingsWindow.xaml`, `src/FpsOverlay/SettingsWindow.xaml.cs`, `src/FpsOverlay.Core/Localization/Loc.cs`

**Interfaces:**
- Produces:
  - `public static class AppVersion { public static string Display(string? informationalVersion); }` → `"v" + phần trước dấu '+'`, cắt khoảng trắng; null hoặc rỗng → `"v?"`.
  - SettingsWindow đọc `Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion`.
  - Loc:

    | Khoá | Tiếng Việt | Tiếng Anh |
    |---|---|---|
    | `FpsColorSummaryOn` | "Bật · {0}/{1}" | "On · {0}/{1}" |
    | `FpsColorSummaryOff` | "Tắt" | "Off" |
    | `AlertsSummary` | "GPU {0}° · CPU {1}° · {2}s" | giống tiếng Việt |

- [ ] **Step 1: Test**
```csharp
[Fact] public void Strips_commit_suffix() => Assert.Equal("v1.3.0", AppVersion.Display("1.3.0+7f042db0123"));
[Fact] public void Plain_version() => Assert.Equal("v1.3.0", AppVersion.Display("1.3.0"));
[Theory] [InlineData(null)] [InlineData("")] [InlineData("  ")]
public void Missing_version_shows_question_mark(string? v) => Assert.Equal("v?", AppVersion.Display(v));
```
- [ ] **Step 2:** `dotnet test` → FAIL; cài `AppVersion`; → PASS.
- [ ] **Step 3:** Cài phần giao diện.
  - Mỗi mục trong ba mục: thanh tiêu đề bấm được, gồm nhãn mục, mũi tên và tóm tắt. Nội dung cũ chuyển vào một panel `Collapsed`, dùng cùng cơ chế với ô Thông số của đợt 6 và dùng chung một helper xổ/thu cho mọi ô (gồm cả hai ô của đợt 6 để không lặp code).
  - Tóm tắt cập nhật trong `Refresh` và `ApplyLanguage`.
  - Nhãn phiên bản `VersionText` ở hàng nút dưới cùng, căn trái, chữ nhỏ, mờ.
- [ ] **Step 4:** `dotnet build src/FpsOverlay` 0 cảnh báo; `dotnet test` qua.
- [ ] **Step 5: Kiểm bằng mắt** (Admin):
  - ba mục thu gọn khi mở cửa sổ; bấm xổ ra và thu lại được;
  - tóm tắt đúng và đổi theo ngôn ngữ;
  - `v1.3.0` hiện ở góc (sau Task 2).
- [ ] **Step 6:** Commit `feat(settings): collapsible colour/fps/alert sections and version label`.

### Task 2: Phiên bản 1.3.0, README, LICENSE, script phát hành, HANDOFF

**Files:**
- Create: `README.md`, `LICENSE`, `scripts/release.ps1`
- Modify: `src/FpsOverlay/FpsOverlay.csproj` (`<Version>1.3.0</Version>`), `.gitignore` (nếu thiếu `publish/`, `*.pdb`), `HANDOFF.md`

**Interfaces:**
- `scripts/release.ps1` (PowerShell 5.1, chạy từ gốc kho, không tham số):
  1. dừng nếu `git status --porcelain` không rỗng hoặc nhánh hiện tại không phải `master`;
  2. đọc `<Version>` từ csproj → `$tag = "v$version"`; dừng nếu tag đã có (`git tag -l`, `gh release view`);
  3. `dotnet test`; dừng nếu lỗi;
  4. `dotnet publish src/FpsOverlay -c Release -o publish`;
  5. ghi `publish\FpsOverlay.exe.sha256` gồm `<hash thường> *FpsOverlay.exe`;
  6. `git tag $tag` rồi `git push origin $tag`;
  7. `gh release create $tag publish\FpsOverlay.exe publish\FpsOverlay.exe.sha256 --title "FPS Overlay $version" --notes <ghi chú có lệnh cài>`.

  Mọi lệnh ngoài kiểm `$LASTEXITCODE` và dừng khi lỗi.
- `LICENSE`: văn bản MIT chuẩn, dòng `Copyright (c) 2026 CKhang`.
- `README.md`: tiếng Việt trước, tiếng Anh sau. Gồm:
  - app làm gì (overlay FPS và thông số, các phong cách, log, chỉ hiện khi chơi game);
  - yêu cầu (Windows 10/11 64-bit, quyền Admin);
  - **lệnh cài** đúng như Global Constraints, trong khối code;
  - tải tay từ trang Releases;
  - cảnh báo SmartScreen khi tải bằng trình duyệt;
  - PawnIO tuỳ chọn cho nhiệt độ CPU;
  - gỡ: Settings → Apps → FPS Overlay;
  - build từ mã nguồn (`dotnet test`, `dotnet publish …`);
  - giấy phép (MIT, cùng giấy phép bên thứ ba trong `src/FpsOverlay/Assets/Licenses`).
- `HANDOFF.md`:
  - lịch sử thêm "Đợt 7";
  - tài liệu thêm spec và plan;
  - mục 5 có kho GitHub, cách phát hành (`.\scripts\release.ps1`) và lệnh cài;
  - mục 6 thêm "Phát hành bản mới";
  - mục 9 "Đợt 7": các kiểm tay của Task 1, chạy lệnh cài trên máy khác, và chạy lại lệnh để cập nhật.

- [ ] **Step 1:** Tạo và sửa các file theo Interfaces.
- [ ] **Step 2:** Kiểm cú pháp script: `powershell -NoProfile -Command "$null = [scriptblock]::Create((Get-Content -Raw scripts/release.ps1))"` không lỗi. **Không chạy** script ở bước này.
- [ ] **Step 3:** `dotnet build src/FpsOverlay` 0 cảnh báo; `dotnet test` qua.
- [ ] **Step 4:** Commit `chore: version 1.3.0, README, MIT licence, release script`.

### Task 3 (người điều phối làm, cần người dùng đã duyệt): merge, đổi tác giả, tạo kho, phát hành

- [ ] **Step 1:** Merge nhánh vào master (`--ff-only`), xoá nhánh, `dotnet test` qua.
- [ ] **Step 2:** Sao lưu: `git bundle create %USERPROFILE%\Downloads\FPS-history-backup.bundle --all`, rồi `git bundle verify` báo ổn.
- [ ] **Step 3:** Đổi tác giả và người commit của mọi commit thành `CKhang <39177627+ckhangg@users.noreply.github.com>`. Dùng `git filter-branch --env-filter` trên mọi nhánh và tag, không đụng nội dung file. Sau đó xoá `refs/original` và đặt `git config user.name "CKhang"`, `git config user.email "39177627+ckhangg@users.noreply.github.com"` (cấu hình riêng của kho).
- [ ] **Step 4:** Kiểm: `git log --all --format='%an %ae %cn %ce' | sort -u` chỉ còn CKhang/noreply; `git log --all -p -S <tên miền cũ>` rỗng (nếu có thì báo người dùng trước khi đẩy). `dotnet test` vẫn qua.
- [ ] **Step 5:** `gh auth status` cho thấy `ckhangg` đang active.
- [ ] **Step 6:** Kiểm file sẽ đẩy: `git ls-files` không có `publish/`, `.superpowers/`, `*.pdb`, `bin/`, `obj/`.
- [ ] **Step 7:** `gh repo create ckhangg/FpsOverlay --public --source . --remote origin --push --description "FPS overlay for Windows games (PresentMon + sensors)"`.
- [ ] **Step 8:** Chạy `.\scripts\release.ps1`. Kiểm `curl -sIL https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe` trả về 200 và đúng dung lượng.
- [ ] **Step 9:** Báo người dùng link kho, link release và lệnh cài.
