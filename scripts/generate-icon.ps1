#requires -Version 7.0
<#
.SYNOPSIS
    Generates the RoboSync application icon (multi-resolution .ico) from code.
.DESCRIPTION
    Renders the "RS" badge on the brand accent color at several sizes and packs
    them into a single PNG-compressed .ico. Re-run to regenerate the icon.
#>
Add-Type -AssemblyName System.Drawing

$accent = [System.Drawing.Color]::FromArgb(0x2F, 0x81, 0xF7)
$sizes  = 16, 24, 32, 48, 64, 128, 256
$outPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/RoboSync.App/Assets/RoboSync.ico'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outPath) | Out-Null

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

$pngs = @()
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $radius = [Math]::Max(2.0, $size * 0.22)
    $path = New-RoundedRectPath 0 0 $size $size $radius
    $brush = New-Object System.Drawing.SolidBrush($accent)
    $g.FillPath($brush, $path)

    $text = 'RS'
    $fontSize = [single]($size * 0.42)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, [single](-$size * 0.02), $size, $size)
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.DrawString($text, $font, $white, $rect, $fmt)

    $g.Dispose(); $brush.Dispose(); $white.Dispose(); $font.Dispose(); $path.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose(); $ms.Dispose()
}

# Assemble the .ico container (ICONDIR + ICONDIRENTRY[] + PNG payloads).
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type = icon
$bw.Write([uint16]$sizes.Count)   # image count

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$dim)         # width
    $bw.Write([byte]$dim)         # height
    $bw.Write([byte]0)            # color palette
    $bw.Write([byte]0)            # reserved
    $bw.Write([uint16]1)          # color planes
    $bw.Write([uint16]32)         # bits per pixel
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($outPath, $out.ToArray())
$bw.Dispose(); $out.Dispose()

Write-Host "Icon written: $outPath ($([Math]::Round((Get-Item $outPath).Length/1KB,1)) KB, sizes: $($sizes -join ', '))" -ForegroundColor Green
