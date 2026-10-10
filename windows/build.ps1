param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework 4.x C# compiler was not found.'
}

$projectRoot = Join-Path $PSScriptRoot 'HiddenType.Windows'
$outputDirectory = Join-Path $PSScriptRoot "dist\$Configuration"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$sources = Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -Recurse | ForEach-Object { $_.FullName }
$options = @(
    '/nologo', '/target:winexe', '/platform:anycpu', ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $projectRoot 'app.ico')),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', ('/out:' + (Join-Path $outputDirectory 'HiddenType.exe'))
)
if ($Configuration -eq 'Release') { $options += '/optimize+' } else { $options += '/debug+' }

& $compiler $options $sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }
Write-Host "Built $outputDirectory\HiddenType.exe"
