param(
    [Parameter(Mandatory=$true)][string]$Background,
    [Parameter(Mandatory=$true)][string[]]$Icons,
    [Parameter(Mandatory=$true)][string]$OutputStem
)
$ErrorActionPreference='Stop'
if ($Icons.Count -ne 4) { throw 'Supply exactly four original resource icons in display order.' }
Add-Type -AssemblyName System.Drawing
$canvas=[System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Background).Path)
$graphics=[System.Drawing.Graphics]::FromImage($canvas)
try {
    $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $cellWidth=$canvas.Width * 0.24
    $rowCenter=$canvas.Height * 0.72
    for ($i=0; $i -lt 4; $i++) {
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
            $scale=[Math]::Max(1,[Math]::Floor([Math]::Min(($cellWidth*0.88)/$w,($canvas.Height*0.22)/$h)))
            $dw=[int]($w*$scale); $dh=[int]($h*$scale)
            $centerX=$canvas.Width*(0.14+$i*0.24)
            $destination=New-Object System.Drawing.Rectangle([int]($centerX-$dw/2),[int]($rowCenter-$dh/2),$dw,$dh)
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
