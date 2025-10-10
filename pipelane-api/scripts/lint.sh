#!/usr/bin/env bash
set -euo pipefail
dotnet build Pipelane.sln -c Debug
dotnet format --verify-no-changes

