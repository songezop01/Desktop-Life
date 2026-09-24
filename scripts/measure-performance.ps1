$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet=Join-Path $root 'tools/dotnet/dotnet.exe'
$process=Start-Process -FilePath $dotnet -ArgumentList @('"src/DesktopLife.App/bin/Release/net10.0-windows/DesktopLife.App.dll"','--performance-test') -WorkingDirectory $root -WindowStyle Hidden -PassThru
$handle=$process.Handle
$watch=[Diagnostics.Stopwatch]::StartNew()
$samples=[Collections.Generic.List[object]]::new()
$lastCpu=$null
$lastTime=0
while(!$process.HasExited -and $watch.Elapsed.TotalSeconds -lt 55) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    if($process.HasExited){break}
    $now=$watch.Elapsed.TotalSeconds
    $cpu=$process.TotalProcessorTime.TotalSeconds
    if($now -gt 5 -and $null -ne $lastCpu) {
        $oneCore=100*($cpu-$lastCpu)/($now-$lastTime)
        $samples.Add([pscustomobject]@{Seconds=$now;OneCoreCpuPercent=$oneCore;MachineCpuPercent=$oneCore/[Environment]::ProcessorCount;WorkingSetMB=$process.WorkingSet64/1MB;PrivateMB=$process.PrivateMemorySize64/1MB})
    }
    $lastCpu=$cpu;$lastTime=$now
}
if(!$process.HasExited){$process.CloseMainWindow() | Out-Null;throw 'Performance test exceeded duration'}
if($samples.Count -eq 0 -or $process.ExitCode -ne 0){throw 'Performance run failed'}
$result=[pscustomobject]@{
    Timestamp=[DateTimeOffset]::Now.ToString('O');Samples=$samples.Count;LogicalProcessors=[Environment]::ProcessorCount;
    MeanMachineCpuPercent=($samples|Measure-Object MachineCpuPercent -Average).Average;
    MeanOneCoreCpuPercent=($samples|Measure-Object OneCoreCpuPercent -Average).Average;
    PeakWorkingSetMB=($samples|Measure-Object WorkingSetMB -Maximum).Maximum;
    FirstPrivateMB=$samples[0].PrivateMB;LastPrivateMB=$samples[$samples.Count-1].PrivateMB;
    Note='45-second local companion idle action run; control panel hidden after 3 sec; no connectome or hardware feeding. Short test, not a long-duration leak test.'
}
New-Item -ItemType Directory -Force (Join-Path $root 'artifacts') | Out-Null
$result | ConvertTo-Json | Set-Content (Join-Path $root 'artifacts/performance.json') -Encoding utf8
$result | ConvertTo-Json
