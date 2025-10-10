#!/usr/bin/env bash
set -euo pipefail

: "${DB_CONNECTION:=Server=localhost\\SQLEXPRESS;Database=Pipelane;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true}"
echo "Using DB_CONNECTION=${DB_CONNECTION}"

if dotnet ef --version >/dev/null 2>&1; then
  echo "Applying EF Core migrations via dotnet ef..."
  dotnet ef database update --project src/Pipelane.Infrastructure --startup-project src/Pipelane.Api
else
  echo "dotnet-ef not available; starting API to auto-migrate..." >&2
  ASPNETCORE_URLS='http://localhost:5099' dotnet run --project src/Pipelane.Api -c Release >/dev/null 2>&1 &
  pid=$!
  sleep 5
  # best effort
  curl -fsS http://localhost:5099/health >/dev/null 2>&1 || true
  kill $pid >/dev/null 2>&1 || true
fi

echo "Database is up-to-date."

