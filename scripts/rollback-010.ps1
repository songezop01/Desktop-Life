param([Parameter(Mandatory=$true)][string]$RecordPath,[switch]$NativeWorker,[string]$NativeResult)
. "$PSScriptRoot/native-profile-context.ps1"
. "$PSScriptRoot/deployment-transaction.ps1"
if(!$NativeWorker){Invoke-DesktopLifeNative -ScriptPath $PSCommandPath -Arguments @('-RecordPath',$RecordPath);return}
$ErrorActionPreference='Stop'
try{
    if($NativeResult){Start-Transcript -Path ($NativeResult+'.log') -Force|Out-Null}
    $record=Get-Content -LiteralPath $RecordPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $expectedData=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopLife'))
    $data=[IO.Path]::GetFullPath($record.DataDirectory)
    $backup=[IO.Path]::GetFullPath($record.Backup)
    $backupRoot=Join-Path $expectedData 'upgrade-backups'
    $installRoot=Join-Path $env:LOCALAPPDATA 'Programs/DesktopLife'
    if($data -ne $expectedData){throw 'Recovery paths do not belong to this user profile.'}
    [void](Assert-DesktopLifeChildPath $backup $backupRoot)
    if(!$record.PreviousInstallation -or !$record.PreviousExecutableSha256){throw 'No previous installed release was recorded. The data backup remains available.'}
    $oldExe=Assert-DesktopLifeChildPath $record.PreviousInstallation.Executable $installRoot
    $newExe=Assert-DesktopLifeChildPath $record.NewExecutable $installRoot
    $lock=[IO.File]::Open((Join-Path $installRoot 'upgrade.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $dataLock=$null
    # A live root lock must never be copied, moved, or restored as user history.
    $backupHashes=@($record.BackupHashes | Where-Object {$_.Path -ne 'instance.lock'})
    function Start-VerifiedApplication([string]$Executable,[string]$Sha256){
        if((Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash -ne $Sha256){throw 'Launch executable checksum mismatch.'}
        $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]1}
        $result=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=('"'+$Executable+'"');CurrentDirectory=(Split-Path $Executable -Parent);ProcessStartupInformation=$startup}
        if($result.ReturnValue -ne 0){throw "Independent launch failed: $($result.ReturnValue). The desktop shortcut remains available."}
        Write-Output "Independent application process: $($result.ProcessId)"
    }
    try{
        if((Get-FileHash -LiteralPath $oldExe).Hash -ne $record.PreviousExecutableSha256 -or (Get-FileHash -LiteralPath $newExe).Hash -ne $record.NewSha256){throw 'Installed executable checksum mismatch.'}
        foreach($entry in $backupHashes){
            $source=Assert-DesktopLifeChildPath (Join-Path $backup $entry.Path) $backup
            [void](Assert-DesktopLifeChildPath (Join-Path $data $entry.Path) $data)
            if((Get-FileHash -LiteralPath $source).Hash -ne $entry.Sha256){throw "Backup checksum mismatch: $($entry.Path)"}
        }
        Assert-DesktopLifePlainTree $backup
        $stop=Start-Process -FilePath $newExe -ArgumentList '--shutdown' -WindowStyle Hidden -PassThru
        $handle=$stop.Handle
        if(!$stop.WaitForExit(30000) -or $stop.ExitCode -ne 0){throw 'The application did not acknowledge safe shutdown. Recovery stopped.'}
        $dataLock=[IO.File]::Open((Join-Path $data 'instance.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $current=Join-Path $backupRoot ('before-rollback-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
        $metadata=Get-DesktopLifeInstallationMetadataPaths $installRoot
        try{
            Invoke-DesktopLifeDeploymentTransaction -DataDirectory $data -SnapshotDirectory $current -MetadataPaths $metadata -Action {
                Move-DesktopLifeActiveData $data (Join-Path $current 'replaced-active-data')
                Restore-DesktopLifeBackupFiles $data $backup $backupHashes
                & "$PSScriptRoot/install.ps1" -ReleaseDirectory (Split-Path $oldExe -Parent)
                $restored=Get-Content -LiteralPath (Join-Path $installRoot 'installation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                if($restored.Executable -ne $oldExe -or (Get-FileHash -LiteralPath $restored.Executable -Algorithm SHA256).Hash -ne $record.PreviousExecutableSha256){throw 'Restored installation verification failed.'}
            }
        }catch{
            $failure=$_.Exception
            # The previous active profile is verified before restarting its matching build.
            if($failure.Data['DesktopLifeCompensation'] -eq 'COMPENSATED'){
                $dataLock.Dispose();$dataLock=$null
                try{Start-VerifiedApplication $newExe $record.NewSha256}catch{Write-Warning ('Data compensation succeeded; automatic restart failed: '+$_.Exception.Message)}
            }
            throw $failure
        }
        $dataLock.Dispose();$dataLock=$null
        Start-VerifiedApplication $oldExe $record.PreviousExecutableSha256
        Write-Output "Previous version and its pre-upgrade data restored. Newer data was preserved in $current"
    }finally{if($null -ne $dataLock){$dataLock.Dispose()};$lock.Dispose()}
    Write-DesktopLifeNativeResult $NativeResult $true ''
}catch{
    Write-DesktopLifeNativeResult $NativeResult $false $_.Exception.Message
    throw
}finally{if($NativeResult){Stop-Transcript|Out-Null}}
