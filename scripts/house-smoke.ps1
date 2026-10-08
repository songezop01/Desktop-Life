param([switch]$NativeWorker,[string]$NativeResult,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/native-profile-context.ps1"
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('artifacts/verification/0.11/house-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if(![IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path $root $OutputDirectory}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(!$NativeWorker){Invoke-DesktopLifeNative $PSCommandPath @('-OutputDirectory',$OutputDirectory);return}
$process=$null
try{
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $env:DOTNET_ROOT=Join-Path $root 'tools/dotnet'
    $exe=Join-Path $root 'src/DesktopLife.App/bin/Release/net10.0-windows/DesktopLife.App.exe'
    # The executable loads the PerMonitorV2 manifest; launching the DLL through dotnet would skip it.
    $process=Start-Process -FilePath $exe -ArgumentList '--house-test' -WorkingDirectory $root -WindowStyle Hidden -PassThru
    $handle=$process.Handle
    $finished=$process.WaitForExit(120000)
    if(!$finished){$process.Kill();$process.WaitForExit()}
    $marker=Get-ChildItem (Join-Path $env:TEMP 'DesktopLifeSmoke') -Filter diagnostic-process.json -Recurse | Where-Object {(Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json).ProcessId -eq $process.Id}|Select-Object -First 1
    if($marker){Copy-Item -LiteralPath $marker.DirectoryName -Destination (Join-Path $OutputDirectory 'isolated-run') -Recurse -Force}
    if(!$finished){throw 'Isolated house diagnostic timed out; installed application was not touched.'}
    if($process.ExitCode -ne 0){throw ('House diagnostic failed, exit '+$process.ExitCode+'. See isolated-run/logs/app.log.')}
    if(!$marker -or !(Test-Path -LiteralPath (Join-Path $OutputDirectory 'isolated-run/house-summary.json'))){throw 'House summary is missing.'}
    Write-DesktopLifeNativeResult $NativeResult $true $OutputDirectory
}catch{
    if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}
    $_ | Out-String | Set-Content -LiteralPath ($NativeResult+'.log') -Encoding UTF8
    Write-DesktopLifeNativeResult $NativeResult $false $_.Exception.Message
    throw
}
