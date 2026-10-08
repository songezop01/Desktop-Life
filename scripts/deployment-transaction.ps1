# This helper runs in the native profile worker. Tests supply isolated paths;
# production callers derive profile paths without accepting path overrides.
function Assert-DesktopLifeChildPath([string]$Path,[string]$Root){
    $full=[IO.Path]::GetFullPath($Path)
    $parent=[IO.Path]::GetFullPath($Root).TrimEnd([char[]]@('\','/'))+[IO.Path]::DirectorySeparatorChar
    if(!$full.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase)){throw 'Deployment path escaped its verified root.'}
    return $full
}

function Get-DesktopLifeActiveEntries([string]$DataDirectory){
    @(Get-ChildItem -LiteralPath $DataDirectory -Force | Where-Object {$_.Name -notin @('upgrade-backups','instance.lock')})
}

function Assert-DesktopLifePlainTree([string]$Directory){
    if(!(Test-Path -LiteralPath $Directory -PathType Container)){throw "Directory is missing: $Directory"}
    $entries=@(Get-Item -LiteralPath $Directory -Force)+@(Get-ChildItem -LiteralPath $Directory -Recurse -Force)
    foreach($entry in $entries){
        if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Deployment refuses a reparse point: $($entry.FullName)"}
    }
}

function Get-DesktopLifeTreeInventory([string]$Directory){
    $full=[IO.Path]::GetFullPath($Directory).TrimEnd([char[]]@('\','/'))
    Assert-DesktopLifePlainTree $full
    @(Get-ChildItem -LiteralPath $full -Recurse -File -Force | Sort-Object FullName | ForEach-Object {
        [ordered]@{Path=$_.FullName.Substring($full.Length+1);Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}

function Assert-DesktopLifeTreeInventory([string]$Directory,[object[]]$Inventory){
    $actual=@(Get-DesktopLifeTreeInventory $Directory)
    if($actual.Count -ne @($Inventory).Count){throw "Deployment file count differs: $Directory"}
    foreach($entry in @($Inventory)){
        $path=Assert-DesktopLifeChildPath (Join-Path $Directory $entry.Path) $Directory
        if(!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Deployment checksum differs: $($entry.Path)"}
    }
}

function Write-DesktopLifeTransactionJournal([object]$Journal,[string]$Directory){
    $target=Join-Path $Directory 'transaction.json'
    $temporary=$target+'.tmp-'+[guid]::NewGuid().ToString('N')
    $Journal | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $target -Force
}

function Move-DesktopLifeActiveData([string]$DataDirectory,[string]$HoldingDirectory,[scriptblock]$Probe){
    $data=[IO.Path]::GetFullPath($DataDirectory).TrimEnd([char[]]@('\','/'))
    New-Item -ItemType Directory -Path $HoldingDirectory -Force | Out-Null
    $index=0
    foreach($entry in @(Get-DesktopLifeActiveEntries $data)){
        $source=Assert-DesktopLifeChildPath $entry.FullName $data
        $target=Assert-DesktopLifeChildPath (Join-Path $HoldingDirectory $entry.Name) $HoldingDirectory
        if([IO.Path]::GetDirectoryName($source) -ne $data){throw 'Active-data move did not name a direct child.'}
        Move-Item -LiteralPath $source -Destination $target
        if($Probe){& $Probe ('AfterActiveMove:'+ $index)}
        $index++
    }
}

function Restore-DesktopLifeBackupFiles([string]$DataDirectory,[string]$BackupDirectory,[object[]]$Inventory,[scriptblock]$Probe){
    Assert-DesktopLifePlainTree $BackupDirectory
    $paths=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in @($Inventory)){
        if($entry.Path -eq 'instance.lock' -or $entry.Path -match '^upgrade-backups([\\/]|$)'){throw 'A backup entry named a live lock or backup tree.'}
        $source=Assert-DesktopLifeChildPath (Join-Path $BackupDirectory $entry.Path) $BackupDirectory
        $target=Assert-DesktopLifeChildPath (Join-Path $DataDirectory $entry.Path) $DataDirectory
        if(!$paths.Add($target)){throw 'A recovery inventory contains a duplicate path.'}
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Backup checksum mismatch: $($entry.Path)"}
    }
    foreach($directory in @(Get-ChildItem -LiteralPath $BackupDirectory -Recurse -Directory -Force)){
        $relative=$directory.FullName.Substring(([IO.Path]::GetFullPath($BackupDirectory).TrimEnd([char[]]@('\','/'))).Length+1)
        if($relative -match '^(instance\.lock|upgrade-backups)([\\/]|$)'){throw 'A backup directory named a live lock or backup tree.'}
        $target=Assert-DesktopLifeChildPath (Join-Path $DataDirectory $relative) $DataDirectory
        New-Item -ItemType Directory -Path $target -Force | Out-Null
    }
    $index=0
    foreach($entry in @($Inventory)){
        if($entry.Path -eq 'instance.lock' -or $entry.Path -match '^upgrade-backups([\\/]|$)'){throw 'A backup entry named a live lock or backup tree.'}
        $source=Assert-DesktopLifeChildPath (Join-Path $BackupDirectory $entry.Path) $BackupDirectory
        $target=Assert-DesktopLifeChildPath (Join-Path $DataDirectory $entry.Path) $DataDirectory
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Backup checksum mismatch: $($entry.Path)"}
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target -Force
        if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Restored checksum mismatch: $($entry.Path)"}
        if($Probe){& $Probe ('AfterBackupRestore:'+ $index)}
        $index++
    }
}

function Get-DesktopLifeInstallationMetadataPaths([string]$InstallRoot){
    @((Join-Path $InstallRoot 'installation.json'),
      (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Desktop Life 桌面寵物.lnk'),
      (Join-Path ([Environment]::GetFolderPath('Programs')) 'Desktop Life 桌面寵物.lnk'))
}

function Invoke-DesktopLifeDeploymentTransaction {
    param([string]$DataDirectory,[Parameter(Mandatory=$true)][string]$SnapshotDirectory,
          [string[]]$MetadataPaths=@(),[Parameter(Mandatory=$true)][scriptblock]$Action)
    $ErrorActionPreference='Stop'
    $snapshot=[IO.Path]::GetFullPath($SnapshotDirectory).TrimEnd([char[]]@('\','/'))
    if(Test-Path -LiteralPath $snapshot){throw 'Deployment transaction snapshot already exists.'}
    if($DataDirectory){
        $data=[IO.Path]::GetFullPath($DataDirectory).TrimEnd([char[]]@('\','/'))
        $backupRoot=Join-Path $data 'upgrade-backups'
        [void](Assert-DesktopLifeChildPath $snapshot $backupRoot)
        foreach($directory in @($data,$backupRoot)){
            if((Test-Path -LiteralPath $directory) -and (((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)){throw 'Deployment data or backup root is a reparse point.'}
        }
        # Check active entries only: preserved recovery trees are deliberately excluded.
        foreach($entry in @(Get-DesktopLifeActiveEntries $data)){
            if($entry.PSIsContainer){Assert-DesktopLifePlainTree $entry.FullName}
            elseif(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Active data contains a reparse point.'}
        }
    }
    New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
    $savedData=Join-Path $snapshot 'original-data'
    $savedMetadata=Join-Path $snapshot 'original-installation'
    New-Item -ItemType Directory -Path $savedMetadata -Force | Out-Null
    $journal=[ordered]@{Version=1;State='PREPARING';StartedUtc=[DateTime]::UtcNow;DataDirectory=$DataDirectory;DataHashes=@();Metadata=@();Failure=$null;CompensationFailure=$null}
    if($DataDirectory){
        New-Item -ItemType Directory -Path $savedData -Force | Out-Null
        foreach($entry in @(Get-DesktopLifeActiveEntries $data)){Copy-Item -LiteralPath $entry.FullName -Destination $savedData -Recurse -Force}
        $journal.DataHashes=@(Get-DesktopLifeTreeInventory $savedData)
        foreach($entry in $journal.DataHashes){
            if((Get-FileHash -LiteralPath (Join-Path $data $entry.Path) -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Original data backup failed: $($entry.Path)"}
        }
    }
    $metadataIndex=0
    foreach($path in $MetadataPaths){
        $full=[IO.Path]::GetFullPath($path)
        $existed=Test-Path -LiteralPath $full -PathType Leaf
        $saved=Join-Path $savedMetadata ($metadataIndex.ToString()+'.bin')
        $sha=$null
        if($existed){
            if(((Get-Item -LiteralPath $full -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Installation metadata contains a reparse point.'}
            Copy-Item -LiteralPath $full -Destination $saved -Force
            $sha=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash
            if((Get-FileHash -LiteralPath $saved -Algorithm SHA256).Hash -ne $sha){throw 'Installation metadata backup failed.'}
        }elseif(Test-Path -LiteralPath $full){throw 'Installation metadata must be a regular file.'}
        $journal.Metadata+=@([ordered]@{Path=$full;Existed=$existed;Saved=$saved;Sha256=$sha})
        $metadataIndex++
    }
    $journal.State='PREPARED'
    Write-DesktopLifeTransactionJournal $journal $snapshot
    try{
        $journal.State='APPLYING'
        Write-DesktopLifeTransactionJournal $journal $snapshot
        & $Action
        $journal.State='COMMITTED'
        $journal['CompletedUtc']=[DateTime]::UtcNow
        Write-DesktopLifeTransactionJournal $journal $snapshot
    }catch{
        $original=$_.Exception
        $journal.Failure=$original.Message
        $journal.State='COMPENSATING'
        # A failed journal write must not prevent an attempt to recover the data.
        try{Write-DesktopLifeTransactionJournal $journal $snapshot}catch{}
        try{
            if($DataDirectory){
                Assert-DesktopLifeTreeInventory $savedData $journal.DataHashes
                $failedData=Join-Path $snapshot 'failed-active-data'
                Move-DesktopLifeActiveData $data $failedData
                # Copy directories as well as files, preserving originally empty folders.
                foreach($entry in @(Get-ChildItem -LiteralPath $savedData -Force)){Copy-Item -LiteralPath $entry.FullName -Destination $data -Recurse -Force}
                foreach($entry in $journal.DataHashes){
                    if((Get-FileHash -LiteralPath (Join-Path $data $entry.Path) -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Compensation checksum mismatch: $($entry.Path)"}
                }
                $activeCount=0
                foreach($entry in @(Get-DesktopLifeActiveEntries $data)){
                    if($entry.PSIsContainer){$activeCount+=@(Get-ChildItem -LiteralPath $entry.FullName -Recurse -File -Force).Count}else{$activeCount++}
                }
                if($activeCount -ne $journal.DataHashes.Count){throw 'Compensation left unexpected active files.'}
            }
            foreach($entry in $journal.Metadata){
                if($entry.Existed){
                    if((Get-FileHash -LiteralPath $entry.Saved -Algorithm SHA256).Hash -ne $entry.Sha256){throw 'Saved installation metadata checksum mismatch.'}
                    New-Item -ItemType Directory -Path (Split-Path $entry.Path -Parent) -Force | Out-Null
                    Copy-Item -LiteralPath $entry.Saved -Destination $entry.Path -Force
                    if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){throw 'Installation metadata compensation checksum mismatch.'}
                }elseif(Test-Path -LiteralPath $entry.Path){
                    $failed=Join-Path $snapshot ('created-metadata-'+[guid]::NewGuid().ToString('N'))
                    [void](Assert-DesktopLifeChildPath $failed $snapshot)
                    if(!(Test-Path -LiteralPath $entry.Path -PathType Leaf)){throw 'Created installation metadata changed into a directory.'}
                    Move-Item -LiteralPath $entry.Path -Destination $failed
                }
            }
            $journal.State='COMPENSATED'
            $journal['CompletedUtc']=[DateTime]::UtcNow
            try{Write-DesktopLifeTransactionJournal $journal $snapshot}catch{}
            $failure=[InvalidOperationException]::new("Deployment failed and the original data, manifest and shortcuts were restored. Original failure: "+$original.Message+". Verified snapshot: "+$snapshot,$original)
            $failure.Data['DesktopLifeCompensation']='COMPENSATED'
        }catch{
            $journal.State='COMPENSATION_FAILED'
            $journal.CompensationFailure=$_.Exception.Message
            try{Write-DesktopLifeTransactionJournal $journal $snapshot}catch{}
            $failure=[InvalidOperationException]::new("Deployment failed; automatic compensation could not finish: "+$journal.CompensationFailure+". Keep the verified original snapshot: "+$snapshot+". Original failure: "+$original.Message,$original)
            $failure.Data['DesktopLifeCompensation']='COMPENSATION_FAILED'
        }
        $failure.Data['DesktopLifeSnapshot']=$snapshot
        throw $failure
    }
}

function Install-DesktopLifeReleaseFiles {
    param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,
          [Parameter(Mandatory=$true)][string]$InstallRoot,
          [Parameter(Mandatory=$true)][string[]]$ShortcutDirectories,
          [scriptblock]$Probe)
    $ErrorActionPreference='Stop'
    $manifest=Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'release.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if($manifest.Build -notmatch '^\d+\.\d+\.\d+-\d{8}-\d{6}$'){throw 'Invalid build identifier.'}
    $source=Join-Path $ReleaseDirectory 'DesktopLife.exe'
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $manifest.Sha256){throw 'Release checksum mismatch.'}
    $target=Assert-DesktopLifeChildPath (Join-Path $InstallRoot $manifest.Build) $InstallRoot
    $exe=Join-Path $target 'DesktopLife.exe'
    New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
    $metadata=@((Join-Path $InstallRoot 'installation.json'))+@($ShortcutDirectories | ForEach-Object {Join-Path $_ 'Desktop Life 桌面寵物.lnk'})
    $transaction=Join-Path $InstallRoot ('installation-transactions/'+[guid]::NewGuid().ToString('N'))
    Invoke-DesktopLifeDeploymentTransaction -SnapshotDirectory $transaction -MetadataPaths $metadata -Action {
        if(Test-Path -LiteralPath $target){
            if(!(Test-Path -LiteralPath $exe -PathType Leaf) -or (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $manifest.Sha256){throw 'An installed build differs; publish a new build.'}
            $installedManifest=Get-Content -LiteralPath (Join-Path $target 'release.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            if($installedManifest.Build -ne $manifest.Build -or $installedManifest.Sha256 -ne $manifest.Sha256 -or !(Test-Path -LiteralPath (Join-Path $target 'docs') -PathType Container)){throw 'An existing immutable build is incomplete.'}
        }else{
            $staging=Assert-DesktopLifeChildPath (Join-Path $InstallRoot ($manifest.Build+'.staging-'+[guid]::NewGuid().ToString('N'))) $InstallRoot
            New-Item -ItemType Directory -Path $staging | Out-Null
            Copy-Item -LiteralPath $source -Destination (Join-Path $staging 'DesktopLife.exe')
            if($Probe){& $Probe 'AfterExecutableCopy'}
            Copy-Item -LiteralPath (Join-Path $ReleaseDirectory 'release.json') -Destination $staging
            Copy-Item -LiteralPath (Join-Path $ReleaseDirectory 'docs') -Destination $staging -Recurse
            if((Get-FileHash -LiteralPath (Join-Path $staging 'DesktopLife.exe') -Algorithm SHA256).Hash -ne $manifest.Sha256){throw 'Staged executable checksum mismatch.'}
            # Both absolute paths are verified children before moving the directory.
            Move-Item -LiteralPath $staging -Destination $target
            if($Probe){& $Probe 'AfterReleaseMove'}
        }
        $shell=New-Object -ComObject WScript.Shell
        try{
            $shortcutIndex=0
            foreach($directory in $ShortcutDirectories){
                New-Item -ItemType Directory -Path $directory -Force | Out-Null
                $path=Join-Path $directory 'Desktop Life 桌面寵物.lnk'
                $link=$shell.CreateShortcut($path)
                try{
                    $link.TargetPath=$exe
                    $link.WorkingDirectory=$target
                    $link.IconLocation="$exe,0"
                    $link.Description='Desktop Life 桌面寵物 — 獨立執行、房間布置與本機陪伴記憶'
                    $link.Save()
                }finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)}
                $check=$shell.CreateShortcut($path)
                try{if($check.TargetPath -ne $exe -or $check.WorkingDirectory -ne $target){throw 'Saved shortcut verification failed.'}}
                finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($check)}
                if($Probe){& $Probe ('AfterShortcut:'+ $shortcutIndex)}
                $shortcutIndex++
            }
        }finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
        $manifestPath=Join-Path $InstallRoot 'installation.json'
        $temporary=$manifestPath+'.tmp-'+[guid]::NewGuid().ToString('N')
        [ordered]@{Executable=$exe;Build=$manifest.Build;DataDirectory=(Join-Path $env:LOCALAPPDATA 'DesktopLife')} | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
        Move-Item -LiteralPath $temporary -Destination $manifestPath -Force
        if($Probe){& $Probe 'AfterInstallationManifest'}
        $check=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if($check.Executable -ne $exe -or (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $manifest.Sha256){throw 'Installed executable checksum mismatch.'}
    }
    return $exe
}

function New-DesktopLifeUpgradeState([string]$DataDirectory,[string]$InstallRoot){
    [ordered]@{Phase='PREFLIGHT';FailureState=$null;Failure=$null;DataDirectory=$DataDirectory;InstallRoot=$InstallRoot;
        PreviousExecutable=$null;PreviousExecutableSha256=$null;PreviousManifest=$null;PreviousManifestSha256=$null;
        PreviousWasRunning=$false;ShutdownAcknowledged=$false;MutationStarted=$false;TransactionCommitted=$false;
        CompensationStatus='NONE';NewLaunchAttempted=$false;DataLockReleased=$false;
        PreviousRestartAttempted=$false;PreviousRestartState='NOT_NEEDED';PreviousRestartFailure=$null}
}

function Resume-DesktopLifePreviousApplication([Collections.IDictionary]$State,[scriptblock]$Launch){
    # This guard is deliberately separate from the native process launcher so
    # tests exercise every decision without starting an application or profile.
    if(!$State.ShutdownAcknowledged){$State.PreviousRestartState='NOT_STOPPED';return $false}
    if(!$State.PreviousWasRunning){$State.PreviousRestartState='WAS_NOT_RUNNING';return $false}
    if($State.TransactionCommitted){$State.PreviousRestartState='COMMITTED';return $false}
    if($State.NewLaunchAttempted){$State.PreviousRestartState='NEW_LAUNCH_ATTEMPTED';return $false}
    if($State.MutationStarted -and $State.CompensationStatus -ne 'COMPENSATED'){$State.PreviousRestartState='DATA_NOT_VERIFIED';return $false}
    if(!$State.DataLockReleased){$State.PreviousRestartState='LOCK_NOT_RELEASED';return $false}
    if(!$State.PreviousExecutable -or !$State.PreviousExecutableSha256 -or !$State.PreviousManifestSha256){$State.PreviousRestartState='NO_VERIFIED_PREVIOUS';return $false}
    if($State.PreviousRestartAttempted){return $false}
    try{
        $oldExe=Assert-DesktopLifeChildPath $State.PreviousExecutable $State.InstallRoot
        $manifest=Assert-DesktopLifeChildPath $State.PreviousManifest $State.InstallRoot
        if((Get-FileHash -LiteralPath $oldExe -Algorithm SHA256).Hash -ne $State.PreviousExecutableSha256 -or (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash -ne $State.PreviousManifestSha256){
            $State.PreviousRestartState='PREVIOUS_CHANGED';return $false
        }
        # Disposing our stream is insufficient evidence when another native app
        # has already acquired the profile. Probe the actual lock before launch.
        $probe=$null
        try{$probe=[IO.File]::Open((Join-Path $State.DataDirectory 'instance.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
        catch{$State.PreviousRestartState='LOCK_BUSY';$State.PreviousRestartFailure=$_.Exception.Message;return $false}
        finally{if($probe){$probe.Dispose()}}
        $State.PreviousRestartAttempted=$true
        & $Launch $oldExe $State.PreviousExecutableSha256 | Out-Null
        $State.PreviousRestartState='LAUNCH_DISPATCHED'
        return $true
    }catch{
        $State.PreviousRestartState='RESTART_FAILED'
        $State.PreviousRestartFailure=$_.Exception.Message
        return $false
    }
}
