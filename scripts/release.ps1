# Phát hành bản mới lên GitHub Releases. Chạy từ gốc kho: .\scripts\release.ps1
# Yêu cầu: Windows PowerShell 5.1+, git, dotnet SDK, gh (đã đăng nhập).
# Thẻ (tag) được tạo và đẩy lên trước, rồi gh release create dùng thẻ đó.
$ErrorActionPreference = 'Stop'

function Stop-Release([string]$message) {
    Write-Host "LỖI: $message" -ForegroundColor Red
    exit 1
}

function Assert-Ok([string]$what) {
    if ($LASTEXITCODE -ne 0) { Stop-Release "$what thất bại (mã thoát $LASTEXITCODE)." }
}

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    # 0. Kiểm tra điều kiện trước khi làm bất cứ việc gì có tác dụng phụ
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Stop-Release 'Không thấy lệnh gh (GitHub CLI).' }
    # gh/git ghi stderr; PS 5.1 coi là lỗi nên tạm đổi chế độ khi kiểm tra.
    $ErrorActionPreference = 'Continue'
    gh auth status *> $null
    $ghAuthOk = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'
    if (-not $ghAuthOk) { Stop-Release 'gh chưa đăng nhập (chạy: gh auth login).' }

    # 1. Cây làm việc sạch và đang ở nhánh master
    $status = git status --porcelain
    Assert-Ok 'git status'
    if ($status) { Stop-Release 'Cây làm việc chưa sạch (git status --porcelain không rỗng).' }
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    Assert-Ok 'git rev-parse'
    if ($branch -ne 'master') { Stop-Release "Phải ở nhánh master (đang ở '$branch')." }

    # Master cục bộ phải trùng origin/master (khi có origin)
    $remotes = git remote
    Assert-Ok 'git remote'
    if ($remotes -contains 'origin') {
        $ErrorActionPreference = 'Continue'
        git fetch origin *> $null
        $fetchCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($fetchCode -ne 0) { Stop-Release 'git fetch origin thất bại.' }
        $local = (git rev-parse master).Trim()
        Assert-Ok 'git rev-parse master'
        $remote = (git rev-parse origin/master).Trim()
        Assert-Ok 'git rev-parse origin/master'
        if ($local -ne $remote) { Stop-Release 'master cục bộ khác origin/master (hãy pull/push cho khớp trước).' }
    }

    # 2. Đọc phiên bản, dừng nếu thẻ đã có
    $csproj = 'src\FpsOverlay\FpsOverlay.csproj'
    $match = [regex]::Match((Get-Content -Raw $csproj), '<Version>\s*([^<\s]+)\s*</Version>')
    if (-not $match.Success) { Stop-Release "Không đọc được <Version> trong $csproj." }
    $version = $match.Groups[1].Value
    $tag = "v$version"

    $existing = git tag -l $tag
    Assert-Ok 'git tag -l'
    if ($existing) { Stop-Release "Thẻ $tag đã tồn tại." }
    # gh release view trả mã khác 0 khi chưa có release; PS 5.1 coi stderr là lỗi nên tạm đổi chế độ.
    $ErrorActionPreference = 'Continue'
    gh release view $tag *> $null
    $releaseExists = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'
    if ($releaseExists) { Stop-Release "Release $tag đã tồn tại trên GitHub." }

    # 3. Chạy test
    dotnet test
    Assert-Ok 'dotnet test'

    # 4. Đóng gói
    dotnet publish src/FpsOverlay -c Release -o publish
    Assert-Ok 'dotnet publish'

    # 5. Mã băm SHA-256 (chữ thường, ASCII, không BOM)
    $exe = Join-Path (Get-Location) 'publish\FpsOverlay.exe'
    if (-not (Test-Path $exe)) { Stop-Release 'Không thấy publish\FpsOverlay.exe.' }
    $hash = (Get-FileHash -Algorithm SHA256 -Path $exe).Hash.ToLowerInvariant()
    $shaFile = Join-Path (Get-Location) 'publish\FpsOverlay.exe.sha256'
    [System.IO.File]::WriteAllText($shaFile, "$hash *FpsOverlay.exe`n", [System.Text.Encoding]::ASCII)

    # 6. Tạo và đẩy thẻ
    git tag $tag
    Assert-Ok 'git tag'
    $ErrorActionPreference = 'Continue'
    git push origin $tag
    $pushCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($pushCode -ne 0) { Stop-Release "git push origin $tag thất bại (mã thoát $pushCode)." }

    # 7. Tạo release (thẻ đã có trên GitHub)
    $notes = @"
**Cài đặt (Windows 10/11 64-bit, cần quyền Admin) / Install:**

``````
curl -fL -o "%TEMP%\FpsOverlay.exe" https://github.com/ckhangg/FpsOverlay/releases/latest/download/FpsOverlay.exe && start "" "%TEMP%\FpsOverlay.exe"
``````

Chạy lệnh trên trong Command Prompt (cmd). Chạy lại lệnh để cập nhật.
Run it in Command Prompt (cmd). Run it again to update.

Sau khi cập nhật, Windows Search/Start có thể còn giữ icon cũ (bộ nhớ đệm icon): hãy đăng xuất rồi đăng nhập lại để thấy icon mới.
After updating, Windows Search/Start may keep the old icon until you sign out and back in (icon cache).

SHA-256: ``$hash``
"@
    $notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "fpsoverlay-notes-$tag.md"
    [System.IO.File]::WriteAllText($notesFile, $notes, (New-Object System.Text.UTF8Encoding($false)))
    try {
        gh release create $tag 'publish\FpsOverlay.exe' 'publish\FpsOverlay.exe.sha256' --title "EzFPS $version" --notes-file $notesFile
        if ($LASTEXITCODE -ne 0) {
            Stop-Release "gh release create thất bại sau khi đã đẩy thẻ $tag. Gỡ thẻ rồi chạy lại: git tag -d $tag ; git push origin --delete $tag"
        }
    }
    finally {
        Remove-Item $notesFile -ErrorAction SilentlyContinue
    }

    Write-Host "Đã phát hành $tag." -ForegroundColor Green
}
finally {
    Pop-Location
}
