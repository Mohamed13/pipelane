#!/usr/bin/env bash
set -euo pipefail
export ASPNETCORE_URLS=http://localhost:5000
dotnet run --project src/Pipelane.Api

