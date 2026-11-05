param(
    [string]$Mode = '--stopwatch-appearance-only',
    [string]$LogPath = '.superpowers/sdd/2026-10-31-stopwatch-overlay/native-hidden.log'
)
$ErrorActionPreference='Stop'
$repoRoot=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$logFullPath=[System.IO.Path]::GetFullPath((Join-Path $repoRoot $LogPath))
if(-not $logFullPath.StartsWith($repoRoot+[System.IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Log must stay within the repository.'}
if($Mode -notmatch '^--[a-z-]+$'){throw 'Expected one regression mode flag.'}
$testArguments=if($Mode -eq '--all'){''}else{$Mode}

# Create a private desktop in the current window station. Never call SwitchDesktop.
# Child windows, owned popups and dialogs stay on that desktop for their entire lifetime.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HiddenRegressionDesktop {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Startup {
  public int cb; public string reserved; public string desktop; public string title;
  public int x,y,xSize,ySize,xCount,yCount,fill,flags; public short show,reserved2;
  public IntPtr reservedPtr,input,output,error;
 }
 [StructLayout(LayoutKind.Sequential)] public struct ProcessInfo { public IntPtr process,thread; public uint processId,threadId; }
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
 [DllImport("user32.dll",SetLastError=true)] public static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool CreateProcess(string application,string command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref Startup startup,out ProcessInfo info);
 [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll")] public static extern bool GetExitCodeProcess(IntPtr process,out uint code);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll")] public static extern bool TerminateProcess(IntPtr process,uint code);
}
'@
$desktopName='StopwatchChecks_'+[Guid]::NewGuid().ToString('N')
$desktop=[HiddenRegressionDesktop]::CreateDesktop($desktopName,[IntPtr]::Zero,[IntPtr]::Zero,0,0x10000000,[IntPtr]::Zero)
if($desktop -eq [IntPtr]::Zero){throw "Cannot create hidden desktop: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"}
$runnerPath=Join-Path ([System.IO.Path]::GetDirectoryName($logFullPath)) 'hidden-native-child.ps1'
$escapeRoot=$repoRoot.Replace("'","''");$escapeLog=$logFullPath.Replace("'","''")
$runner=@"
Set-Location -LiteralPath '$escapeRoot'
Copy-Item -LiteralPath 'tools/DesktopRegressionChecks/bin/Debug/net10.0/DesktopRegressionChecks.dll' -Destination 'StopwatchOverlay.Desktop/bin/Debug/net10.0/DesktopRegressionChecks.dll'
Copy-Item -LiteralPath 'tools/DesktopRegressionChecks/bin/Debug/net10.0/DesktopRegressionChecks.pdb' -Destination 'StopwatchOverlay.Desktop/bin/Debug/net10.0/DesktopRegressionChecks.pdb'
dotnet exec --runtimeconfig StopwatchOverlay.Desktop/bin/Debug/net10.0/StopwatchOverlay.Desktop.runtimeconfig.json --depsfile StopwatchOverlay.Desktop/bin/Debug/net10.0/StopwatchOverlay.Desktop.deps.json StopwatchOverlay.Desktop/bin/Debug/net10.0/DesktopRegressionChecks.dll $testArguments *> '$escapeLog'
exit `$LASTEXITCODE
"@
if($Mode -eq '--unit-tests'){
 $unitResultsPath=(Join-Path ([System.IO.Path]::GetDirectoryName($logFullPath)) 'unit-results').Replace("'","''")
 $runner=@"
Set-Location -LiteralPath '$escapeRoot'
dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=unit-tests.trx' --results-directory '$unitResultsPath' *> '$escapeLog'
exit `$LASTEXITCODE
"@
}
[System.IO.File]::WriteAllText($runnerPath,$runner)
$info=New-Object HiddenRegressionDesktop+ProcessInfo
$startup=New-Object HiddenRegressionDesktop+Startup
$startup.cb=[Runtime.InteropServices.Marshal]::SizeOf($startup)
$startup.desktop='WinSta0\'+$desktopName
$startup.flags=1;$startup.show=0
$shellPath=(Get-Process -Id $PID).Path
$command='"'+$shellPath+'" -NoProfile -NonInteractive -WindowStyle Hidden -File "'+$runnerPath+'"'
$exitCode=1
try {
 if(-not [HiddenRegressionDesktop]::CreateProcess($shellPath,$command,[IntPtr]::Zero,[IntPtr]::Zero,$false,0x08000000,[IntPtr]::Zero,$repoRoot,[ref]$startup,[ref]$info)){throw "Hidden process failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"}
 Write-Output "Native fixture isolated on $desktopName (PID $($info.processId)); active desktop untouched."
 # The tool yields while this launcher waits; the fixture has a bounded lifetime.
 $wait=[HiddenRegressionDesktop]::WaitForSingleObject($info.process,300000)
if($wait -eq 258){& "$env:SystemRoot/System32/taskkill.exe" /PID $info.processId /T /F | Out-Null;throw 'Hidden native fixture exceeded five minutes.'}
 [uint32]$nativeExit=1;[void][HiddenRegressionDesktop]::GetExitCodeProcess($info.process,[ref]$nativeExit)
 $exitCode=[int]$nativeExit
 if(Test-Path -LiteralPath $logFullPath){Get-Content -LiteralPath $logFullPath -Tail 20}
} finally {
 if($info.thread -ne [IntPtr]::Zero){[void][HiddenRegressionDesktop]::CloseHandle($info.thread)}
 if($info.process -ne [IntPtr]::Zero){[void][HiddenRegressionDesktop]::CloseHandle($info.process)}
 [void][HiddenRegressionDesktop]::CloseDesktop($desktop)
}
exit $exitCode
