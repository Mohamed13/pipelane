$ErrorActionPreference = "Stop"

Write-Host "🛠️ Build Angular (smoke)"

$output = & npm --prefix pipelane-front run build 2>&1
$exitCode = $LASTEXITCODE
$output | ForEach-Object { Write-Host $_ }

if ($exitCode -ne 0) {
    Write-Error "❌ Build Angular en échec"
    exit $exitCode
}

if ($output -match "ERROR") {
    Write-Error "❌ Build Angular contient des erreurs dans les logs"
    exit 1
}

Write-Host "✅ Build Angular sans erreurs"
