param([ValidateRange(60,28800)][int]$Seconds=600,[string]$Label='home',[string]$OutputDirectory='artifacts/verification/0.10',[ValidateSet('Both','All')][string]$Presence='All',[string]$BinarySource,[ValidateRange(1,3)][int]$Floors=1)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet=& "$PSScriptRoot/dotnet-path.ps1"
$outputRoot=if([IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory}else{Join-Path $root $OutputDirectory}
$destination=Join-Path $outputRoot $Label
New-Item -ItemType Directory -Force $destination | Out-Null
# Snapshot the binaries so a long isolated run does not lock the development build.
$binaryDirectory=Join-Path $destination ('binary-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $binaryDirectory | Out-Null
if(!$BinarySource){$BinarySource=Join-Path $root 'src/DesktopLife.App/bin/Release/net10.0-windows'}
Copy-Item -Path (Join-Path $BinarySource '*') -Destination $binaryDirectory -Recurse
[ordered]@{RequestedSeconds=$Seconds;Presence=$Presence;Floors=$Floors;AppAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $binaryDirectory 'DesktopLife.App.dll') -Algorithm SHA256).Hash;CoreAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $binaryDirectory 'DesktopLife.Core.dll') -Algorithm SHA256).Hash;BinaryDirectory=$binaryDirectory;BinarySource=$BinarySource} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'stress-binary.json') -Encoding utf8
$env:DOTNET_ROOT=Join-Path $root 'tools/dotnet'
$executable=Join-Path $binaryDirectory 'DesktopLife.App.exe'
if(!(Test-Path -LiteralPath $executable)){$executable=Join-Path $binaryDirectory 'DesktopLife.exe'}
if(!(Test-Path -LiteralPath $executable)){throw 'Native apphost executable required for PerMonitorV2 diagnostics.'}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -ArgumentList @('--stress-test',"--stress-seconds=$Seconds","--stress-presence=$Presence","--stress-floors=$Floors") -WorkingDirectory $root -WindowStyle Hidden -PassThru
$handle=$process.Handle
Write-Output "Isolated stress process: $($process.Id), requested seconds: $Seconds"
$timedOut=$false
while(!$process.WaitForExit(30000)){
    if(([DateTime]::UtcNow-$started).TotalSeconds -gt $Seconds*1.5+120){$process.Kill();$process.WaitForExit();$timedOut=$true;break}
    Write-Output "Stress running: $([int]([DateTime]::UtcNow-$started).TotalSeconds) seconds."
}
$marker=Get-ChildItem "$env:TEMP/DesktopLifeSmoke" -Filter stress-process.json -Recurse | Where-Object LastWriteTimeUtc -ge $started | Where-Object {(Get-Content $_.FullName -Raw|ConvertFrom-Json).ProcessId -eq $process.Id} | Select-Object -First 1
if(!$marker){throw 'Matching stress marker missing'}
foreach($name in @('stress-process.json','stress-progress.json','stress-samples.jsonl','stress-report.json','stress-expected-organism.json','organism.json')){
    $source=Join-Path $marker.DirectoryName $name
    if(Test-Path $source){Copy-Item -LiteralPath $source -Destination $destination}
}
Copy-Item -LiteralPath (Join-Path $marker.DirectoryName 'logs/app.log') -Destination $destination
if($timedOut){throw "Owned isolated stress process exceeded timeout and was stopped; evidence: $destination"}
if($process.ExitCode -ne 0){throw "Stress failed: $($process.ExitCode); evidence: $destination"}
if(!(Test-Path (Join-Path $destination 'stress-report.json'))){throw 'Stress report missing'}
$report=Get-Content -LiteralPath (Join-Path $destination 'stress-report.json') -Raw | ConvertFrom-Json
if($report.Presence -ne $Presence){throw "Stress presence mismatch: requested $Presence, observed $($report.Presence)."}
$savedPath=Join-Path $destination 'organism.json'
if(Test-Path -LiteralPath $savedPath){
    $saved=Get-Content -LiteralPath $savedPath -Raw | ConvertFrom-Json
    [ordered]@{ProcessExited=$process.HasExited;ExitCode=$process.ExitCode;SchemaVersion=$saved.SchemaVersion;PrimaryKind=$saved.Settings.PetAppearance;OtherKind=$saved.OtherCharacter.Kind;AdditionalKind=$saved.AdditionalCharacter.Kind;SavedOrganismSha256=(Get-FileHash -LiteralPath $savedPath -Algorithm SHA256).Hash;Note='Actual isolated on-disk save copied after process exit; runtime DurableReloadVerification separately checks the pre-exit captured graph. This is not a process restart test.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'exit-state.json') -Encoding utf8
}
if($report.DurableReloadVerification){
    # Reopen only the just-finished diagnostic profile; the application validates TEMP/GUID ownership again.
    $restart=Start-Process -FilePath $executable -ArgumentList @('--restart-verify-test',('--restart-root="'+$marker.DirectoryName+'"')) -WorkingDirectory $root -WindowStyle Hidden -PassThru
    $restartHandle=$restart.Handle
    $completed=$restart.WaitForExit(30000)
    if(!$completed){$restart.Kill();$restart.WaitForExit();throw "Isolated restart verification exceeded 30 seconds: $destination"}
    $restartReport=Join-Path $marker.DirectoryName 'restart-report.json'
    if(Test-Path -LiteralPath $restartReport){Copy-Item -LiteralPath $restartReport -Destination $destination}
    Copy-Item -LiteralPath (Join-Path $marker.DirectoryName 'logs/app.log') -Destination (Join-Path $destination 'restart-app.log')
    if($restart.ExitCode -ne 0 -or !(Test-Path -LiteralPath $restartReport)){throw "Isolated restart verification failed: exit $($restart.ExitCode); evidence $destination"}
    $restartResult=Get-Content -LiteralPath $restartReport -Raw | ConvertFrom-Json
    if($restartResult.Succeeded -ne $true){throw "Isolated restart did not confirm state preservation: $destination"}
}else{
    [ordered]@{Succeeded=$null;Reason='Not available: immutable reference binary predates isolated restart verification.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'restart-report.json') -Encoding utf8
}
Write-Output "Stress complete: $destination"

