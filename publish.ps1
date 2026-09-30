# Stop any running StopwatchOverlay process before publishing to release the file lock
$running = Get-Process -Name StopwatchOverlay -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Closing running StopwatchOverlay process (PID: $($running.Id))..." -ForegroundColor Yellow
    Stop-Process -Name StopwatchOverlay -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
}

Write-Host "Publishing StopwatchOverlay Release build..." -ForegroundColor Cyan
dotnet publish StopwatchOverlay\StopwatchOverlay.csproj -c Release -r win-x64 --no-self-contained

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublish succeeded! Starting StopwatchOverlay..." -ForegroundColor Green
    $publishDir = "$PSScriptRoot\StopwatchOverlay\bin\Release\net10.0-windows\win-x64\publish"
    $publishExe = "$publishDir\StopwatchOverlay.exe"
    Start-Process -FilePath $publishExe -WorkingDirectory $publishDir
} else {
    Write-Host "`nPublish failed with exit code $LASTEXITCODE." -ForegroundColor Red
}
