# Vẽ logo EzFPS ("màn hình 60") và ghi src/FpsOverlay/Assets/EzFPS.ico (PNG cho mọi cỡ).
# Chạy từ gốc kho bằng Windows PowerShell 5.1:  powershell -File scripts/make-icon.ps1
# Tham số tuỳ chọn -PreviewDir: xuất thêm PNG xem thử từng cỡ.
param([string]$PreviewDir = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = (Get-Location).Path
$fontFile = Join-Path $root 'src\FpsOverlay\Assets\Fonts\JetBrainsMono-Variable.ttf'
$icoFile = Join-Path $root 'src\FpsOverlay\Assets\EzFPS.ico'
if (-not (Test-Path $fontFile)) { throw "Chạy từ gốc kho: không thấy $fontFile" }

$fonts = New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile($fontFile)
$family = $fonts.Families[0]

$pink = [System.Drawing.ColorTranslator]::FromHtml('#FF4FD8')
$bg = [System.Drawing.ColorTranslator]::FromHtml('#140620')

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

# Vẽ ở lưới 64x64 (như SVG #logoD) trên bitmap $ss lần cỡ đích, rồi thu nhỏ để khử răng cưa.
# Cỡ nhỏ (<= 32) phóng to số "60" và dày nét viền để còn đọc được.
function DrawIcon([int]$size) {
    $ss = 8
    $big = $size * $ss
    $bmp = New-Object System.Drawing.Bitmap $big, $big, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)
    $k = $big / 64.0
    $g.ScaleTransform($k, $k)
    $small = $size -le 32
    $stroke = if ($small) { 5.0 } else { 3.5 }
    # thân màn hình: nét viền nằm giữa mép hình chữ nhật như SVG
    $pixelDigits = $size -le 24   # 16 và 24 px: bản đơn giản hoá, bỏ chân đế, màn hình choán gần hết ô (chừa lề trong suốt 1 px), số vẽ tay theo điểm ảnh
    if ($pixelDigits) {
        $edge = 64.0 / $size + $stroke / 2   # lề 1 px + nửa nét viền
        $body = RoundRect $edge $edge (64 - 2 * $edge) (64 - 2 * $edge) 5
    } else {
        $body = RoundRect 4 8 56 40 5
    }
    $g.FillPath((New-Object System.Drawing.SolidBrush $bg), $body)
    $pen = New-Object System.Drawing.Pen $pink, $stroke
    $pen.LineJoin = 'Round'
    $g.DrawPath($pen, $body)
    $pb = New-Object System.Drawing.SolidBrush $pink
    if (-not $pixelDigits) {
        $g.FillRectangle($pb, 24, 50, 16, 4)
        $g.FillPath($pb, (RoundRect 16 55 32 4 2))
    }
    $em = if ($small) { 31.0 } else { 22.0 }
    $emBold = if ($small) { 1.6 } else { 1.1 }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $white = [System.Drawing.Color]::White
    # số "60": đường viền trắng làm đậm (font biến thiên chỉ nạp được kiểu thường)
    if (-not $pixelDigits) {
        $path.AddString('60', $family, 0, $em, (New-Object System.Drawing.PointF 32, 28), $fmt)
        $g.FillPath((New-Object System.Drawing.SolidBrush $white), $path)
        $wp = New-Object System.Drawing.Pen $white, $emBold
        $wp.LineJoin = 'Round'
        $g.DrawPath($wp, $path)
    }
    $g.Dispose()

    $out = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($out)
    $g2.InterpolationMode = 'HighQualityBicubic'; $g2.PixelOffsetMode = 'HighQuality'; $g2.SmoothingMode = 'HighQuality'
    $g2.DrawImage($bmp, 0, 0, $size, $size)
    $g2.Dispose(); $bmp.Dispose()
    if ($pixelDigits) {
        # số 60 ở 16/24 px vẽ tay theo điểm ảnh (vector bị nhoè, chữ số 0 có chấm khó đọc)
        # glyph 3x5 (16 px) và 5x7 (24 px); x0/y0 là góc trên trái của cụm "60"
        if ($size -eq 16) {
            $six = '111', '100', '111', '101', '111'; $zero = '111', '101', '101', '101', '111'; $x0 = 4; $y0 = 5; $gap = 2
        } else {
            $six = '01110', '10000', '10000', '11110', '10001', '10001', '01110'
            $zero = '01110', '10001', '10001', '10001', '10001', '10001', '01110'; $x0 = 6; $y0 = 8; $gap = 2
        }
        $x = $x0
        foreach ($glyph in $six, $zero) {
            for ($r = 0; $r -lt $glyph.Count; $r++) {
                for ($c = 0; $c -lt $glyph[$r].Length; $c++) {
                    if ($glyph[$r][$c] -eq '1') { $out.SetPixel($x + $c, $y0 + $r, $white) }
                }
            }
            $x += $glyph[0].Length + $gap
        }
    }
    return $out
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = DrawIcon $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    if ($PreviewDir) {
        New-Item -ItemType Directory -Force $PreviewDir | Out-Null
        $bmp.Save((Join-Path $PreviewDir "icon-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $bmp.Dispose()
}

# ICONDIR (6 byte) + mỗi ICONDIRENTRY 16 byte, rồi dữ liệu PNG; 0 trong width/height nghĩa là 256.
$fs = [System.IO.File]::Create($icoFile)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush(); $w.Close()
Write-Host "Đã ghi $icoFile"
