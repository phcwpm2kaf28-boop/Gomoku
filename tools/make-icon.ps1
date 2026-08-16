# Generates app icons: rounded gradient background + glossy black/white stones.
# Outputs Icon.ico (multi-size), Icon.png, tile logos and splash screen into Gomoku\Assets.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assets = Join-Path (Split-Path -Parent $PSScriptRoot) "Gomoku\Assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null

function Draw-Stone($g, $s, $cx, $cy, $r, $kind) {
    # drop shadow
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 0, 0, 0))
    $g.FillEllipse($shadowBrush, [single]($cx - $r + 0.03 * $s), [single]($cy - $r + 0.05 * $s), [single](2 * $r), [single](2 * $r))
    $shadowBrush.Dispose()

    # body: path gradient, light source top-left
    $pp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pp.AddEllipse([single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r))
    $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($pp)
    $pgb.CenterPoint = [System.Drawing.PointF]::new([single]($cx - 0.32 * $r), [single]($cy - 0.32 * $r))
    if ($kind -eq 'black') {
        $pgb.CenterColor = [System.Drawing.Color]::FromArgb(255, 150, 150, 150)
        $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 8, 8, 8))
        $rimColor = [System.Drawing.Color]::FromArgb(220, 0, 0, 0)
    } else {
        $pgb.CenterColor = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
        $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 176, 166, 146))
        $rimColor = [System.Drawing.Color]::FromArgb(140, 110, 102, 88)
    }
    $g.FillPath($pgb, $pp)

    # rim for 3D thickness
    $rimPen = New-Object System.Drawing.Pen($rimColor, [single](0.045 * $r))
    $g.DrawEllipse($rimPen, [single]($cx - $r + 0.02 * $r), [single]($cy - $r + 0.02 * $r), [single](2 * $r - 0.04 * $r), [single](2 * $r - 0.04 * $r))
    $rimPen.Dispose()

    # specular highlight
    $hlAlpha = if ($kind -eq 'black') { 110 } else { 200 }
    $hl = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($hlAlpha, 255, 255, 255))
    $hlR = 0.30 * $r
    $g.FillEllipse($hl, [single]($cx - 0.38 * $r), [single]($cy - 0.42 * $r), [single](2 * $hlR), [single](2 * $hlR))
    $hl.Dispose()
    $pp.Dispose(); $pgb.Dispose()
}

function New-Art($size, $rounded) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $s = [double]$size
    $rect = New-Object System.Drawing.RectangleF(0, 0, $s, $s)

    # 圆角剪裁路径（圆角更大，更柔和）
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($rounded) {
        $d = $s * 0.24
        $path.AddArc(0, 0, $d, $d, 180, 90)
        $path.AddArc($s - $d, 0, $d, $d, 270, 90)
        $path.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
        $path.AddArc(0, $s - $d, $d, $d, 90, 90)
        $path.CloseFigure()
    } else {
        $path.AddRectangle($rect)
    }

    # 背景渐变：靛蓝 → 青蓝（更饱和的现代感）
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 74, 92, 240), [System.Drawing.Color]::FromArgb(255, 0, 194, 216), 135)
    $g.FillPath($bg, $path)

    # 左上角柔光（高光质感）
    $glow = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(90, 255, 255, 255), [System.Drawing.Color]::FromArgb(0, 255, 255, 255), 90)
    $g.FillPath($glow, $path)

    # 浅色棋盘网格底纹（4×4 交点棋盘，增强五子棋辨识度）
    $g.SetClip($path)
    $gridPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(42, 255, 255, 255), [single](0.010 * $s))
    $x0 = 0.16 * $s; $x1 = 0.84 * $s; $y0 = 0.16 * $s; $y1 = 0.84 * $s
    for ($i = 0; $i -le 4; $i++) {
        $t = $i / 4.0
        $gx = $x0 + $t * ($x1 - $x0)
        $gy = $y0 + $t * ($y1 - $y0)
        $g.DrawLine($gridPen, [single]$gx, [single]$y0, [single]$gx, [single]$y1)
        $g.DrawLine($gridPen, [single]$x0, [single]$gy, [single]$x1, [single]$gy)
    }
    $gridPen.Dispose()

    # 棋子（更饱满，置于网格之上）
    Draw-Stone $g $s (0.345 * $s) (0.325 * $s) (0.215 * $s) 'black'
    Draw-Stone $g $s (0.645 * $s) (0.615 * $s) (0.215 * $s) 'white'
    $g.ResetClip()

    # 白色细描边（浅色壁纸上也有精致边缘）
    $rim = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(72, 255, 255, 255), [single](0.014 * $s))
    $g.DrawPath($rim, $path)
    $rim.Dispose()

    $path.Dispose()
    $g.Dispose()
    return $bmp
}

function New-WideArt($w, $h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $rect = New-Object System.Drawing.RectangleF(0, 0, $w, $h)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 48, 88, 255), [System.Drawing.Color]::FromArgb(255, 0, 186, 214), 45)
    $g.FillRectangle($bg, $rect)
    $glow = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(70, 255, 255, 255), [System.Drawing.Color]::FromArgb(0, 255, 255, 255), 90)
    $g.FillRectangle($glow, $rect)
    $r = 0.16 * $h
    Draw-Stone $g $w (0.28 * $w) (0.52 * $h) $r 'black'
    Draw-Stone $g $w (0.42 * $w) (0.60 * $h) $r 'white'
    $g.Dispose()
    return $bmp
}

function Save-Png($bmp, $path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  -> $path"
}

function Write-Ico($path, $sizes) {
    $images = @()
    foreach ($sz in $sizes) {
        $bmp = New-Art $sz $true
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $images += , @($sz, $ms.ToArray())
        $bmp.Dispose()
    }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($img in $images) {
        $sz = $img[0]; $data = $img[1]
        $w = if ($sz -ge 256) { 0 } else { $sz }
        $bw.Write([byte]$w); $bw.Write([byte]$w)
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$offset)
        $offset += $data.Length
    }
    foreach ($img in $images) { $bw.Write($img[1]) }
    $bw.Close(); $fs.Close()
    Write-Host "  -> $path"
}

Write-Host "Generating icons..."
Write-Ico (Join-Path $assets "Icon.ico") @(16, 24, 32, 48, 64, 128, 256)
# 所有静态图标统一圆角（$true），与系统应用图标风格一致
Save-Png (New-Art 256 $true) (Join-Path $assets "Icon.png")
Save-Png (New-Art 256 $true) (Join-Path $assets "Square310x310Logo.png")
Save-Png (New-Art 150 $true) (Join-Path $assets "Square150x150Logo.png")
Save-Png (New-Art 71 $true) (Join-Path $assets "Square71x71Logo.png")
Save-Png (New-Art 44 $true) (Join-Path $assets "Square44x44Logo.png")
Save-Png (New-Art 50 $true) (Join-Path $assets "StoreLogo.png")
Save-Png (New-WideArt 310 150) (Join-Path $assets "Wide310x150Logo.png")
Save-Png (New-WideArt 620 300) (Join-Path $assets "SplashScreen.png")
Write-Host "Done."
