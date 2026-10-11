param([string]$Path)
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($Path)
$minX = 99999; $maxX = -1; $minY = 99999; $maxY = -1; $count = 0
$first = @()
for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -eq 240 -and $c.G -eq 240 -and $c.B -eq 240) {
            $count++
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
            if ($first.Count -lt 10) { $first += "$x,$y" }
        }
    }
}
Write-Output "count=$count x=$minX-$maxX y=$minY-$maxY"
Write-Output ("primi: " + ($first -join ' '))
