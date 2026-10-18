$ErrorActionPreference = 'Stop'
$publishScript = Join-Path $PSScriptRoot '../publish.ps1'
$expectedExe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../dist/desktop-windows/StopwatchOverlay.Desktop.exe'))

# Replace external effects so the actual publisher can be tested without
# compiling, closing the user's app, changing the registry, or launching windows.
function Get-Process { @() }
function Get-ItemPropertyValue { if ($global:publishTest_startupEnabled) { '"C:\old\StopwatchOverlay.exe"' } }
function Set-ItemProperty {
    param($LiteralPath, $Name, $Value, $Type)
    if ($LiteralPath -ne 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -or $Name -ne 'StopwatchOverlay') {
        throw 'Unexpected startup registry target.'
    }
    $global:publishTest_registeredCommand = $Value
}
function dotnet { $global:LASTEXITCODE = $global:publishTest_publishExitCode }
function Test-Path { $global:publishTest_executableExists }
function Start-Process { $global:publishTest_launched = $true }

foreach ($case in @(
    @{ Name = 'enabled startup follows the published executable'; Enabled = $true; ExitCode = 0; Exists = $true; Fails = $false },
    @{ Name = 'disabled startup stays disabled'; Enabled = $false; ExitCode = 0; Exists = $true; Fails = $false },
    @{ Name = 'failed publish leaves startup untouched'; Enabled = $true; ExitCode = 1; Exists = $true; Fails = $true },
    @{ Name = 'missing executable leaves startup untouched'; Enabled = $true; ExitCode = 0; Exists = $false; Fails = $true }
)) {
    $global:publishTest_startupEnabled = $case.Enabled
    $global:publishTest_publishExitCode = $case.ExitCode
    $global:publishTest_executableExists = $case.Exists
    $global:publishTest_registeredCommand = $null
    $global:publishTest_launched = $false
    $failed = $false
    try { & $publishScript } catch { $failed = $true }
    if ($failed -ne $case.Fails) { throw "Unexpected publish result: $($case.Name)" }
    $expectedCommand = if ($case.Enabled -and -not $case.Fails) { '"' + $expectedExe + '"' } else { $null }
    if ($global:publishTest_registeredCommand -ne $expectedCommand) { throw "Incorrect startup command: $($case.Name)" }
    if ($global:publishTest_launched -eq $case.Fails) { throw "Incorrect launch result: $($case.Name)" }
    Write-Host "PASS $($case.Name)"
}
