param([string]$ReleaseDirectory, [switch]$Launch)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/deployment-transaction.ps1"
$root=Split-Path $PSScriptRoot -Parent
if(!$ReleaseDirectory){
    $latest=Get-Content -LiteralPath (Join-Path $root 'artifacts/standalone/latest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $ReleaseDirectory=Split-Path $latest.Executable -Parent
}
# The shared installer validates the build identifier before creating any files.
# Keep one validation path so newer releases use the same guarded transaction.
$installRoot=Join-Path $env:LOCALAPPDATA 'Programs/DesktopLife'
$exe=Install-DesktopLifeReleaseFiles -ReleaseDirectory $ReleaseDirectory -InstallRoot $installRoot -ShortcutDirectories @([Environment]::GetFolderPath('DesktopDirectory'),[Environment]::GetFolderPath('Programs'))
Write-Output "Installed: $exe"
Write-Output 'Desktop and Start menu shortcuts created. Existing pet data and icon backups were preserved.'
if($Launch){
    # WMI creates the GUI outside the invoking terminal/Codex process tree.
    $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]1}
    $result=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=('"'+$exe+'"');CurrentDirectory=(Split-Path $exe -Parent);ProcessStartupInformation=$startup}
    if($result.ReturnValue -ne 0){throw "Independent launch failed: $($result.ReturnValue). Please use the desktop shortcut."}
    Write-Output "Independent application process: $($result.ProcessId)"
}
