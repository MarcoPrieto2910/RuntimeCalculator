$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "OMAX Runtime Collector - Publish"
Write-Host "========================================"
Write-Host ""

cd..

dotnet publish `
    -c Release `
    -r win-x86 `
    --self-contained true

Write-Host ""
Write-Host "Publish completed."
Write-Host ""