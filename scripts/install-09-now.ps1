# Explicit immediate-install workflow requested by the user; no verification result is rewritten.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$release=Get-Content (Join-Path $root 'artifacts/standalone/latest.json') -Raw|ConvertFrom-Json
if($release.Version -ne '0.9.0'){throw 'V0.9 package missing'}
if((Get-FileHash -LiteralPath $release.Executable).Hash -ne $release.Sha256){throw 'Package checksum mismatch'}
$stop=Start-Process -FilePath $release.Executable -ArgumentList '--shutdown' -WindowStyle Hidden -PassThru
$handle=$stop.Handle
if(!$stop.WaitForExit(30000) -or $stop.ExitCode -ne 0){throw 'Old version did not finish safe shutdown; real data not touched'}
$data=Join-Path $env:LOCALAPPDATA 'DesktopLife'
$backup=$null
if(Test-Path -LiteralPath $data){
    $backup=Join-Path $data ('upgrade-backups/before-0.9-immediate-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $backup | Out-Null
    Get-ChildItem -LiteralPath $data -Force | Where-Object Name -ne 'upgrade-backups' | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $backup -Recurse -Force}
    foreach($name in @('organism.json','desktop-icons-backup.json','settings.json')){
        $source=Join-Path $data $name
        if(Test-Path -LiteralPath $source){if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath (Join-Path $backup $name)).Hash){throw "Backup checksum mismatch: $name"}}
    }
}
& "$PSScriptRoot/install.ps1" -ReleaseDirectory (Split-Path $release.Executable -Parent) -Launch
$installed=Get-Content (Join-Path $env:LOCALAPPDATA 'Programs/DesktopLife/installation.json') -Raw|ConvertFrom-Json
if((Get-FileHash -LiteralPath $installed.Executable).Hash -ne $release.Sha256){throw 'Installed checksum mismatch'}
$result=[ordered]@{Installed=$true;Version='0.9.0';Executable=$installed.Executable;Backup=$backup;AtUtc=[DateTime]::UtcNow;Verification='Immediate install explicitly requested; Standard and Full not confirmed passed.'}
$result|ConvertTo-Json|Set-Content (Join-Path $root 'artifacts/installation/immediate-09.json') -Encoding UTF8
$result|ConvertTo-Json|Write-Output
