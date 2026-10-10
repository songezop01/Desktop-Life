param([Parameter(Mandatory=$true)][string]$VerificationDirectory,[switch]$NativeWorker,[string]$NativeResult)
. "$PSScriptRoot/native-profile-context.ps1"
. "$PSScriptRoot/deployment-transaction.ps1"
. "$PSScriptRoot/release-verification.ps1"
. "$PSScriptRoot/upgrade012-contract.ps1"
$VerificationDirectory=[IO.Path]::GetFullPath($VerificationDirectory)
if(!$NativeWorker){Invoke-DesktopLifeNative -ScriptPath $PSCommandPath -Arguments @('-VerificationDirectory',$VerificationDirectory);return}
try{
    if($NativeResult){Start-Transcript -Path ($NativeResult+'.log') -Force|Out-Null}
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=Get-DesktopLifeReleaseVersion $root
$verificationMode='Full'
$summary=Get-Content -LiteralPath (Join-Path $VerificationDirectory 'verification-summary.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-DesktopLife012InitialRelease $version $summary
Assert-DesktopLifeVerificationEvidence $summary $root $VerificationDirectory -Mode Full
function Verify-SourceInventory {
    Assert-DesktopLifeSourceInventory $summary.SourceHashes $root
    Assert-DesktopLifeBuildInventory $summary.BuildHashes $root
}
Verify-SourceInventory
$run=Join-Path $root ('artifacts/installation/'+$version+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
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
    try{
        $handle=$process.Handle
        if(!$process.WaitForExit($Timeout)){
            if($Argument -in @('--smoke-test','--house-test')){$process.Kill();$process.WaitForExit()}
            throw "Timed out: $Argument. Upgrade stopped."
        }
        if($process.ExitCode -ne 0){throw "$Argument failed: $($process.ExitCode). Upgrade stopped."}
    }finally{$process.Dispose()}
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
    $releaseDirectory=Assert-DesktopLife012PublishedRelease $release $root
    $packageInventory=@(Get-DesktopLifeTreeInventory $releaseDirectory)
    $installedInventory=@(Get-DesktopLife012InstalledInventory $packageInventory)
    $packageChecks=[Collections.Generic.List[object]]::new()
    $upgradeState.Phase='SMOKE'
    Invoke-Isolated $release.Executable '--house-test' 120000
    Invoke-Isolated $release.Executable '--smoke-test' 90000
    foreach($component in @(Get-DesktopLifeComponentSpecifications $version)){
        $upgradeState.Phase='PACKAGED_'+$component.Stage.ToUpperInvariant()
        & "$PSScriptRoot/component-smoke.ps1" -Component $component.Stage -BinarySource $releaseDirectory -OutputDirectory (Join-Path $run ('raw/'+$component.Directory))
        # The standalone executable embeds its assemblies. The sealed developer
        # build keeps the six-file gate; packaged checks bind to the exact bundle
        # plus every copied package file rather than pretending it has those DLLs.
        Assert-DesktopLifeComponentEvidence @(@{Path='DesktopLife.exe';Sha256=$release.Sha256}) $run $component
        $componentDirectory=Join-Path $run ('raw/'+$component.Directory)
        $componentBinary=Read-DesktopLifeEvidence (Join-Path $componentDirectory 'component-binary.json')
        Assert-DesktopLife012PackageSnapshot $release $packageInventory $componentBinary
        $componentSummary=Read-DesktopLifeEvidence (Join-Path $componentDirectory 'component-summary.json')
        $packageChecks.Add([ordered]@{Stage=$component.Stage;Argument=$component.Argument;EvidenceDirectory=$componentDirectory;ProcessId=$componentSummary.ProcessId;ExitCode=$componentSummary.ExitCode;ReportHashes=$componentSummary.ReportHashes;ExecutableSha256=$release.Sha256})
    }
    Assert-DesktopLifeTreeInventory $releaseDirectory $packageInventory
    Verify-SourceInventory
    # Capture the matching installed build before stopping it. A failed backup
    # must still have enough verified information to resume the previous app.
    $previous=if(Test-Path -LiteralPath $previousFile){Get-Content -LiteralPath $previousFile -Raw -Encoding UTF8 | ConvertFrom-Json}else{$null}
    if($previous){
        $oldExe=Assert-DesktopLifeChildPath $previous.Executable $installRoot
        if([IO.Path]::GetFullPath([string]$previous.DataDirectory) -ine [IO.Path]::GetFullPath($data)){throw 'The installed build belongs to a different profile; upgrade stopped.'}
        $previousHash=(Get-FileHash -LiteralPath $oldExe -Algorithm SHA256).Hash
        $upgradeState.PreviousExecutable=$oldExe
        $upgradeState.PreviousExecutableSha256=$previousHash
        $upgradeState.PreviousManifest=$previousFile
        $upgradeState.PreviousManifestSha256=(Get-FileHash -LiteralPath $previousFile -Algorithm SHA256).Hash
        $upgradeState.PreviousWasRunning=@(Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath -eq $oldExe}).Count -gt 0
    }
    $upgradeState.Phase='SHUTDOWN_PENDING'
    Assert-DesktopLife012DataRoots $data
    Invoke-Isolated $release.Executable '--shutdown' 30000
    $upgradeState.ShutdownAcknowledged=$true
    $upgradeState.Phase='SHUTDOWN_ACKNOWLEDGED'
    New-Item -ItemType Directory -Force $data | Out-Null
    # Hold the same lock as the application throughout the data/shortcut transaction.
    # A relaunch after safe shutdown must not write into a partially copied/restored profile.
    $dataLock=[IO.File]::Open((Join-Path $data 'instance.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $upgradeState.Phase='BACKING_UP'
    Assert-DesktopLife012DataRoots $data
    $backup=Join-Path $data ('upgrade-backups/before-'+$version+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
    [void](Assert-DesktopLifeChildPath $backup (Join-Path $data 'upgrade-backups'))
    if(Test-Path -LiteralPath $backup){throw 'Upgrade backup directory already exists.'}
    New-Item -ItemType Directory $backup | Out-Null
    $activeInventory=Get-DesktopLife012ActiveInventory $data
    foreach($entry in @(Get-DesktopLifeActiveEntries $data)){Copy-Item -LiteralPath $entry.FullName -Destination $backup -Recurse -Force}
    $hashes=@(Assert-DesktopLife012Backup $backup $activeInventory)
    $afterBackup=Get-DesktopLife012ActiveInventory $data
    if(@($activeInventory.Files).Count -gt 0 -or @($afterBackup.Files).Count -gt 0){Assert-DesktopLifeInventory @($activeInventory.Files) @($afterBackup.Files) 'Active profile during backup'}
    if(($activeInventory.Directories -join "`n") -cne ($afterBackup.Directories -join "`n")){throw 'Active profile directory inventory changed during backup.'}
    Assert-DesktopLifeTreeInventory $releaseDirectory $packageInventory
    Verify-SourceInventory
    $expectedInstalledExe=Join-Path (Join-Path $installRoot $release.Build) 'DesktopLife.exe'
    $record=[ordered]@{Version=$version;VerificationMode=$verificationMode;DataDirectory=$data;Backup=$backup;BackupHashes=$hashes;BackupDirectories=$activeInventory.Directories;PackageInventory=$packageInventory;InstalledInventory=$installedInventory;PackagedComponents=$packageChecks.ToArray();PackageVerificationDirectory=$run;PreviousInstallation=$previous;PreviousExecutableSha256=$previousHash;NewExecutable=$expectedInstalledExe;NewSha256=$release.Sha256;VerificationDirectory=$VerificationDirectory;CreatedUtc=[DateTime]::UtcNow;State='BACKED_UP'}
    $recordPath=Join-Path $run 'upgrade-record.json'
    $record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    $recovery=Join-Path $root ('artifacts/recovery/'+$version+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
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
            Assert-DesktopLifeTreeInventory $releaseDirectory $packageInventory
            & "$PSScriptRoot/install.ps1" -ReleaseDirectory $releaseDirectory
            $installed=Get-Content -LiteralPath $previousFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $installedExe=Assert-DesktopLifeChildPath $installed.Executable $installRoot
            if($installed.Build -cne $release.Build -or $installedExe -ine $expectedInstalledExe -or (Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash -ne $release.Sha256){throw 'Installed executable or build identifier mismatch.'}
            Assert-DesktopLifeTreeInventory (Split-Path $installedExe -Parent) $installedInventory
            $afterInstall=Get-DesktopLife012ActiveInventory $data
            if(@($activeInventory.Files).Count -gt 0 -or @($afterInstall.Files).Count -gt 0){Assert-DesktopLifeInventory @($activeInventory.Files) @($afterInstall.Files) 'Profile preserved during installation'}
            if(($activeInventory.Directories -join "`n") -cne ($afterInstall.Directories -join "`n")){throw 'Installation changed profile directories.'}
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
