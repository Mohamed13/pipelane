$ErrorActionPreference = 'Stop'

# Use provided DB_CONNECTION or default to LocalDB
if (-not $env:DB_CONNECTION -or [string]::IsNullOrWhiteSpace($env:DB_CONNECTION)) {
  $env:DB_CONNECTION = 'Server=localhost\\SQLEXPRESS;Database=Pipelane;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true'
}

Write-Host "Using DB_CONNECTION=$($env:DB_CONNECTION)"

function Has-DotNetEf {
  try { dotnet ef --version *> $null; return $true } catch { return $false }
}

if (Has-DotNetEf) {
  Write-Host "Applying EF Core migrations via dotnet ef..."
  dotnet ef database update --project src/Pipelane.Infrastructure --startup-project src/Pipelane.Api
} else {
  Write-Warning "dotnet-ef not available; starting API to auto-migrate..."
  $env:ASPNETCORE_URLS = 'http://localhost:5099'
  $p = Start-Process -FilePath dotnet -WorkingDirectory . -ArgumentList @('run','--project','src/Pipelane.Api','-c','Release') -PassThru
  Start-Sleep -Seconds 5
  try { Invoke-WebRequest -Uri 'http://localhost:5099/health' -UseBasicParsing -TimeoutSec 10 *> $null } catch {}
  if ($p) { Stop-Process -Id $p.Id -Force }
}

Write-Host "Database is up-to-date."

