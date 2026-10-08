param([Parameter(Mandatory=$true)][string]$VerificationDirectory,[switch]$NativeWorker,[string]$NativeResult)
. "$PSScriptRoot/native-profile-context.ps1"
. "$PSScriptRoot/deployment-transaction.ps1"
if(!$NativeWorker){Invoke-DesktopLifeNative -ScriptPath $PSCommandPath -Arguments @('-VerificationDirectory',$VerificationDirectory);return}
try{
    if($NativeResult){Start-Transcript -Path ($NativeResult+'.log') -Force|Out-Null}
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$summary=Get-Content -LiteralPath (Join-Path $VerificationDirectory 'verification-summary.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($summary.Version -ne '0.10.1' -or $summary.Conclusion -ne 'PASS' -or $summary.Mode -ne 'Full'){throw 'A passing 0.10.1 Full verification is required.'}
if(!$summary.SourceHashes -or $summary.SourceHashes.Count -eq 0){throw 'Verification source inventory is missing.'}
function Verify-SourceInventory {
    $sourceFiles=@(Get-ChildItem (Join-Path $root 'src'),(Join-Path $root 'tests'),(Join-Path $root 'scripts') -Recurse -File | Where-Object {$_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' -and $_.Extension -in @('.cs','.xaml','.csproj','.ps1','.py','.png','.ico','.json','.manifest','.props','.targets','.resx','.config')})
    $sourceFiles+=Get-ChildItem -LiteralPath $root -File | Where-Object {$_.Name -eq 'global.json' -or $_.Extension -in @('.slnx','.props','.targets','.config')}
    $currentPaths=@($sourceFiles | ForEach-Object {$_.FullName.Substring($root.Length+1)} | Sort-Object)
    $verifiedPaths=@($summary.SourceHashes | ForEach-Object Path | Sort-Object)
    if(($currentPaths -join "`n") -ne ($verifiedPaths -join "`n")){throw 'Source inventory changed after verification.'}
    foreach($file in $summary.SourceHashes){
        if((Get-FileHash -LiteralPath (Join-Path $root $file.Path) -Algorithm SHA256).Hash -ne $file.Sha256){throw "Source changed after verification: $($file.Path)"}
    }
}
Verify-SourceInventory
$run=Join-Path $root ('artifacts/installation/0.10.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $run | Out-Null
$installRoot=Join-Path $env:LOCALAPPDATA 'Programs/DesktopLife'
New-Item -ItemType Directory -Force $installRoot | Out-Null
$lock=[IO.File]::Open((Join-Path $installRoot 'upgrade.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$dataLock=$null
$data=Join-Path $env:LOCALAPPDATA 'DesktopLife'
$upgradeState=New-DesktopLifeUpgradeState $data $installRoot
$previousFile=Join-Path $installRoot 'installation.json'
$previous=$null;$previousHash=$null
function Invoke-Isolated([string]$Executable,[string]$Argument,[int]$Timeout){
    $process=Start-Process -FilePath $Executable -ArgumentList $Argument -WindowStyle Hidden -PassThru
    $handle=$process.Handle
    if(!$process.WaitForExit($Timeout)){
        if($Argument -eq '--smoke-test'){$process.Kill()}
        throw "Timed out: $Argument. Upgrade stopped."
    }
    if($process.ExitCode -ne 0){throw "$Argument failed: $($process.ExitCode). Upgrade stopped."}
}
function Start-VerifiedApplication([string]$Executable,[string]$Sha256){
    if((Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash -ne $Sha256){throw 'Launch executable checksum mismatch.'}
    $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]1}
    $result=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=('"'+$Executable+'"');CurrentDirectory=(Split-Path $Executable -Parent);ProcessStartupInformation=$startup}
    if($result.ReturnValue -ne 0){throw "Independent launch failed: $($result.ReturnValue). The desktop shortcut remains available."}
    Write-Output "Independent application process: $($result.ProcessId)"
}
try{
    $upgradeState.Phase='PUBLISHING'
    & "$PSScriptRoot/publish.ps1" -SkipArchive
    Verify-SourceInventory
    $release=Get-Content -LiteralPath (Join-Path $root 'artifacts/standalone/latest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if($release.Version -ne '0.10.1'){throw 'Unexpected release version.'}
    $upgradeState.Phase='SMOKE'
    Invoke-Isolated $release.Executable '--smoke-test' 60000
    Verify-SourceInventory
    # Capture the matching installed build before stopping it. A failed backup
    # must still have enough verified information to resume the previous app.
    $previous=if(Test-Path -LiteralPath $previousFile){Get-Content -LiteralPath $previousFile -Raw -Encoding UTF8 | ConvertFrom-Json}else{$null}
    if($previous){
        $oldExe=Assert-DesktopLifeChildPath $previous.Executable $installRoot
        $previousHash=(Get-FileHash -LiteralPath $oldExe -Algorithm SHA256).Hash
        $upgradeState.PreviousExecutable=$oldExe
        $upgradeState.PreviousExecutableSha256=$previousHash
        $upgradeState.PreviousManifest=$previousFile
        $upgradeState.PreviousManifestSha256=(Get-FileHash -LiteralPath $previousFile -Algorithm SHA256).Hash
        $upgradeState.PreviousWasRunning=@(Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath -eq $oldExe}).Count -gt 0
    }
    $upgradeState.Phase='SHUTDOWN_PENDING'
    Invoke-Isolated $release.Executable '--shutdown' 30000
    $upgradeState.ShutdownAcknowledged=$true
    $upgradeState.Phase='SHUTDOWN_ACKNOWLEDGED'
    New-Item -ItemType Directory -Force $data | Out-Null
    # Hold the same lock as the application throughout the data/shortcut transaction.
    # A relaunch after safe shutdown must not write into a partially copied/restored profile.
    $dataLock=[IO.File]::Open((Join-Path $data 'instance.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $upgradeState.Phase='BACKING_UP'
    $backup=Join-Path $data ('upgrade-backups/before-0.10.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $backup | Out-Null
    Get-ChildItem -LiteralPath $data -Force | Where-Object {$_.Name -notin @('upgrade-backups','instance.lock')} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $backup -Recurse -Force}
    $hashes=@(Get-ChildItem -LiteralPath $backup -Recurse -File -Force | ForEach-Object {
        $relative=$_.FullName.Substring($backup.Length+1)
        $source=Join-Path $data $relative
        $sha=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $sha){throw "Backup verification failed: $relative"}
        [ordered]@{Path=$relative;Sha256=$sha}
    })
    $expectedInstalledExe=Join-Path (Join-Path $installRoot $release.Build) 'DesktopLife.exe'
    $record=[ordered]@{DataDirectory=$data;Backup=$backup;BackupHashes=$hashes;PreviousInstallation=$previous;PreviousExecutableSha256=$previousHash;NewExecutable=$expectedInstalledExe;NewSha256=$release.Sha256;VerificationDirectory=$VerificationDirectory;CreatedUtc=[DateTime]::UtcNow;State='BACKED_UP'}
    $recordPath=Join-Path $run 'upgrade-record.json'
    $record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    $recovery=Join-Path $root ('artifacts/recovery/0.10.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    $upgradeState.Phase='PREPARING_RECOVERY'
    New-Item -ItemType Directory -Force $recovery | Out-Null
    Copy-Item -LiteralPath $recordPath -Destination $recovery
    Copy-Item -LiteralPath "$PSScriptRoot/rollback-010.ps1" -Destination $recovery
    Copy-Item -LiteralPath "$PSScriptRoot/install.ps1" -Destination $recovery
    Copy-Item -LiteralPath "$PSScriptRoot/native-profile-context.ps1" -Destination $recovery
    Copy-Item -LiteralPath "$PSScriptRoot/deployment-transaction.ps1" -Destination $recovery
    @('@echo off','powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0rollback-010.ps1" -RecordPath "%~dp0upgrade-record.json"','pause') | Set-Content -LiteralPath (Join-Path $recovery 'Restore previous Desktop Life.cmd') -Encoding ASCII
    $transaction=Join-Path $data ('upgrade-backups/install-transaction-'+[guid]::NewGuid().ToString('N'))
    $record['TransactionDirectory']=$transaction
    $upgradeState.Phase='PREPARING_TRANSACTION'
    try{
        Invoke-DesktopLifeDeploymentTransaction -DataDirectory $data -SnapshotDirectory $transaction -MetadataPaths (Get-DesktopLifeInstallationMetadataPaths $installRoot) -Action {
            $upgradeState.MutationStarted=$true
            $upgradeState.Phase='INSTALLING'
            & "$PSScriptRoot/install.ps1" -ReleaseDirectory (Split-Path $release.Executable -Parent)
            $installed=Get-Content -LiteralPath $previousFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if((Get-FileHash -LiteralPath $installed.Executable -Algorithm SHA256).Hash -ne $release.Sha256){throw 'Installed executable checksum mismatch.'}
            $record.NewExecutable=$installed.Executable;$record.State='INSTALLED';$record['RecoveryDirectory']=$recovery
            $record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
            Copy-Item -LiteralPath $recordPath -Destination $recovery -Force
        }
        $upgradeState.TransactionCommitted=$true
        $upgradeState.Phase='INSTALLED'
    }catch{
        $failure=$_.Exception
        $upgradeState.CompensationStatus=if($failure.Data['DesktopLifeCompensation']){$failure.Data['DesktopLifeCompensation']}else{'NOT_REQUIRED'}
        $record.State=if($failure.Data['DesktopLifeCompensation'] -eq 'COMPENSATED'){'COMPENSATED'}else{'FAILED'}
        $record['Failure']=$failure.Message
        try{$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8;Copy-Item -LiteralPath $recordPath -Destination $recovery -Force}catch{}
        throw $failure
    }
    $installed=Get-Content -LiteralPath $previousFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $dataLock.Dispose();$dataLock=$null
    $upgradeState.DataLockReleased=$true
    $upgradeState.NewLaunchAttempted=$true
    $upgradeState.Phase='NEW_LAUNCH_ATTEMPTED'
    Start-VerifiedApplication $installed.Executable $release.Sha256
    $upgradeState.Phase='NEW_LAUNCH_DISPATCHED'
    Write-Output "Installed: $($installed.Executable)"
    Write-Output "Verified data backup: $backup"
    Write-Output "One-click recovery: $recovery"
}catch{
    $originalFailure=$_
    $upgradeState.Failure=$originalFailure.Exception.Message
    $upgradeState.FailureState=if($upgradeState.TransactionCommitted){if($upgradeState.NewLaunchAttempted){'INSTALLED_LAUNCH_FAILED'}else{'INSTALLED_POSTCHECK_FAILED'}}elseif($upgradeState.CompensationStatus -eq 'COMPENSATED'){'COMPENSATED'}elseif($upgradeState.CompensationStatus -eq 'COMPENSATION_FAILED'){'COMPENSATION_FAILED'}elseif($upgradeState.MutationStarted){'FAILED_UNVERIFIED'}elseif($upgradeState.ShutdownAcknowledged){'PREVIOUS_UNCHANGED_AFTER_SHUTDOWN'}else{'PREFLIGHT_FAILED'}
    # Release our lock before the guard probes the real native profile lock.
    if($null -ne $dataLock){try{$dataLock.Dispose();$dataLock=$null;$upgradeState.DataLockReleased=$true}catch{Write-Warning ('Could not release profile lock: '+$_.Exception.Message)}}else{$upgradeState.DataLockReleased=$true}
    [void](Resume-DesktopLifePreviousApplication $upgradeState {param($exe,$sha) Start-VerifiedApplication $exe $sha})
    if($upgradeState.PreviousRestartFailure){Write-Warning ('Original upgrade failure retained; previous application restart: '+$upgradeState.PreviousRestartFailure)}
    throw $originalFailure
}finally{
    if($null -ne $dataLock){$dataLock.Dispose()}
    try{$upgradeState | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'upgrade-state.json') -Encoding UTF8}catch{Write-Warning ('Could not write upgrade status: '+$_.Exception.Message)}
    $lock.Dispose()
}

    Write-DesktopLifeNativeResult $NativeResult $true ''
}catch{
    Write-DesktopLifeNativeResult $NativeResult $false $_.Exception.Message
    throw
}finally{if($NativeResult){Stop-Transcript|Out-Null}}
