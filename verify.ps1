param([string]$Compiler = 'J:\codex-tools\probably-stolen-mod\compiler\tasks\net472\csc.exe')
$ErrorActionPreference = 'Stop'
$outDir = Join-Path $PSScriptRoot 'outputs\verification'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$output = Join-Path $outDir 'RegressionTests.exe'
$arguments = @('/noconfig','/target:exe','/langversion:9','/utf8output','/warnaserror+',
    '/reference:System.Web.Extensions.dll','/reference:System.Core.dll','/reference:System.dll',"/out:$output")
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'verify') -Filter '*.cs' | ForEach-Object FullName
& $Compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Regression harness compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Regression verification failed.' }
