$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Starting KeyVaultComparer..." -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Check if Azure CLI is logged in
$azStatus = az account show 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: Azure CLI is not logged in. Ensure you have configured authentication via Visual Studio, Rider, or Environment Variables." -ForegroundColor Yellow
} else {
    Write-Host "Azure CLI is logged in successfully." -ForegroundColor Green
}

# Start Backend
Write-Host "Starting Backend API (.NET 10)..." -ForegroundColor Yellow
$backendProcess = Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory ".\KeyVaultComparer.Api" -PassThru -NoNewWindow
Start-Sleep -Seconds 3

# Start Frontend
Write-Host "Starting Frontend UI (Vue + Vite)..." -ForegroundColor Yellow
$frontendProcess = Start-Process -FilePath "npm.cmd" -ArgumentList "run dev" -WorkingDirectory ".\keyvaultcomparer-ui" -PassThru -NoNewWindow

Write-Host "Services started successfully!" -ForegroundColor Green
Write-Host "- API is running at http://localhost:5065" -ForegroundColor Green
Write-Host "- UI is running at http://localhost:5173" -ForegroundColor Green
Write-Host "Press Ctrl+C to terminate both processes." -ForegroundColor Cyan

try {
    # Keep script alive to hold processes
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    Write-Host "`nStopping services..." -ForegroundColor Yellow
    if ($backendProcess -and !$backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force
    }
    if ($frontendProcess -and !$frontendProcess.HasExited) {
        Stop-Process -Id $frontendProcess.Id -Force
    }
    Write-Host "Services stopped." -ForegroundColor Green
}
