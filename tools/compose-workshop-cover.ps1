param(
    [Parameter(Mandatory=$true)][string]$Background,
    [Parameter(Mandatory=$true)][string[]]$Icons,
    [Parameter(Mandatory=$true)][string]$OutputStem,
    [ValidateSet('row','staggered','five')][string]$Layout = 'staggered'
)
$ErrorActionPreference='Stop'
$expected = $(if ($Layout -eq 'five') { 5 } else { 4 })
if ($Icons.Count -ne $expected) { throw "Supply $expected resource icons in display order." }
Add-Type -AssemblyName System.Drawing
$canvas=[System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Background).Path)
$graphics=[System.Drawing.Graphics]::FromImage($canvas)
try {
    $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $maxWidth=$canvas.Width * $(if ($Layout -eq 'five') { 0.28 } elseif ($Layout -eq 'staggered') { 0.32 } else { 0.24*0.88 })
    $maxHeight=$canvas.Height * $(if ($Layout -eq 'five') { 0.24 } elseif ($Layout -eq 'staggered') { 0.26 } else { 0.22 })
    for ($i=0; $i -lt $expected; $i++) {
        $icon=New-Object System.Drawing.Bitmap((Resolve-Path -LiteralPath $Icons[$i]).Path)
        try {
            $left=$icon.Width; $top=$icon.Height; $right=-1; $bottom=-1
            for ($y=0; $y -lt $icon.Height; $y++) {
                for ($x=0; $x -lt $icon.Width; $x++) {
                    if ($icon.GetPixel($x,$y).A -gt 0) {
                        $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x)
                        $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y)
                    }
                }
            }
            if ($right -lt 0) { throw 'Empty source icon.' }
            $w=$right-$left+1; $h=$bottom-$top+1
            $scale=[Math]::Max(1,[Math]::Floor([Math]::Min($maxWidth/$w,$maxHeight/$h)))
            $dw=[int]($w*$scale); $dh=[int]($h*$scale)
            $centerX=$canvas.Width * $(if ($Layout -eq 'five') { @(0.18,0.50,0.82,0.34,0.66)[$i] } elseif ($Layout -eq 'staggered') { 0.17+$i*0.22 } else { 0.14+$i*0.24 })
            $centerY=$canvas.Height * $(if ($Layout -eq 'five') { if ($i -lt 3) { 0.61 } else { 0.85 } } elseif ($Layout -eq 'staggered') { if ($i%2 -eq 0) { 0.60 } else { 0.84 } } else { 0.72 })
            $destination=New-Object System.Drawing.Rectangle([int]($centerX-$dw/2),[int]($centerY-$dh/2),$dw,$dh)
            if ($destination.Left -lt 0 -or $destination.Right -gt $canvas.Width -or $destination.Top -lt 0 -or $destination.Bottom -gt $canvas.Height) {
                throw 'Icon placement exceeds canvas.'
            }
            $source=New-Object System.Drawing.Rectangle($left,$top,$w,$h)
            $graphics.DrawImage($icon,$destination,$source,[System.Drawing.GraphicsUnit]::Pixel)
        } finally { $icon.Dispose() }
    }
    $fullStem=[IO.Path]::GetFullPath($OutputStem)
    New-Item -ItemType Directory -Path (Split-Path $fullStem -Parent) -Force | Out-Null
    $canvas.Save($fullStem+'.png',[System.Drawing.Imaging.ImageFormat]::Png)
    $encoder=[System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $options=New-Object System.Drawing.Imaging.EncoderParameters(1)
    try {
        $options.Param[0]=New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality,[long]90)
        $canvas.Save($fullStem+'.jpg',$encoder,$options)
    } finally { $options.Dispose() }
    if ((Get-Item -LiteralPath ($fullStem+'.jpg')).Length -ge 1MB) { throw 'JPG exceeds the preview size limit.' }
    Write-Output ($fullStem+'.jpg')
} finally {
    $graphics.Dispose()
    $canvas.Dispose()
}
