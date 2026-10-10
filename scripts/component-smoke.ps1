param(
    [Parameter(Mandatory=$true)][ValidateSet('FoodInteraction','DirectInteraction','RuntimeDiagnostics','HouseMotion')][string]$Component,
    [string]$OutputDirectory,
    [string]$BinarySource,
    [ValidateRange(15,300)][int]$TimeoutSeconds=120,
    [switch]$NativeWorker,
    [string]$NativeResult
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/native-profile-context.ps1"
. "$PSScriptRoot/release-verification.ps1"
$specification=Get-DesktopLifeComponentSpecifications '0.12.0'|Where-Object Stage -CEQ $Component
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('artifacts/verification/0.12/'+$specification.Directory+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
if(![IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path $root $OutputDirectory}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(!$BinarySource){$BinarySource=Join-Path $root 'src/DesktopLife.App/bin/Release/net10.0-windows'}
$BinarySource=[IO.Path]::GetFullPath($BinarySource)
if(!$NativeWorker){Invoke-DesktopLifeNative $PSCommandPath @('-Component',$Component,'-OutputDirectory',$OutputDirectory,'-BinarySource',$BinarySource,'-TimeoutSeconds',[string]$TimeoutSeconds);return}
$process=$null;$profile=$null;$finished=$false;$started=[DateTime]::UtcNow;$binary=$null;$outputCreated=$false
function Find-ComponentProfile {
    if(!$process){return $null}
    $parent=Join-Path $env:TEMP 'DesktopLifeSmoke'
    if(!(Test-Path -LiteralPath $parent)){return $null}
    foreach($candidate in @(Get-ChildItem -LiteralPath $parent -Directory)){
        $parsed=[guid]::Empty
        if(![guid]::TryParseExact($candidate.Name,'N',[ref]$parsed)){continue}
        $path=Join-Path $candidate.FullName 'diagnostic-process.json'
        if(!(Test-Path -LiteralPath $path)){continue}
        try{
            $marker=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
            if($marker.ProcessId -eq $process.Id -and [DateTimeOffset]::Parse($marker.StartedUtc).UtcDateTime -ge $started.AddSeconds(-1) -and $marker.Executable -ieq $binary.Executable -and @($marker.Arguments) -ccontains $specification.Argument){return $candidate.FullName}
        }catch{continue}
    }
    return $null
}
try{
    if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new component output directory; old evidence cannot be reused.'}
    New-Item -ItemType Directory -Path $OutputDirectory|Out-Null
    $outputCreated=$true
    $binaryDirectory=Join-Path $OutputDirectory ('binary-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $binaryDirectory|Out-Null
    if(((Get-Item -LiteralPath $BinarySource -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Native component binary source cannot be a redirected path.'}
    $files=@(Get-ChildItem -LiteralPath $BinarySource -Recurse -File)
    foreach($entry in @(Get-ChildItem -LiteralPath $BinarySource -Recurse -Force)){
        if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Native component binary source cannot contain redirected paths.'}
    }
    $inventory=@(foreach($file in $files){
        $relative=$file.FullName.Substring($BinarySource.TrimEnd([char[]]@('\','/')).Length+1)
        $copied=Join-Path $binaryDirectory $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $copied -Parent)|Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $copied
        $hash=(Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash
        if((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $hash){throw 'Application binaries changed during native component preparation.'}
        [IO.File]::SetAttributes($copied,([IO.File]::GetAttributes($copied) -bor [IO.FileAttributes]::ReadOnly))
        [ordered]@{Name=$relative;Sha256=$hash}
    })
    $executable=Join-Path $binaryDirectory 'DesktopLife.App.exe'
    if(!(Test-Path -LiteralPath $executable)){$executable=Join-Path $binaryDirectory 'DesktopLife.exe'}
    if(!(Test-Path -LiteralPath $executable)){throw 'A native apphost executable is required for PerMonitorV2 component checks.'}
    $binary=[ordered]@{Component=$Component;Argument=$specification.Argument;Executable=$executable;BinarySource=$BinarySource;Files=$inventory}
    $binary|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputDirectory 'component-binary.json') -Encoding UTF8
    $localRuntime=Join-Path $root 'tools/dotnet'
    if(Test-Path -LiteralPath (Join-Path $localRuntime 'dotnet.exe')){$env:DOTNET_ROOT=$localRuntime}
    $started=[DateTime]::UtcNow
    $process=Start-Process -FilePath $executable -ArgumentList $specification.Argument -WorkingDirectory $binaryDirectory -WindowStyle Hidden -PassThru
    $handle=$process.Handle
    $finished=$process.WaitForExit($TimeoutSeconds*1000)
    if(!$finished){$process.Kill();$process.WaitForExit()}
    $profile=Find-ComponentProfile
    if($profile){Copy-Item -LiteralPath $profile -Destination (Join-Path $OutputDirectory 'isolated-run') -Recurse}
    if(!$finished){throw "$Component native component timed out; partial evidence retained in $OutputDirectory"}
    if($process.ExitCode -ne 0){throw "$Component native component failed with exit $($process.ExitCode); inspect isolated-run/logs/app.log."}
    if(!$profile){throw "$Component matching TEMP/GUID diagnostic profile is missing."}
    for($index=0;$index -lt @($specification.Reports).Count;$index++){
        $path=Join-Path $OutputDirectory ('isolated-run/'+$specification.Reports[$index])
        Assert-DesktopLifeComponentReport (Read-DesktopLifeEvidence $path) $specification ($index -gt 0)
    }
    $reportHashes=@(foreach($name in $specification.Reports){[ordered]@{Name=$name;Sha256=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory ('isolated-run/'+$name)) -Algorithm SHA256).Hash}})
    [ordered]@{Schema=1;Succeeded=$true;Component=$Component;Argument=$specification.Argument;StartedUtc=$started;CompletedUtc=[DateTime]::UtcNow;ProcessId=$process.Id;ExitCode=$process.ExitCode;TimedOut=$false;NativeAppHost=$true;IsolatedProfileVerified=$true;IsolatedProfile=$profile;ReportHashes=$reportHashes;Scope='Native component checks only; elapsed stress, Standard and Full are separate release stages.'}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputDirectory 'component-summary.json') -Encoding UTF8
    Write-DesktopLifeNativeResult $NativeResult $true $OutputDirectory
}catch{
    $componentFailure=$_;$retentionError=$null
    try{if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}}catch{$retentionError=$_.Exception.Message}
    try{if($outputCreated -and !$profile -and $process -and $binary){$profile=Find-ComponentProfile;if($profile -and !(Test-Path -LiteralPath (Join-Path $OutputDirectory 'isolated-run'))){Copy-Item -LiteralPath $profile -Destination (Join-Path $OutputDirectory 'isolated-run') -Recurse}}}catch{$retentionError=$_.Exception.Message}
    if($outputCreated){
        [ordered]@{Schema=1;Succeeded=$false;Component=$Component;Argument=$specification.Argument;StartedUtc=$started;CompletedUtc=[DateTime]::UtcNow;ProcessId=$(if($process){$process.Id}else{$null});ExitCode=$(if($process -and $process.HasExited){$process.ExitCode}else{$null});TimedOut=($process -and !$finished);IsolatedProfile=$profile;Error=$componentFailure.Exception.Message;EvidenceRetentionError=$retentionError}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputDirectory 'component-summary.json') -Encoding UTF8
    }
    if($NativeResult -or $outputCreated){
        $log=if($NativeResult){$NativeResult+'.log'}else{Join-Path $OutputDirectory 'component-error.log'}
        $componentFailure|Out-String|Set-Content -LiteralPath $log -Encoding UTF8
    }
    Write-DesktopLifeNativeResult $NativeResult $false $componentFailure.Exception.Message
    throw $componentFailure
}finally{if($process){$process.Dispose()}}
