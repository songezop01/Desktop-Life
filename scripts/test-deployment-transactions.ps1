param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/deployment-transaction.ps1"
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('artifacts/verification/0.10.1/deployment-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Choose a new isolated test output directory.'}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$results=New-Object 'System.Collections.Generic.List[object]'

function Assert-Test([bool]$Condition,[string]$Message){if(!$Condition){throw $Message}}
function Write-Fixture([string]$Path,[string]$Value){
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($Path,$Value,[Text.UTF8Encoding]::new($false))
}
function Get-ActiveFingerprint([string]$Data){
    $entries=New-Object 'System.Collections.Generic.List[string]'
    foreach($entry in @(Get-DesktopLifeActiveEntries $Data)){
        $items=if($entry.PSIsContainer){@($entry)+@(Get-ChildItem -LiteralPath $entry.FullName -Recurse -Force)}else{@($entry)}
        foreach($item in $items){
            $relative=$item.FullName.Substring($Data.Length+1)
            if($item.PSIsContainer){$entries.Add('D:'+$relative)}else{$entries.Add('F:'+$relative+':'+(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash)}
        }
    }
    (@($entries) | Sort-Object) -join "`n"
}
function Get-MetadataFingerprint([string[]]$Paths){
    (@($Paths | ForEach-Object {if(Test-Path -LiteralPath $_ -PathType Leaf){(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}else{'ABSENT'}})) -join '|'
}
function New-Fixture([string]$Name,[bool]$MetadataExisted=$true){
    $directory=Join-Path $output $Name
    $data=Join-Path $directory 'profile'
    $backup=Join-Path $data 'upgrade-backups/original-release'
    $metadata=@((Join-Path $directory 'install/installation.json'),(Join-Path $directory 'desktop/Desktop Life 桌面寵物.lnk'),(Join-Path $directory 'programs/Desktop Life 桌面寵物.lnk'))
    Write-Fixture (Join-Path $data 'organism.json') '{"Version":6,"Name":"original-current","RewardCount":170}'
    Write-Fixture (Join-Path $data 'organism.json.bak.1') 'current-generation-1'
    Write-Fixture (Join-Path $data 'settings.json') '{"Volume":0.35}'
    Write-Fixture (Join-Path $data 'imports/pending.json') 'current-pending-import'
    Write-Fixture (Join-Path $data 'audio/custom/bell.wav') 'current-custom-audio'
    Write-Fixture (Join-Path $data 'instance.lock') 'live-lock-sentinel'
    New-Item -ItemType Directory -Path (Join-Path $data 'empty-preserved-folder') -Force | Out-Null
    Write-Fixture (Join-Path $backup 'organism.json') '{"Version":5,"Name":"older-release","RewardCount":169}'
    Write-Fixture (Join-Path $backup 'organism.json.bak.1') 'older-generation-1'
    Write-Fixture (Join-Path $backup 'settings.json') '{"Volume":0.2}'
    Write-Fixture (Join-Path $backup 'audio/custom/bell.wav') 'older-custom-audio'
    New-Item -ItemType Directory -Path (Join-Path $backup 'old-empty-folder') -Force | Out-Null
    if($MetadataExisted){
        Write-Fixture $metadata[0] '{"Executable":"current-installed-build"}'
        Write-Fixture $metadata[1] 'binary-link-fixture'
        Write-Fixture $metadata[2] 'binary-link-fixture'
        [IO.File]::WriteAllBytes($metadata[1],[byte[]]@(0,1,2,13,10,255,128))
        [IO.File]::WriteAllBytes($metadata[2],[byte[]]@(80,69,84,0,255,0,17))
    }
    return [pscustomobject]@{Data=$data;Backup=$backup;Metadata=$metadata;Transaction=(Join-Path $data 'upgrade-backups/transaction');Inventory=@(Get-DesktopLifeTreeInventory $backup);Fingerprint=(Get-ActiveFingerprint $data);MetadataFingerprint=(Get-MetadataFingerprint $metadata)}
}
function Invoke-TestCase([string]$Name,[scriptblock]$Case){
    $started=[DateTime]::UtcNow
    try{& $Case;$results.Add([ordered]@{Name=$Name;Passed=$true;Seconds=([DateTime]::UtcNow-$started).TotalSeconds});Write-Output ('PASS '+$Name)}
    catch{$results.Add([ordered]@{Name=$Name;Passed=$false;Seconds=([DateTime]::UtcNow-$started).TotalSeconds;Failure=$_.Exception.Message;Stack=$_.ScriptStackTrace});Write-Output ('FAIL '+$Name+': '+$_.Exception.Message)}
}
function Invoke-FailureCase([string]$Name,[string]$Fault,[string]$Mode,[bool]$MetadataExisted=$true){
    $fixture=New-Fixture $Name $MetadataExisted
    $data=$fixture.Data;$backup=$fixture.Backup;$transaction=$fixture.Transaction;$metadata=$fixture.Metadata;$inventory=$fixture.Inventory
    $probe={param($Stage) if($Stage -eq $Fault){throw ('Injected interruption: '+$Stage)}}
    $caught=$null
    $instanceLock=[IO.File]::Open((Join-Path $data 'instance.lock'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try{
        try{
            Invoke-DesktopLifeDeploymentTransaction -DataDirectory $data -SnapshotDirectory $transaction -MetadataPaths $metadata -Action {
                if($Mode -eq 'Rollback'){
                    Move-DesktopLifeActiveData $data (Join-Path $transaction 'replaced-active-data') $probe
                    Restore-DesktopLifeBackupFiles $data $backup $inventory $probe
                }
                # The real installer uses the same nested metadata transaction.
                Invoke-DesktopLifeDeploymentTransaction -SnapshotDirectory (Join-Path $output ($Name+'-installer')) -MetadataPaths $metadata -Action {
                    foreach($index in 0..2){Write-Fixture $metadata[$index] ('replacement-metadata-'+$index);& $probe ('AfterMetadata:'+ $index)}
                    if($Mode -eq 'Upgrade'){Write-Fixture (Join-Path $data 'organism.json') 'simulated-new-writer';Write-Fixture (Join-Path $data 'introduced/file.txt') 'new-file'}
                    & $probe 'AfterInstallationVerification'
                }
            }
        }catch{$caught=$_.Exception}
        Assert-Test ($null -ne $caught) 'Injected failure was not surfaced.'
        Assert-Test ($caught.Data['DesktopLifeCompensation'] -eq 'COMPENSATED') ('Compensation failed: '+$caught.Message)
        Assert-Test ((Get-ActiveFingerprint $data) -eq $fixture.Fingerprint) 'Current profile bytes or empty directories were not restored exactly.'
        Assert-Test ((Get-MetadataFingerprint $metadata) -eq $fixture.MetadataFingerprint) 'Manifest or shortcut bytes/absence were not restored exactly.'
        $instanceLock.Dispose();$instanceLock=$null
        Assert-Test ((Get-Content -LiteralPath (Join-Path $data 'instance.lock') -Raw) -eq 'live-lock-sentinel') 'Live lock was touched.'
        Assert-DesktopLifeTreeInventory $backup $inventory
        $journal=Get-Content -LiteralPath (Join-Path $transaction 'transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-Test ($journal.State -eq 'COMPENSATED') 'Journal did not record compensation.'
        Assert-DesktopLifeTreeInventory (Join-Path $transaction 'original-data') $journal.DataHashes
    }finally{if($instanceLock){$instanceLock.Dispose()}}
}

foreach($fault in @('AfterActiveMove:0','AfterActiveMove:2','AfterBackupRestore:0','AfterBackupRestore:2','AfterMetadata:0','AfterMetadata:1','AfterMetadata:2','AfterInstallationVerification')){
    $name='rollback-'+$fault.Replace(':','-')
    Invoke-TestCase $name {Invoke-FailureCase $name $fault 'Rollback'}
}
foreach($fault in @('AfterMetadata:0','AfterMetadata:1','AfterMetadata:2','AfterInstallationVerification')){
    $name='upgrade-'+$fault.Replace(':','-')
    Invoke-TestCase $name {Invoke-FailureCase $name $fault 'Upgrade'}
}
Invoke-TestCase 'first-install-metadata-absence' {Invoke-FailureCase 'first-install-metadata-absence' 'AfterMetadata:2' 'Upgrade' $false}
Invoke-TestCase 'rollback-commit' {
    $fixture=New-Fixture 'rollback-commit'
    $data=$fixture.Data;$backup=$fixture.Backup;$transaction=$fixture.Transaction;$metadata=$fixture.Metadata;$inventory=$fixture.Inventory
    Invoke-DesktopLifeDeploymentTransaction -DataDirectory $data -SnapshotDirectory $transaction -MetadataPaths $metadata -Action {
        Move-DesktopLifeActiveData $data (Join-Path $transaction 'replaced-active-data')
        Restore-DesktopLifeBackupFiles $data $backup $inventory
        foreach($index in 0..2){Write-Fixture $metadata[$index] ('successful-older-metadata-'+$index)}
    }
    Assert-Test (!(Test-Path -LiteralPath (Join-Path $data 'imports/pending.json'))) 'Rollback left a pending newer import.'
    Assert-Test (Test-Path -LiteralPath (Join-Path $data 'old-empty-folder') -PathType Container) 'Rollback lost an empty old directory.'
    foreach($entry in $inventory){Assert-Test ((Get-FileHash -LiteralPath (Join-Path $data $entry.Path) -Algorithm SHA256).Hash -eq $entry.Sha256) 'Commit restored different bytes.'}
    $journal=Get-Content -LiteralPath (Join-Path $transaction 'transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Test ($journal.State -eq 'COMMITTED') 'Successful rollback did not commit.'
    Assert-DesktopLifeTreeInventory (Join-Path $transaction 'original-data') $journal.DataHashes
}
Invoke-TestCase 'persistent-metadata-lock-preserves-recovery' {
    $fixture=New-Fixture 'persistent-metadata-lock-preserves-recovery'
    $data=$fixture.Data;$transaction=$fixture.Transaction;$metadata=$fixture.Metadata;$locked=$null;$caught=$null
    try{
        try{
            Invoke-DesktopLifeDeploymentTransaction -DataDirectory $data -SnapshotDirectory $transaction -MetadataPaths $metadata -Action {
                Write-Fixture (Join-Path $data 'organism.json') 'partial-replacement'
                $script:testDeploymentLock=[IO.File]::Open($metadata[1],[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
                throw 'Persistent file lock injected.'
            }
        }catch{$caught=$_.Exception}
        Assert-Test ($caught.Data['DesktopLifeCompensation'] -eq 'COMPENSATION_FAILED') 'A persistent lock must not be called a successful compensation.'
        Assert-Test ($caught.Message.Contains('verified original snapshot')) 'Failure omitted the usable recovery location.'
        $journal=Get-Content -LiteralPath (Join-Path $transaction 'transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-Test ($journal.State -eq 'COMPENSATION_FAILED') 'Journal omitted incomplete compensation.'
        Assert-DesktopLifeTreeInventory (Join-Path $transaction 'original-data') $journal.DataHashes
        Assert-Test ((Get-ActiveFingerprint $data) -eq $fixture.Fingerprint) 'Data should still be restored when only a shortcut remains locked.'
    }finally{if($script:testDeploymentLock){$script:testDeploymentLock.Dispose();$script:testDeploymentLock=$null}}
}
Invoke-TestCase 'reject-traversal-and-live-lock' {
    $fixture=New-Fixture 'reject-traversal-and-live-lock'
    foreach($path in @('../outside.json','instance.lock','upgrade-backups/evil.json')){
        $caught=$false
        try{Restore-DesktopLifeBackupFiles $fixture.Data $fixture.Backup @([pscustomobject]@{Path=$path;Sha256='invalid'})}catch{$caught=$true}
        Assert-Test $caught ('Unsafe entry accepted: '+$path)
    }
    Assert-Test ((Get-ActiveFingerprint $fixture.Data) -eq $fixture.Fingerprint) 'Rejected backup entries altered active files.'
    $caught=$false
    try{Invoke-DesktopLifeDeploymentTransaction -DataDirectory $fixture.Data -SnapshotDirectory (Join-Path $output 'outside-backup-root') -Action {throw 'Should not reach action'}}catch{$caught=$true}
    Assert-Test $caught 'A transaction escaped the backup root.'
    Assert-Test (!(Test-Path -LiteralPath (Join-Path $output 'outside-backup-root'))) 'An unsafe snapshot directory was created.'
}
Invoke-TestCase 'reject-tampered-backup' {
    $fixture=New-Fixture 'reject-tampered-backup'
    Write-Fixture (Join-Path $fixture.Backup 'organism.json') 'tampered'
    $caught=$false
    try{Assert-DesktopLifeTreeInventory $fixture.Backup $fixture.Inventory}catch{$caught=$true}
    Assert-Test $caught 'Corrupted backup checksum was accepted.'
    Assert-Test ((Get-ActiveFingerprint $fixture.Data) -eq $fixture.Fingerprint) 'Backup validation altered current history.'
}
function Invoke-RealInstallerCase([string]$Name,[string]$Fault,[string]$Version='0.10.1'){
    $fixture=New-Fixture $Name
    $installRoot=Split-Path $fixture.Metadata[0] -Parent
    $directories=@($fixture.Metadata[1..2] | ForEach-Object {Split-Path $_ -Parent})
    $release=Join-Path (Split-Path $fixture.Data -Parent) 'release'
    Write-Fixture (Join-Path $release 'DesktopLife.exe') 'isolated-placeholder-executable-never-launched'
    Write-Fixture (Join-Path $release 'docs/readme.md') 'isolated-release-docs'
    $sha=(Get-FileHash -LiteralPath (Join-Path $release 'DesktopLife.exe') -Algorithm SHA256).Hash
    $build=$Version+'-20261002-235959'
    [ordered]@{Version=$Version;Build=$build;Sha256=$sha} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $release 'release.json') -Encoding UTF8
    $oldExe=Join-Path $installRoot 'prior-build/DesktopLife.exe'
    Write-Fixture $oldExe 'isolated-old-executable-never-launched'
    $shell=New-Object -ComObject WScript.Shell
    try{
        foreach($path in $fixture.Metadata[1..2]){
            # Fixture links start as valid binary .lnk files, as in a real installation.
            [IO.File]::WriteAllBytes($path,[byte[]]@())
            $link=$shell.CreateShortcut($path)
            try{$link.TargetPath=$oldExe;$link.WorkingDirectory=(Split-Path $oldExe -Parent);$link.Description='prior-installation-fixture';$link.Save()}
            finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)}
        }
    }finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
    $expected=Get-MetadataFingerprint $fixture.Metadata
    $probe={param($Stage) if($Stage -eq $Fault){throw ('Real installer interruption: '+$Stage)}}
    $caught=$null;$installedExe=$null
    try{$installedExe=Install-DesktopLifeReleaseFiles -ReleaseDirectory $release -InstallRoot $installRoot -ShortcutDirectories $directories -Probe $probe}catch{$caught=$_.Exception}
    Assert-Test ((Get-ActiveFingerprint $fixture.Data) -eq $fixture.Fingerprint) 'The real installer changed user data.'
    if($Fault){
        Assert-Test ($caught -and $caught.Data['DesktopLifeCompensation'] -eq 'COMPENSATED') 'The real installer did not compensate an interruption.'
        Assert-Test ((Get-MetadataFingerprint $fixture.Metadata) -eq $expected) 'The real installer did not restore prior shortcut and manifest bytes.'
        $transaction=@(Get-ChildItem -LiteralPath (Join-Path $installRoot 'installation-transactions') -Directory)[0].FullName
        $journal=Get-Content -LiteralPath (Join-Path $transaction 'transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-Test ($journal.State -eq 'COMPENSATED') 'Real installer journal omitted compensation.'
        if($Fault -eq 'AfterExecutableCopy'){Assert-Test (!(Test-Path -LiteralPath (Join-Path $installRoot $build))) 'Partial copying occupied the immutable build directory.'}
    }else{
        Assert-Test (!$caught) ('The real installer failed: '+$caught)
        Assert-Test ((Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash -eq $sha) 'The real installer changed the release executable.'
        Assert-Test (Test-Path -LiteralPath (Join-Path (Split-Path $installedExe -Parent) 'docs/readme.md')) 'The staged release lost docs.'
        $manifest=Get-Content -LiteralPath $fixture.Metadata[0] -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-Test ($manifest.Executable -eq $installedExe) 'The real installer manifest targets a different executable.'
        $shell=New-Object -ComObject WScript.Shell
        try{foreach($path in $fixture.Metadata[1..2]){$link=$shell.CreateShortcut($path);try{Assert-Test ($link.TargetPath -eq $installedExe) 'The real shortcut targets a different executable.'}finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)}}}
        finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
        $again=Install-DesktopLifeReleaseFiles -ReleaseDirectory $release -InstallRoot $installRoot -ShortcutDirectories $directories
        Assert-Test ($again -eq $installedExe) 'Reinstalling a verified immutable build changed the path.'
    }
}
foreach($fault in @('AfterExecutableCopy','AfterReleaseMove','AfterShortcut:0','AfterShortcut:1','AfterInstallationManifest')){
    $name='real-installer-'+$fault.Replace(':','-')
    Invoke-TestCase $name {Invoke-RealInstallerCase $name $fault}
}
Invoke-TestCase 'real-installer-commit' {Invoke-RealInstallerCase 'real-installer-commit' ''}
Invoke-TestCase 'real-installer-0.11-commit' {Invoke-RealInstallerCase 'real-installer-0.11-commit' '' '0.11.0'}
function Invoke-RestartGuardCase([string]$Name,[scriptblock]$Arrange,[string]$ExpectedState,[bool]$ShouldLaunch){
    $fixture=New-Fixture $Name
    $installRoot=Split-Path $fixture.Metadata[0] -Parent
    $oldExe=Join-Path $installRoot 'prior-build/DesktopLife.exe'
    Write-Fixture $oldExe 'isolated-guard-executable-never-launched'
    $state=New-DesktopLifeUpgradeState $fixture.Data $installRoot
    $state.PreviousExecutable=$oldExe
    $state.PreviousExecutableSha256=(Get-FileHash -LiteralPath $oldExe -Algorithm SHA256).Hash
    $state.PreviousManifest=$fixture.Metadata[0]
    $state.PreviousManifestSha256=(Get-FileHash -LiteralPath $fixture.Metadata[0] -Algorithm SHA256).Hash
    $state.PreviousWasRunning=$true;$state.ShutdownAcknowledged=$true;$state.DataLockReleased=$true
    $script:guardHeldLock=$null
    $launchTrace=Join-Path (Split-Path $fixture.Data -Parent) 'launch-request.txt'
    try{
        & $Arrange
        $started=Resume-DesktopLifePreviousApplication $state {param($exe,$sha) if($state['InjectLaunchFailure']){throw 'Isolated launch failed.'};Write-Fixture $launchTrace ($exe+'|'+$sha)}
        Assert-Test ($state.PreviousRestartState -eq $ExpectedState) ('Restart guard state was '+$state.PreviousRestartState+' instead of '+$ExpectedState)
        Assert-Test ($started -eq $ShouldLaunch) 'Restart guard returned an incorrect result.'
        Assert-Test ((Test-Path -LiteralPath $launchTrace) -eq $ShouldLaunch) 'Restart guard dispatched an unexpected application.'
        Assert-Test ((Get-ActiveFingerprint $fixture.Data) -eq $fixture.Fingerprint) 'Restart guard modified user data.'
        if($state['InjectLaunchFailure']){Assert-Test ($state.Failure -eq 'Original backup copy failure') 'Restart failure overwrote the original upgrade failure.'}
        if($ShouldLaunch){
            Assert-Test ((Get-Content -LiteralPath $launchTrace -Raw) -eq ($oldExe+'|'+$state.PreviousExecutableSha256)) 'Restart guard selected a different build/hash.'
            $repeated=Resume-DesktopLifePreviousApplication $state {throw 'A restart must not be dispatched twice.'}
            Assert-Test (!$repeated) 'Restart guard dispatched twice.'
        }
    }finally{if($script:guardHeldLock){$script:guardHeldLock.Dispose();$script:guardHeldLock=$null}}
}
Invoke-TestCase 'guard-preflight-backup-failure-resumes-original' {Invoke-RestartGuardCase 'guard-preflight-backup-failure-resumes-original' {$state.Phase='BACKING_UP';$state.Failure='Injected copy failure'} 'LAUNCH_DISPATCHED' $true}
Invoke-TestCase 'guard-recovery-copy-failure-resumes-original' {Invoke-RestartGuardCase 'guard-recovery-copy-failure-resumes-original' {$state.Phase='PREPARING_RECOVERY';$state.Failure='Injected recovery copy failure'} 'LAUNCH_DISPATCHED' $true}
Invoke-TestCase 'guard-verified-compensation-resumes-original' {Invoke-RestartGuardCase 'guard-verified-compensation-resumes-original' {$state.MutationStarted=$true;$state.CompensationStatus='COMPENSATED'} 'LAUNCH_DISPATCHED' $true}
Invoke-TestCase 'guard-before-shutdown-no-launch' {Invoke-RestartGuardCase 'guard-before-shutdown-no-launch' {$state.ShutdownAcknowledged=$false} 'NOT_STOPPED' $false}
Invoke-TestCase 'guard-original-was-closed-no-launch' {Invoke-RestartGuardCase 'guard-original-was-closed-no-launch' {$state.PreviousWasRunning=$false} 'WAS_NOT_RUNNING' $false}
Invoke-TestCase 'guard-committed-install-no-old-launch' {Invoke-RestartGuardCase 'guard-committed-install-no-old-launch' {$state.TransactionCommitted=$true} 'COMMITTED' $false}
Invoke-TestCase 'guard-possibly-new-launched-no-old-launch' {Invoke-RestartGuardCase 'guard-possibly-new-launched-no-old-launch' {$state.NewLaunchAttempted=$true} 'NEW_LAUNCH_ATTEMPTED' $false}
Invoke-TestCase 'guard-mutation-without-compensation-no-launch' {Invoke-RestartGuardCase 'guard-mutation-without-compensation-no-launch' {$state.MutationStarted=$true} 'DATA_NOT_VERIFIED' $false}
Invoke-TestCase 'guard-compensation-failed-no-launch' {Invoke-RestartGuardCase 'guard-compensation-failed-no-launch' {$state.MutationStarted=$true;$state.CompensationStatus='COMPENSATION_FAILED'} 'DATA_NOT_VERIFIED' $false}
Invoke-TestCase 'guard-own-lock-not-released-no-launch' {Invoke-RestartGuardCase 'guard-own-lock-not-released-no-launch' {$state.DataLockReleased=$false} 'LOCK_NOT_RELEASED' $false}
Invoke-TestCase 'guard-native-lock-busy-no-launch' {Invoke-RestartGuardCase 'guard-native-lock-busy-no-launch' {$script:guardHeldLock=[IO.File]::Open((Join-Path $fixture.Data 'instance.lock'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)} 'LOCK_BUSY' $false}
Invoke-TestCase 'guard-changed-executable-no-launch' {Invoke-RestartGuardCase 'guard-changed-executable-no-launch' {Write-Fixture $oldExe 'changed-since-shutdown'} 'PREVIOUS_CHANGED' $false}
Invoke-TestCase 'guard-changed-manifest-no-launch' {Invoke-RestartGuardCase 'guard-changed-manifest-no-launch' {Write-Fixture $fixture.Metadata[0] 'changed-since-shutdown'} 'PREVIOUS_CHANGED' $false}
Invoke-TestCase 'guard-missing-verified-build-no-launch' {Invoke-RestartGuardCase 'guard-missing-verified-build-no-launch' {$state.PreviousExecutableSha256=$null} 'NO_VERIFIED_PREVIOUS' $false}
Invoke-TestCase 'guard-launch-failure-retains-upgrade-failure' {Invoke-RestartGuardCase 'guard-launch-failure-retains-upgrade-failure' {$state['InjectLaunchFailure']=$true;$state.Failure='Original backup copy failure'} 'RESTART_FAILED' $false}
Invoke-TestCase 'guard-outside-install-root-no-launch' {Invoke-RestartGuardCase 'guard-outside-install-root-no-launch' {$state.PreviousExecutable=Join-Path $output 'outside/DesktopLife.exe'} 'RESTART_FAILED' $false}
$failed=@($results | Where-Object {!$_.Passed})
$summary=[ordered]@{Version='0.11.0';Runtime=$PSVersionTable.PSVersion.ToString();Passed=$failed.Count -eq 0;Cases=$results.Count;Failures=$failed.Count;Directory=$output;CompletedUtc=[DateTime]::UtcNow;Results=$results.ToArray()}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'deployment-test-summary.json') -Encoding UTF8
Write-Output ('Deployment cases: '+$results.Count+'. Failures: '+$failed.Count+'. Evidence: '+$output)
if($failed.Count -gt 0){throw 'Isolated deployment transaction tests failed.'}
