param(
    [string]$GameRoot = 'I:\SteamLibrary\steamapps\common\Probably Stolen',
    [string]$Compiler = 'J:\codex-tools\probably-stolen-mod\compiler\tasks\net472\csc.exe'
)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameRoot 'Probably Stolen_Data\Managed'
$packageRoot = Join-Path $PSScriptRoot 'package\ScavengerTeams'
foreach ($spriteKey in @('printer_crate','metal_crate','electronic_crate','ore_crate')) {
    if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot ('content\Textures\Items\' + $spriteKey + '.png')))) {
        throw "Missing crate art: $spriteKey. Refusing to package an incomplete mod."
    }
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$output = Join-Path $packageRoot 'ScavengerTeams.dll'
$arguments = @('/noconfig', '/nostdlib+', '/target:library', '/langversion:9', '/utf8output',
    '/deterministic+', '/optimize+', '/warnaserror+', "/out:$output")
$arguments += Get-ChildItem -LiteralPath $managed -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $Compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'content\manifest.xml') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'content\README.zh-CN.md') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'content\Textures') -Destination $packageRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'content\Localization') -Destination $packageRoot -Recurse -Force
foreach ($locale in @('zh-Hans','zh-CN')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'content\Localization\zh.csv') -Destination (Join-Path $packageRoot ('Localization\' + $locale + '.csv')) -Force
}
Get-FileHash -LiteralPath $output -Algorithm SHA256
