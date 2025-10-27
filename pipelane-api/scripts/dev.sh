#!/usr/bin/env bash
set -euo pipefail
export ASPNETCORE_URLS=http://localhost:56667
dotnet run --project src/Pipelane.Api
