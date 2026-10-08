param([switch]$Worker,[string]$RunDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Worker){
    $RunDirectory=Join-Path $root ('artifacts/installation/0.9-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $RunDirectory | Out-Null
    $paths=[ordered]@{}
    foreach($name in @('git','python')){$cmd=Get-Command $name -ErrorAction Stop;$paths[$name]=$cmd.Source}
    $paths|ConvertTo-Json|Set-Content (Join-Path $RunDirectory 'runtime-paths.json') -Encoding UTF8
    $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
    $command='powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -Worker -RunDirectory "'+$RunDirectory+'"'
    $created=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=$root;ProcessStartupInformation=$startup}
    if($created.ReturnValue -ne 0){throw "Worker launch failed: $($created.ReturnValue)"}
    [ordered]@{ProcessId=$created.ProcessId;RunDirectory=$RunDirectory;StartedUtc=[DateTime]::UtcNow}|ConvertTo-Json|Set-Content (Join-Path $RunDirectory 'launcher.json') -Encoding UTF8
    Write-Output "V0.9 completion worker PID=$($created.ProcessId), directory=$RunDirectory"
    return
}
Start-Transcript -Path (Join-Path $RunDirectory 'completion.log') | Out-Null
$status=[ordered]@{State='VERIFYING';Installed=$false;Backup=$null;Executable=$null;Message=$null;UpdatedUtc=[DateTime]::UtcNow}
function Save-Status {$status.UpdatedUtc=[DateTime]::UtcNow;$status|ConvertTo-Json|Set-Content (Join-Path $RunDirectory 'status.json') -Encoding UTF8}
try{
    $installLock=[IO.File]::Open((Join-Path $root 'artifacts/installation/install.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $paths=Get-Content (Join-Path $RunDirectory 'runtime-paths.json') -Raw|ConvertFrom-Json
    $env:PATH=(Split-Path $paths.git -Parent)+';'+(Split-Path $paths.python -Parent)+';'+$env:PATH
    Save-Status
    $verify=Join-Path $RunDirectory 'standard'
    New-Item -ItemType Directory -Force $verify | Out-Null
    Copy-Item -LiteralPath (Join-Path $RunDirectory 'runtime-paths.json') -Destination $verify
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/verify.ps1" -Worker -Mode Standard -RunDirectory $verify
    $summary=Get-Content (Join-Path $verify 'verification-summary.json') -Raw | ConvertFrom-Json
    if($summary.Conclusion -ne 'PASS'){throw "Installation withheld: Standard conclusion $($summary.Conclusion). Read verification summary."}
    # Refuse installing a tree edited after this worker started its verification.
    foreach($file in $summary.SourceHashes){if((Get-FileHash -LiteralPath (Join-Path $root $file.Path) -Algorithm SHA256).Hash -ne $file.Sha256){throw "Source changed after verification: $($file.Path)"}}
    $status.State='PUBLISHING';Save-Status
    & "$PSScriptRoot/publish.ps1" -SkipArchive
    $release=Get-Content (Join-Path $root 'artifacts/standalone/latest.json') -Raw|ConvertFrom-Json
    if($release.Version -ne '0.9.0'){throw 'Unexpected release version'}
    $status.Executable=$release.Executable
    # Test the exact self-contained executable in its isolated smoke profile.
    $smoke=Start-Process -FilePath $release.Executable -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
    $handle=$smoke.Handle
    if(!$smoke.WaitForExit(60000)){ $smoke.Kill();throw 'Packaged isolated smoke timed out' }
    if($smoke.ExitCode -ne 0){throw "Packaged isolated smoke failed: $($smoke.ExitCode)"}
    $status.State='BACKING_UP';Save-Status
    $stop=Start-Process -FilePath $release.Executable -ArgumentList '--shutdown' -WindowStyle Hidden -PassThru
    $handle=$stop.Handle
    if(!$stop.WaitForExit(30000) -or $stop.ExitCode -ne 0){throw 'Existing application did not acknowledge safe shutdown; installation withheld'}
    $data=Join-Path $env:LOCALAPPDATA 'DesktopLife'
    if(Test-Path -LiteralPath $data){
        $backup=Join-Path $data ('upgrade-backups/before-0.9-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Force $backup | Out-Null
        Get-ChildItem -LiteralPath $data -Force | Where-Object Name -ne 'upgrade-backups' | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $backup -Recurse -Force}
        foreach($name in @('organism.json','desktop-icons-backup.json','settings.json')){
            $source=Join-Path $data $name
            if(Test-Path -LiteralPath $source){if((Get-FileHash $source).Hash -ne (Get-FileHash (Join-Path $backup $name)).Hash){throw "Backup verification failed: $name"}}
        }
        $status.Backup=$backup;Save-Status
    }
    $status.State='INSTALLING';Save-Status
    & "$PSScriptRoot/install.ps1" -ReleaseDirectory (Split-Path $release.Executable -Parent) -Launch
    $installed=Get-Content (Join-Path $env:LOCALAPPDATA 'Programs/DesktopLife/installation.json') -Raw|ConvertFrom-Json
    if((Get-FileHash -LiteralPath $installed.Executable).Hash -ne $release.Sha256){throw 'Installed executable checksum mismatch'}
    $status.Executable=$installed.Executable;$status.Installed=$true;$status.State='INSTALLED';Save-Status
    # Full runs independently. Installation success is not a claim that Full passed.
    & "$PSScriptRoot/verify.ps1" -Mode Full
    $status.Message='Installed after Standard and packaged smoke passed. Full launched independently; its result remains pending.';Save-Status
}catch{$status.State=if($status.Installed){'INSTALLED_FULL_LAUNCH_FAILED'}else{'NEEDS_REVIEW'};$status.Message=$_.Exception.Message;Save-Status;Write-Output $status.Message;exit 1}
finally{if($installLock){$installLock.Dispose()};Stop-Transcript | Out-Null}
