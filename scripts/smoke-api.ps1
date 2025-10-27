param(
    [string]$BaseUrl = "https://localhost:56667"
)

$ErrorActionPreference = "Stop"
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

Write-Host "🔎 Vérification API sur $BaseUrl"

try {
    $health = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -TimeoutSec 10
    if ($health.StatusCode -ne 200) {
        throw "Endpoint /health renvoie $($health.StatusCode)"
    }

    $metricsResponse = Invoke-WebRequest -Uri "$BaseUrl/health/metrics" -UseBasicParsing -TimeoutSec 10
    if ($metricsResponse.StatusCode -ne 200) {
        throw "Endpoint /health/metrics renvoie $($metricsResponse.StatusCode)"
    }

    $metrics = $metricsResponse.Content | ConvertFrom-Json
    Write-Host " • queueDepth       = $($metrics.queueDepth)"
    Write-Host " • avgSendLatencyMs = $([math]::Round($metrics.avgSendLatencyMs,2))"
    Write-Host " • deadWebhookBacklog = $($metrics.deadWebhookBacklog)"

    if ($metrics.queueDepth -lt 0 -or $metrics.deadWebhookBacklog -lt 0) {
        throw "Les métriques retournent des valeurs négatives"
    }

    Write-Host "✅ API disponible"
}
catch {
    Write-Error "❌ Smoke API en échec : $_"
    exit 1
}
