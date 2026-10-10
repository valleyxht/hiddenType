$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$bundleDirectory = Join-Path $projectRoot 'dist\hidden-type'
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'manifest.json') -Raw | ConvertFrom-Json
$zipPath = Join-Path $projectRoot ("dist\hidden-type-{0}.zip" -f $manifest.version)

Push-Location $projectRoot
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Plugin build failed with exit code $LASTEXITCODE." }

    New-Item -ItemType Directory -Force -Path $bundleDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'main.js') -Destination $bundleDirectory -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'manifest.json') -Destination $bundleDirectory -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'styles.css') -Destination $bundleDirectory -Force
    Compress-Archive -LiteralPath $bundleDirectory -DestinationPath $zipPath -Force
    Write-Host "Created $zipPath"
}
finally {
    Pop-Location
}
