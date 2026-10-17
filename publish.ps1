$ErrorActionPreference = 'Stop'

$projectFile = Join-Path $PSScriptRoot 'StopwatchOverlay.Desktop/StopwatchOverlay.Desktop.csproj'
$publishDir = Join-Path $PSScriptRoot 'dist/desktop-windows'
$publishExe = Join-Path $publishDir 'StopwatchOverlay.Desktop.exe'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar

Get-Command dotnet -ErrorAction Stop | Out-Null

# Close this project's legacy and Desktop instances to release output locks
# and avoid running two applications against the same timer workspace.
$running = Get-Process -Name StopwatchOverlay, StopwatchOverlay.Desktop -ErrorAction SilentlyContinue
foreach ($appProcess in $running) {
    if ($appProcess.Path -and $appProcess.Path.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "Closing $($appProcess.ProcessName) (PID: $($appProcess.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $appProcess.Id -Force -ErrorAction Stop
        if (-not $appProcess.WaitForExit(5000)) {
            throw "Stopwatch process $($appProcess.Id) did not exit."
        }
    }
}

Write-Host 'Publishing StopwatchOverlay Desktop Release build (win-x64)...' -ForegroundColor Cyan
dotnet publish $projectFile -c Release -r win-x64 --self-contained false -m:1 `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $publishExe -PathType Leaf)) {
    throw "Publish did not produce $publishExe."
}

Write-Host "`nPublish succeeded! Starting StopwatchOverlay Desktop..." -ForegroundColor Green
Start-Process -FilePath $publishExe -WorkingDirectory $publishDir -WindowStyle Normal
