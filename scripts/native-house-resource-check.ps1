param(
    [ValidateRange(60,28800)][int]$Seconds=600,
    [ValidateRange(1,3)][int]$Floors=3,
    [string]$OutputDirectory,
    [string]$BinarySource,
    [switch]$NativeWorker,
    [string]$NativeResult
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/native-profile-context.ps1"
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('artifacts/verification/0.11/native-house-resources-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if(![IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path $root $OutputDirectory}
if(!$BinarySource){$BinarySource=Join-Path $root 'src/DesktopLife.App/bin/Release/net10.0-windows'}
if(!$NativeWorker){Invoke-DesktopLifeNative $PSCommandPath @('-Seconds',[string]$Seconds,'-Floors',[string]$Floors,'-OutputDirectory',$OutputDirectory,'-BinarySource',$BinarySource);return}
function Get-NearestRank([double[]]$Values,[double]$Quantile){
    if(!$Values.Count){return $null}
    $ordered=@($Values|Sort-Object)
    return $ordered[[Math]::Max(0,[int][Math]::Ceiling($ordered.Count*$Quantile)-1)]
}
function Measure-ResourceRows($Rows){
    if(!$Rows.Count){return $null}
    $summary=[ordered]@{Samples=$Rows.Count;FirstSeconds=$Rows[0].Seconds;LastSeconds=$Rows[$Rows.Count-1].Seconds}
    foreach($metric in @('OneCoreCpuPercent','MachineCpuPercent','WorkingSetMB','PrivateMB','HandleCount','GdiObjects','UserObjects')){
        $values=@($Rows|ForEach-Object {$_.$metric})
        $summary[$metric]=[ordered]@{First=$values[0];Last=$values[$values.Count-1];Mean=($values|Measure-Object -Average).Average;P50=(Get-NearestRank $values .5);P95=(Get-NearestRank $values .95);Max=($values|Measure-Object -Maximum).Maximum}
    }
    return $summary
}
$process=$null;$marker=$null;$rawSamples=[Collections.Generic.List[object]]::new();$runSucceeded=$false
try{
    if($NativeResult){Start-Transcript -Path ($NativeResult+'.log') -Force|Out-Null}
    New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
    $binaryDirectory=Join-Path $OutputDirectory ('binary-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $binaryDirectory -Force|Out-Null
    Copy-Item -Path (Join-Path $BinarySource '*') -Destination $binaryDirectory -Recurse
    $executable=Join-Path $binaryDirectory 'DesktopLife.App.exe'
    if(!(Test-Path -LiteralPath $executable)){throw 'A built native apphost executable is required.'}
    $candidateVersion=(Get-Item -LiteralPath $executable).VersionInfo.FileVersion
    if([version]$candidateVersion -lt [version]'0.11'){throw 'Resource checks require a freshly built 0.11 or newer house candidate.'}
    $inventory=@(Get-ChildItem -LiteralPath $binaryDirectory -File|ForEach-Object {[ordered]@{Path=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
    [ordered]@{Seconds=$Seconds;Floors=$Floors;Presence='All';BinarySource=$BinarySource;Executable=$executable;FileVersion=(Get-Item -LiteralPath $executable).VersionInfo.FileVersion;SourceHashes=$inventory;Manifest='Native executable loads PerMonitorV2; no DLL-host fallback.'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'resource-binary.json') -Encoding UTF8
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DesktopLifeResourceSampler {
    [DllImport("user32.dll")]public static extern uint GetGuiResources(IntPtr process,uint flags);
}
'@
    $env:DOTNET_ROOT=Join-Path $root 'tools/dotnet'
    $startedUtc=[DateTimeOffset]::UtcNow
    $process=Start-Process -FilePath $executable -ArgumentList @('--stress-test',"--stress-seconds=$Seconds",'--stress-presence=All',"--stress-floors=$Floors") -WorkingDirectory $root -WindowStyle Hidden -PassThru
    $processHandle=$process.Handle
    $watch=[Diagnostics.Stopwatch]::StartNew();$previousCpu=0.0;$previousSeconds=0.0;$intervalStartedUtc=$startedUtc
    [ordered]@{ProcessId=$process.Id;StartedUtc=$startedUtc.ToString('O');Seconds=$Seconds;Floors=$Floors;NativeWorker=$true;InstalledApplicationTouched=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'resource-process.json') -Encoding UTF8
    while(!$process.HasExited){
        if($watch.Elapsed.TotalSeconds -gt $Seconds*1.5+120){$process.Kill();$process.WaitForExit();throw 'Owned isolated resource process exceeded its deadline.'}
        Start-Sleep -Seconds 1
        $process.Refresh();if($process.HasExited){break}
        $elapsed=$watch.Elapsed.TotalSeconds;$cpu=$process.TotalProcessorTime.TotalSeconds
        $oneCore=100*($cpu-$previousCpu)/[Math]::Max(.001,$elapsed-$previousSeconds)
        $sampledUtc=[DateTimeOffset]::UtcNow
        $sample=[pscustomobject]@{Seconds=$elapsed;Utc=$sampledUtc.ToString('O');IntervalStartedUtc=$intervalStartedUtc.ToString('O');OneCoreCpuPercent=$oneCore;MachineCpuPercent=$oneCore/[Environment]::ProcessorCount;WorkingSetMB=$process.WorkingSet64/1MB;PrivateMB=$process.PrivateMemorySize64/1MB;HandleCount=$process.HandleCount;GdiObjects=[DesktopLifeResourceSampler]::GetGuiResources($processHandle,0);UserObjects=[DesktopLifeResourceSampler]::GetGuiResources($processHandle,1)}
        $rawSamples.Add($sample);$previousCpu=$cpu;$previousSeconds=$elapsed
        $intervalStartedUtc=$sampledUtc
        $sample | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $OutputDirectory 'resource-samples.jsonl') -Encoding UTF8
        if([int]$elapsed%15 -eq 0){$sample|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'resource-progress.json') -Encoding UTF8}
    }
    $marker=Get-ChildItem (Join-Path $env:TEMP 'DesktopLifeSmoke') -Filter diagnostic-process.json -Recurse | Where-Object LastWriteTimeUtc -ge $startedUtc.UtcDateTime | Where-Object {(Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json).ProcessId -eq $process.Id}|Select-Object -First 1
    if(!$marker){throw 'The isolated candidate process marker is missing.'}
    Copy-Item -LiteralPath $marker.DirectoryName -Destination (Join-Path $OutputDirectory 'isolated-run') -Recurse -Force
    if($process.ExitCode -ne 0){throw ('Native house stress failed, exit '+$process.ExitCode+'. See isolated-run/logs/app.log.')}
    $stressPath=Join-Path $OutputDirectory 'isolated-run/stress-report.json'
    if(!(Test-Path -LiteralPath $stressPath)){throw 'Native stress report is missing.'}
    $stress=Get-Content -LiteralPath $stressPath -Raw|ConvertFrom-Json
    if($stress.Floors -ne $Floors -or $stress.Presence -ne 'All' -or $stress.RequestedSeconds -ne $Seconds){throw 'The stress workload does not match the requested three-character house.'}
    foreach($counter in @('Exceptions','StuckSequences','InvalidFurnitureInteractions','HouseRouteFailures','ApproachTimeouts','NavigationRecoveryTimeouts','UnexpectedNavigationRecoveryAttempts')){
        if(($stress.$counter -isnot [int] -and $stress.$counter -isnot [long]) -or $stress.$counter -ne 0){throw "Native stress has a missing or nonzero reliability counter: $counter"}
    }
    if(!$stress.DurableReloadVerification.Succeeded){throw 'Final native stress state did not pass durable reload verification.'}
    $steadyUtc=[DateTimeOffset]::Parse($stress.SteadyStartedUtc)
    $workloadEndedUtc=[DateTimeOffset]::Parse($stress.StartedUtc).AddSeconds($stress.Seconds)
    $steadyRows=@($rawSamples|Where-Object {[DateTimeOffset]::Parse($_.IntervalStartedUtc) -ge $steadyUtc -and [DateTimeOffset]::Parse($_.Utc) -le $workloadEndedUtc})
    [ordered]@{Succeeded=$true;CapturedUtc=[DateTimeOffset]::UtcNow.ToString('O');ProcessId=$process.Id;Seconds=$Seconds;Floors=$Floors;Presence='All';ActualSeconds=$stress.Seconds;StairTrips=$stress.StairTrips;HouseRouteFailures=$stress.HouseRouteFailures;ApproachTimeouts=$stress.ApproachTimeouts;NavigationFailures=$stress.NavigationFailures;NavigationRecoveriesCompleted=$stress.NavigationRecoveriesCompleted;NavigationRecoveryTimeouts=$stress.NavigationRecoveryTimeouts;AllSamples=(Measure-ResourceRows @($rawSamples.ToArray()));SteadySamples=(Measure-ResourceRows $steadyRows);StressCpuPercent=$stress.MachineCpuPercent;InstalledApplicationTouched=$false;Scope='Native apphost stress in TEMP/GUID diagnostic profile. Resource observations include the requested workload; no physical phone disconnect or Windows layout mutation performed.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'resource-summary.json') -Encoding UTF8
    $runSucceeded=$true;Write-DesktopLifeNativeResult $NativeResult $true $OutputDirectory
}catch{
    if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}
    if($process -and !$marker){
        $marker=Get-ChildItem (Join-Path $env:TEMP 'DesktopLifeSmoke') -Filter diagnostic-process.json -Recurse -ErrorAction SilentlyContinue | Where-Object {(Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json).ProcessId -eq $process.Id}|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1
        if($marker){Copy-Item -LiteralPath $marker.DirectoryName -Destination (Join-Path $OutputDirectory 'isolated-run') -Recurse -Force}
    }
    [ordered]@{Succeeded=$false;Error=$_.Exception.Message;ProcessId=if($process){$process.Id}else{$null};Samples=$rawSamples.Count;Floors=$Floors;Seconds=$Seconds;InstalledApplicationTouched=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'resource-summary.json') -Encoding UTF8
    Write-DesktopLifeNativeResult $NativeResult $false $_.Exception.Message;throw
}finally{if($NativeResult){Stop-Transcript|Out-Null}}
