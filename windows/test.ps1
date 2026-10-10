$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputDirectory = Join-Path $PSScriptRoot 'dist\tests'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

& $compiler /nologo /target:exe /out:$outputDirectory\HiddenType.Tests.exe `
    $PSScriptRoot\HiddenType.Windows\Services\NoteFileCodec.cs `
    $PSScriptRoot\HiddenType.Windows.Tests\Program.cs
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed with exit code $LASTEXITCODE." }
& $outputDirectory\HiddenType.Tests.exe
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
