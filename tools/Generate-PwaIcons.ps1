# Sinh icon PWA cho Vinatex Phú Hưng ERP (192x192 và 512x512).
# Chạy: powershell -ExecutionPolicy Bypass -File tools/Generate-PwaIcons.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'src\CvErp.Web\wwwroot\icons'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function New-ErpIcon {
    param(
        [Parameter(Mandatory = $true)][int]$Size,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $bounds = New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)
    $startColor = [System.Drawing.ColorTranslator]::FromHtml('#0B2439')
    $endColor = [System.Drawing.ColorTranslator]::FromHtml('#178A74')
    $background = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bounds, $startColor, $endColor, 45.0)
    $graphics.FillRectangle($background, $bounds)

    # Vòng tròn trang trí góc trên phải
    $ringColor = [System.Drawing.Color]::FromArgb(36, 255, 255, 255)
    $ringPen = New-Object System.Drawing.Pen($ringColor, ([float]([Math]::Max(1.0, $Size * 0.012))))
    $graphics.DrawEllipse($ringPen, [float]($Size * 0.60), [float](-$Size * 0.24), [float]($Size * 0.80), [float]($Size * 0.80))

    # Vòng tròn mờ góc dưới trái
    $glowColor = [System.Drawing.Color]::FromArgb(28, 255, 255, 255)
    $glowBrush = New-Object System.Drawing.SolidBrush($glowColor)
    $graphics.FillEllipse($glowBrush, [float](-$Size * 0.26), [float]($Size * 0.66), [float]($Size * 0.56), [float]($Size * 0.56))

    $family = New-Object System.Drawing.FontFamily('Segoe UI')
    $mainFont = New-Object System.Drawing.Font($family, [float]($Size * 0.27), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subFont = New-Object System.Drawing.Font($family, [float]($Size * 0.105), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $softWhite = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(225, 206, 240, 233))
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center

    $mainRect = New-Object System.Drawing.RectangleF(0, [float]($Size * 0.16), $Size, [float]($Size * 0.48))
    $graphics.DrawString('VPH', $mainFont, $white, $mainRect, $format)

    $subRect = New-Object System.Drawing.RectangleF(0, [float]($Size * 0.605), $Size, [float]($Size * 0.14))
    $graphics.DrawString('ERP', $subFont, $softWhite, $subRect, $format)

    $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 88, 210, 185))
    $barWidth = [float]($Size * 0.16)
    $barHeight = [float]([Math]::Max(3.0, $Size * 0.014))
    $graphics.FillRectangle($accentBrush, [float](($Size - $barWidth) / 2), [float]($Size * 0.79), $barWidth, $barHeight)

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)

    $accentBrush.Dispose()
    $format.Dispose()
    $softWhite.Dispose()
    $white.Dispose()
    $subFont.Dispose()
    $mainFont.Dispose()
    $family.Dispose()
    $glowBrush.Dispose()
    $ringPen.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

foreach ($size in @(192, 512)) {
    $target = Join-Path $outDir "icon-$size.png"
    New-ErpIcon -Size $size -Path $target
    Write-Host "Đã tạo $target"
}
