param(
    [Parameter(Mandatory=$true)][string]$GeneratedImage,
    [string]$SourceFileName = 'printer-crate-generated.png',
    [string]$ProjectRoot = '',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$SpriteKey = 'printer_crate',
    [ValidateSet('HighQualityBicubic','NearestNeighbor')]
    [string]$ResizeFilter = 'HighQualityBicubic',
    [ValidateRange(1,1.5)][double]$VisualScale = 1,
    [ValidateRange(0,0.5)][double]$EdgeSharpness = 0,
    [switch]$ReuseSource,
    [switch]$ReducePalette
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
$sourceDir = Join-Path $projectRoot 'assets\source'
$itemDir = Join-Path $projectRoot 'content\Textures\Items'
New-Item -ItemType Directory -Path $sourceDir,$itemDir -Force | Out-Null
if ([System.IO.Path]::GetFileName($SourceFileName) -ne $SourceFileName) { throw 'SourceFileName must be a filename.' }
$sourceTarget = Join-Path $sourceDir $SourceFileName
if (Test-Path -LiteralPath $sourceTarget) {
    if (!$ReuseSource -or (Get-FileHash -LiteralPath $sourceTarget -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $GeneratedImage -Algorithm SHA256).Hash) {
        throw 'Source exists; reuse requires an identical file. Source image was not overwritten.'
    }
} else { Copy-Item -LiteralPath $GeneratedImage -Destination $sourceTarget }
$source = [System.Drawing.Bitmap]::FromFile($sourceTarget)
$target = New-Object System.Drawing.Bitmap(48,48,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($target)
try {
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::$ResizeFilter
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.DrawImage($source,[System.Drawing.Rectangle]::new(0,0,48,48),0,0,$source.Width,$source.Height,[System.Drawing.GraphicsUnit]::Pixel)
    if ($VisualScale -gt 1) {
        # Find visible bounds on the small first pass, ignoring barely visible alpha noise.
        # Re-render directly from the ORIGINAL source, centred on the object, only once.
        $minX = 48; $minY = 48; $maxX = -1; $maxY = -1
        for ($y = 0; $y -lt 48; $y++) { for ($x = 0; $x -lt 48; $x++) {
            if ($target.GetPixel($x,$y).A -ge 64) {
                $minX = [Math]::Min($minX,$x); $maxX = [Math]::Max($maxX,$x)
                $minY = [Math]::Min($minY,$y); $maxY = [Math]::Max($maxY,$y)
            }
        } }
        if ($maxX -lt 0 -or ($maxX-$minX+1)*$VisualScale -gt 48 -or ($maxY-$minY+1)*$VisualScale -gt 48) {
            throw 'Requested visual scale would clip the visible object.'
        }
        $cx = ($minX+$maxX+1)/2.0*$source.Width/48.0
        $cy = ($minY+$maxY+1)/2.0*$source.Height/48.0
        $viewWidth = $source.Width/$VisualScale; $viewHeight = $source.Height/$VisualScale
        $view = [System.Drawing.RectangleF]::new($cx-$viewWidth/2,$cy-$viewHeight/2,$viewWidth,$viewHeight)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.DrawImage($source,[System.Drawing.RectangleF]::new(0,0,48,48),$view,[System.Drawing.GraphicsUnit]::Pixel)
    }
    if ($EdgeSharpness -gt 0) {
        # Sharpen strong boundaries only. Alpha-aware averaging avoids dark fringes;
        # a smooth alpha curve tightens the silhouette without binary cutout edges.
        $sample = $target.Clone()
        try {
            for ($y = 0; $y -lt 48; $y++) { for ($x = 0; $x -lt 48; $x++) {
                $pixel = $sample.GetPixel($x,$y)
                if ($pixel.A -eq 0) { continue }
                $r = [int]$pixel.R; $g = [int]$pixel.G; $b = [int]$pixel.B
                if ($pixel.A -ge 128) {
                    $sr = 0.0; $sg = 0.0; $sb = 0.0; $weight = 0.0
                    $low = 255.0; $high = 0.0
                    for ($dy = -1; $dy -le 1; $dy++) { for ($dx = -1; $dx -le 1; $dx++) {
                        $nx = [Math]::Max(0,[Math]::Min(47,$x+$dx)); $ny = [Math]::Max(0,[Math]::Min(47,$y+$dy))
                        $near = $sample.GetPixel($nx,$ny)
                        $w = (2-[Math]::Abs($dx))*(2-[Math]::Abs($dy))*$near.A/255.0
                        $sr += $near.R*$w; $sg += $near.G*$w; $sb += $near.B*$w; $weight += $w
                        if ($near.A -ge 128) {
                            $luma = .2126*$near.R+.7152*$near.G+.0722*$near.B
                            $low = [Math]::Min($low,$luma); $high = [Math]::Max($high,$luma)
                        }
                    } }
                    if ($weight -gt 0 -and $high-$low -ge 24) {
                        $dr = $pixel.R-$sr/$weight; $dg = $pixel.G-$sg/$weight; $db = $pixel.B-$sb/$weight
                        if ([Math]::Abs(.2126*$dr+.7152*$dg+.0722*$db) -ge 3) {
                            $r = [int][Math]::Round([Math]::Max(0,[Math]::Min(255,$r+$EdgeSharpness*$dr)))
                            $g = [int][Math]::Round([Math]::Max(0,[Math]::Min(255,$g+$EdgeSharpness*$dg)))
                            $b = [int][Math]::Round([Math]::Max(0,[Math]::Min(255,$b+$EdgeSharpness*$db)))
                        }
                    }
                }
                $a = $pixel.A/255.0
                $alpha = [int][Math]::Round(255*$a*$a*(3-2*$a))
                $target.SetPixel($x,$y,[System.Drawing.Color]::FromArgb($alpha,$r,$g,$b))
            } }
        } finally { $sample.Dispose() }
    }
    if ($ReducePalette) {
        # No dithering: convert the generated art into solid native-sized pixel clusters.
        $palette = @('151820','242832','343A45','474E58','5C6570','737E88','909CA3','B4BFC0',
            'D7DCDA','213A45','2D515D','3F6B78','588894','78A5AF','A1C2C5',
            '4B3828','725134','A27645','CAA164','E5C389') | ForEach-Object {
                [System.Drawing.ColorTranslator]::FromHtml('#' + $_)
            }
        for ($y = 0; $y -lt 48; $y++) {
            for ($x = 0; $x -lt 48; $x++) {
                $pixel = $target.GetPixel($x,$y)
                if ($pixel.A -lt 128) { $target.SetPixel($x,$y,[System.Drawing.Color]::Transparent); continue }
                $best = $palette[0]; $bestDistance = [double]::PositiveInfinity
                foreach ($color in $palette) {
                    $distance = [Math]::Pow($pixel.R-$color.R,2) + [Math]::Pow($pixel.G-$color.G,2) + [Math]::Pow($pixel.B-$color.B,2)
                    if ($distance -lt $bestDistance) { $bestDistance = $distance; $best = $color }
                }
                $target.SetPixel($x,$y,$best)
            }
        }
    }
    $target.Save((Join-Path $itemDir ($SpriteKey + '.png')),[System.Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose(); $target.Dispose(); $source.Dispose() }
