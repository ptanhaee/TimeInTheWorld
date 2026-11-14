# Rebuild the Windows icon and preview from the geometry in Assets/AppIcon.svg.
# Small frames omit the globe grid so the clock remains legible at 16-24 pixels.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Assets'))

function New-IconBitmap([int]$size) {
    $scale = 4
    $canvas = [Drawing.Bitmap]::new($size * $scale, $size * $scale)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.ScaleTransform($size * $scale / 256.0, $size * $scale / 256.0)
    $navy = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#151D2B'))
    $face = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#1B2B37'))
    $mint = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#9CE6C5'))
    $gridPen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#3C6268'), 5)
    $ringPen = [Drawing.Pen]::new($mint, [Math]::Max(12, 1.3 * 256 / $size))
    $handPen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#F1F4FA'), [Math]::Max(12, 1.4 * 256 / $size))
    $handPen.StartCap = $handPen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $handPen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $tickPen = [Drawing.Pen]::new($mint, 5)
    $tickPen.StartCap = $tickPen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $tile = [Drawing.Drawing2D.GraphicsPath]::new()
    $tile.AddArc(12, 12, 104, 104, 180, 90)
    $tile.AddArc(140, 12, 104, 104, 270, 90)
    $tile.AddArc(140, 140, 104, 104, 0, 90)
    $tile.AddArc(12, 140, 104, 104, 90, 90)
    $tile.CloseFigure()
    $graphics.FillPath($navy, $tile)
    $graphics.FillEllipse($face, 50, 50, 156, 156)
    if ($size -ge 32) {
        $graphics.DrawEllipse($gridPen, 96, 50, 64, 156)
        $graphics.DrawLine($gridPen, 50, 128, 206, 128)
        $graphics.DrawBezier($gridPen, 60, 90, 105.33, 107.33, 150.67, 107.33, 196, 90)
        $graphics.DrawBezier($gridPen, 60, 166, 105.33, 148.67, 150.67, 148.67, 196, 166)
    }
    $graphics.DrawEllipse($ringPen, 50, 50, 156, 156)
    if ($size -ge 48) {
        $graphics.DrawLine($tickPen, 128, 62, 128, 70)
        $graphics.DrawLine($tickPen, 194, 128, 186, 128)
        $graphics.DrawLine($tickPen, 128, 194, 128, 186)
        $graphics.DrawLine($tickPen, 62, 128, 70, 128)
    }
    $graphics.DrawLines($handPen, [Drawing.PointF[]]@([Drawing.PointF]::new(96,103), [Drawing.PointF]::new(128,128), [Drawing.PointF]::new(162,84)))
    $graphics.FillEllipse($mint, 119, 119, 18, 18)
    $graphics.Dispose()
    foreach ($resource in @($navy, $face, $mint, $gridPen, $ringPen, $handPen, $tickPen, $tile)) { $resource.Dispose() }
    $result = [Drawing.Bitmap]::new($size, $size)
    $resizer = [Drawing.Graphics]::FromImage($result)
    $resizer.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $resizer.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $resizer.DrawImage($canvas, 0, 0, $size, $size)
    $resizer.Dispose()
    $canvas.Dispose()
    return $result
}

$preview = New-IconBitmap 512
$preview.Save((Join-Path $assetDirectory 'AppIcon.png'), [Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    $memory = [IO.MemoryStream]::new()
    $bitmap.Save($memory, [Drawing.Imaging.ImageFormat]::Png)
    $frames += ,$memory.ToArray()
    $bitmap.Dispose()
    $memory.Dispose()
}

$stream = [IO.File]::Create((Join-Path $assetDirectory 'AppIcon.ico'))
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
Write-Output "Created AppIcon.ico (9 sizes, 16-256px) and AppIcon.png (512px)."
