$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$iconImages = @()
$script:blue = [System.Drawing.Color]::FromArgb(21, 59, 104)
$script:navy = [System.Drawing.Color]::FromArgb(12, 31, 55)
$script:mint = [System.Drawing.Color]::FromArgb(19, 166, 132)
$script:white = [System.Drawing.Color]::White

function New-RoundedPath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc(($x + $width - $diameter), $y, $diameter, $diameter, 270, 90)
    $path.AddArc(($x + $width - $diameter), ($y + $height - $diameter), $diameter, $diameter, 0, 90)
    $path.AddArc($x, ($y + $height - $diameter), $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-Mark([System.Drawing.Graphics]$graphics, [float]$x, [float]$y, [float]$size) {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $background = New-RoundedPath $x $y $size $size ($size * 0.23)
    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.RectangleF]::new($x, $y, $size, $size)),
        $script:blue,
        $script:navy,
        45
    )
    $graphics.FillPath($backgroundBrush, $background)

    $unit = $size / 256.0
    $paperX = $x + 46 * $unit
    $paperY = $y + 34 * $unit
    $paperWidth = 150 * $unit
    $paperHeight = 162 * $unit
    if ($paperX -is [array] -or $unit -is [array]) {
        throw "Icon scaling produced invalid values: x=$($paperX.GetType().FullName), unit=$($unit.GetType().FullName), size=$($size.GetType().FullName)"
    }
    $paperPath = New-RoundedPath $paperX $paperY $paperWidth $paperHeight (12 * $unit)
    $paperBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 250, 253))
    $graphics.FillPath($paperBrush, $paperPath)

    $headerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 101, 156))
    $headerRect = [System.Drawing.RectangleF]::new($paperX, $paperY, $paperWidth, (43 * $unit))
    $graphics.FillPath($headerBrush, (New-RoundedPath $paperX $paperY $paperWidth (48 * $unit) (12 * $unit)))
    $coverBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 101, 156))
    $graphics.FillRectangle($coverBrush, $paperX, ($paperY + 30 * $unit), $paperWidth, 18 * $unit)

    $ringBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(195, 224, 238))
    foreach ($ringX in @(($paperX + (30 * $unit)), ($paperX + (114 * $unit)))) {
        $graphics.FillEllipse($ringBrush, $ringX, ($paperY - 6 * $unit), 7 * $unit, 22 * $unit)
    }

    $lineBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(159, 185, 203))
    $softBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 226, 236))
    foreach ($row in 0..2) {
        $rowY = $paperY + (68 + $row * 31) * $unit
        $graphics.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(216, 235, 243))), (New-RoundedPath ($paperX + 17 * $unit) $rowY (17 * $unit) (17 * $unit) (5 * $unit)))
        $graphics.FillPath($lineBrush, (New-RoundedPath ($paperX + 44 * $unit) ($rowY + 2 * $unit) ((72 - $row * 9) * $unit) (5 * $unit) (2.5 * $unit)))
        $graphics.FillPath($softBrush, (New-RoundedPath ($paperX + 44 * $unit) ($rowY + 12 * $unit) ((52 - $row * 5) * $unit) (4 * $unit) (2 * $unit)))
    }

    $badgeSize = 81 * $unit
    $badgeX = $x + 137 * $unit
    $badgeY = $y + 139 * $unit
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(31, 0, 10, 20))
    $graphics.FillEllipse($shadowBrush, ($badgeX + 3 * $unit), ($badgeY + 5 * $unit), $badgeSize, $badgeSize)
    $badgeBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.RectangleF]::new($badgeX, $badgeY, $badgeSize, $badgeSize)),
        ([System.Drawing.Color]::FromArgb(72, 209, 176)),
        $script:mint,
        45
    )
    $graphics.FillEllipse($badgeBrush, $badgeX, $badgeY, $badgeSize, $badgeSize)
    $checkPen = New-Object System.Drawing.Pen($script:white, (9 * $unit))
    $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $checkPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $checkPoints = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(($badgeX + 19 * $unit), ($badgeY + 41 * $unit)),
        [System.Drawing.PointF]::new(($badgeX + 34 * $unit), ($badgeY + 56 * $unit)),
        [System.Drawing.PointF]::new(($badgeX + 61 * $unit), ($badgeY + 27 * $unit))
    )
    $graphics.DrawLines($checkPen, $checkPoints)
}

function New-PngBytes([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    Draw-Mark $graphics 0 0 $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    return ,$bytes
}

$png = New-PngBytes 256
[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'PresenzeXmlConverter.png'), $png)

$iconStream = New-Object System.IO.MemoryStream
$iconWriter = New-Object System.IO.BinaryWriter($iconStream)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$iconWriter.Write([UInt16]0)
$iconWriter.Write([UInt16]1)
$iconWriter.Write([UInt16]$sizes.Count)
$imageOffset = 6 + (16 * $sizes.Count)
foreach ($size in $sizes) {
    $image = New-PngBytes $size
    $iconWriter.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
    $iconWriter.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
    $iconWriter.Write([byte]0)
    $iconWriter.Write([byte]0)
    $iconWriter.Write([UInt16]1)
    $iconWriter.Write([UInt16]32)
    $iconWriter.Write([UInt32]$image.Length)
    $iconWriter.Write([UInt32]$imageOffset)
    $imageOffset += $image.Length
    $iconImages += ,$image
}
foreach ($image in $iconImages) {
    $iconWriter.Write($image)
}
[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'PresenzeXmlConverter.ico'), $iconStream.ToArray())
$iconWriter.Dispose()
$iconStream.Dispose()

$wizardBitmap = New-Object System.Drawing.Bitmap(164, 314, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$wizardGraphics = [System.Drawing.Graphics]::FromImage($wizardBitmap)
$wizardGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$wizardGraphics.Clear($script:navy)
$wizardBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    ([System.Drawing.Rectangle]::new(0, 0, 164, 314)),
    $script:blue,
    $script:navy,
    65
)
$wizardGraphics.FillRectangle($wizardBrush, 0, 0, 164, 314)
Draw-Mark $wizardGraphics 37 35 90
$accentPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 113, 149), 1)
foreach ($line in 0..4) {
    $wizardGraphics.DrawLine($accentPen, 17, (185 + $line * 23), 147, (185 + $line * 23))
}
$font = New-Object System.Drawing.Font('Segoe UI', 10, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Point)
$captionBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(229, 240, 250))
$wizardGraphics.DrawString('PRESENZE XML', $font, $captionBrush, 18, 191)
$subFont = New-Object System.Drawing.Font('Segoe UI', 8, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Point)
$subBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(177, 201, 222))
$wizardGraphics.DrawString('Conversione Zucchetti', $subFont, $subBrush, 18, 216)
$wizardBitmap.Save((Join-Path $assetDirectory 'WizardImage.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$wizardBitmap.Save((Join-Path $assetDirectory 'WizardImage.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$wizardGraphics.Dispose()
$wizardBitmap.Dispose()

$smallBitmap = New-Object System.Drawing.Bitmap(55, 55, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$smallGraphics = [System.Drawing.Graphics]::FromImage($smallBitmap)
$smallGraphics.Clear($script:navy)
Draw-Mark $smallGraphics 2 2 51
$smallBitmap.Save((Join-Path $assetDirectory 'WizardSmallImage.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$smallGraphics.Dispose()
$smallBitmap.Dispose()

Write-Output "Brand assets created in $assetDirectory"
