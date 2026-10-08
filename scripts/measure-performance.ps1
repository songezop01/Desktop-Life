param([ValidateRange(10,600)][int]$Seconds=45,[string]$OutputPath='artifacts/performance.json',[string]$BinarySource)
$ErrorActionPreference='Stop'
function Get-NearestRank([double[]]$Values,[double]$Quantile) {
    if(!$Values.Count){return $null}
    $ordered=@($Values | Sort-Object)
    return $ordered[[Math]::Max(0,[int][Math]::Ceiling($ordered.Count*$Quantile)-1)]
}
function Get-ResourceSummary($Rows) {
    if(!$Rows.Count){return $null}
    $summary=[ordered]@{Samples=$Rows.Count;FirstSeconds=$Rows[0].Seconds;LastSeconds=$Rows[$Rows.Count-1].Seconds}
    foreach($metric in @('OneCoreCpuPercent','MachineCpuPercent','WorkingSetMB','PrivateMB','HandleCount')) {
        $values=@($Rows | ForEach-Object {$_.$metric})
        $summary[$metric]=[ordered]@{First=$values[0];Last=$values[$values.Count-1];Mean=($values|Measure-Object -Average).Average;P50=(Get-NearestRank $values .5);P95=(Get-NearestRank $values .95);Max=($values|Measure-Object -Maximum).Maximum}
    }
    return $summary
}
$root=Split-Path $PSScriptRoot -Parent
$dotnet=& "$PSScriptRoot/dotnet-path.ps1"
$destination=if([IO.Path]::IsPathRooted($OutputPath)){$OutputPath}else{Join-Path $root $OutputPath}
$runDirectory=Join-Path $root ('artifacts/performance-runs/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$binaryDirectory=Join-Path $runDirectory 'binary'
New-Item -ItemType Directory -Force $binaryDirectory | Out-Null
if(!$BinarySource){$BinarySource=Join-Path $root 'src/DesktopLife.App/bin/Release/net10.0-windows'}
Copy-Item -Path (Join-Path $BinarySource '*') -Destination $binaryDirectory -Recurse
$startedUtc=[DateTime]::UtcNow
$process=Start-Process -FilePath $dotnet -ArgumentList @(('"'+(Join-Path $binaryDirectory 'DesktopLife.App.dll')+'"'),'--performance-test',"--performance-seconds=$Seconds") -WorkingDirectory $root -WindowStyle Hidden -PassThru
$handle=$process.Handle
$watch=[Diagnostics.Stopwatch]::StartNew()
$samples=[Collections.Generic.List[object]]::new()
$rawSamples=[Collections.Generic.List[object]]::new()
$lastCpu=$null
$lastTime=0
while(!$process.HasExited -and $watch.Elapsed.TotalSeconds -lt $Seconds+30) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    if($process.HasExited){break}
    $now=$watch.Elapsed.TotalSeconds
    $cpu=$process.TotalProcessorTime.TotalSeconds
    if($null -ne $lastCpu) {
        $oneCore=100*($cpu-$lastCpu)/($now-$lastTime)
        $sample=[pscustomobject]@{Seconds=$now;Utc=[DateTimeOffset]::UtcNow.ToString('O');OneCoreCpuPercent=$oneCore;MachineCpuPercent=$oneCore/[Environment]::ProcessorCount;WorkingSetMB=$process.WorkingSet64/1MB;PrivateMB=$process.PrivateMemorySize64/1MB;HandleCount=$process.HandleCount}
        $rawSamples.Add($sample)
        if($now -gt 5){$samples.Add($sample)}
    }
    $lastCpu=$cpu;$lastTime=$now
}
if(!$process.HasExited){$process.Kill();$process.WaitForExit();throw "Owned isolated performance process exceeded duration and was stopped; evidence: $runDirectory"}
if($samples.Count -eq 0 -or $process.ExitCode -ne 0){throw 'Performance run failed'}
$marker=Get-ChildItem "$env:TEMP/DesktopLifeSmoke" -Filter diagnostic-process.json -Recurse | Where-Object LastWriteTimeUtc -ge $startedUtc | Where-Object {(Get-Content $_.FullName -Raw|ConvertFrom-Json).ProcessId -eq $process.Id} | Select-Object -First 1
if(!$marker){throw 'Matching performance marker missing'}
$runtimeReport=Join-Path $marker.DirectoryName 'performance-report.json'
$runtimeMetrics=$null
if(Test-Path -LiteralPath $runtimeReport){$runtimeMetrics=Get-Content -LiteralPath $runtimeReport -Raw | ConvertFrom-Json;Copy-Item -LiteralPath $runtimeReport -Destination $runDirectory}
Copy-Item -LiteralPath (Join-Path $marker.DirectoryName 'logs/app.log') -Destination $runDirectory
$steadySamples=@()
$steadyReason='Not available: this binary does not report the UTC beginning of steady observation.'
if($runtimeMetrics.SteadyStartedUtc) {
    $steadyUtc=[DateTimeOffset]::Parse($runtimeMetrics.SteadyStartedUtc)
    $steadySamples=@($rawSamples | Where-Object { [DateTimeOffset]::Parse($_.Utc) -ge $steadyUtc })
    $steadyReason='Aligned to the runtime UTC beginning of steady observation; intervals crossing the boundary are excluded.'
    # CPU values describe the interval ending at the sample, so discard the first crossing interval.
    if($steadySamples.Count -gt 1){$steadySamples=@($steadySamples | Select-Object -Skip 1)}else{$steadySamples=@()}
}
$result=[pscustomobject]@{
    Timestamp=[DateTimeOffset]::Now.ToString('O');ProcessId=$process.Id;RequestedSeconds=$Seconds;Samples=$samples.Count;LogicalProcessors=[Environment]::ProcessorCount;
    MeanMachineCpuPercent=($samples|Measure-Object MachineCpuPercent -Average).Average;
    MeanOneCoreCpuPercent=($samples|Measure-Object OneCoreCpuPercent -Average).Average;
    PeakWorkingSetMB=($samples|Measure-Object WorkingSetMB -Maximum).Maximum;
    FirstPrivateMB=$samples[0].PrivateMB;LastPrivateMB=$samples[$samples.Count-1].PrivateMB;
    RuntimeMetrics=$runtimeMetrics;RunDirectory=$runDirectory;BinarySource=$BinarySource;
    ProcessWindow=Get-ResourceSummary $samples;SteadyWindow=Get-ResourceSummary $steadySamples;SteadyWindowReason=$steadyReason;
    RawSamples=$rawSamples;ResourceTrendAssessment='Not assessed: short performance comparison; startup and GC residency changes are retained in raw samples.';
    AppAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $binaryDirectory 'DesktopLife.App.dll') -Algorithm SHA256).Hash;
    Note="$Seconds-second local companion idle action run; control panel hidden after 3 sec; no connectome or hardware feeding. Short test, not a long-duration leak test."
}
New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $destination -Encoding utf8
$result | ConvertTo-Json -Depth 8
