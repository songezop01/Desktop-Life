param([string]$ReleaseDirectory, [switch]$Launch)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$ReleaseDirectory){
    $latest=Get-Content -LiteralPath (Join-Path $root 'artifacts/standalone/latest.json') -Raw | ConvertFrom-Json
    $ReleaseDirectory=Split-Path $latest.Executable -Parent
}
$manifest=Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'release.json') -Raw | ConvertFrom-Json
$source=Join-Path $ReleaseDirectory 'DesktopLife.exe'
if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $manifest.Sha256){throw 'Release checksum mismatch.'}
if($manifest.Build -notmatch '^0\.5\.0-\d{8}-\d{6}$'){throw 'Invalid build identifier.'}
$installRoot=Join-Path $env:LOCALAPPDATA 'Programs/DesktopLife'
$target=Join-Path $installRoot $manifest.Build
$exe=Join-Path $target 'DesktopLife.exe'
New-Item -ItemType Directory -Force $target | Out-Null
if(Test-Path -LiteralPath $exe){
    if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $manifest.Sha256){throw 'An installed build differs; publish a new build.'}
}else{
    Copy-Item -LiteralPath $source -Destination $exe
    Copy-Item -LiteralPath (Join-Path $ReleaseDirectory 'release.json') -Destination $target
    Copy-Item -LiteralPath (Join-Path $ReleaseDirectory 'docs') -Destination $target -Recurse
}
$shell=New-Object -ComObject WScript.Shell
try{
    foreach($directory in @([Environment]::GetFolderPath('DesktopDirectory'),[Environment]::GetFolderPath('Programs'))){
        $link=$shell.CreateShortcut((Join-Path $directory 'Desktop Life 桌面寵物.lnk'))
        $link.TargetPath=$exe
        $link.WorkingDirectory=$target
        $link.IconLocation="$exe,0"
        $link.Description='Desktop Life 桌面寵物 — 獨立執行、房間布置與本機陪伴記憶'
        $link.Save()
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)
    }
}finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
[ordered]@{Executable=$exe;Build=$manifest.Build;DataDirectory=(Join-Path $env:LOCALAPPDATA 'DesktopLife')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installRoot 'installation.json') -Encoding UTF8
Write-Output "Installed: $exe"
Write-Output 'Desktop and Start menu shortcuts created. Existing pet data and icon backups were preserved.'
if($Launch){
    # Win32_Process creates the GUI under WmiPrvSE, outside the invoking terminal/Codex process tree.
    # Refuse fallback attachment: the user can always launch the desktop shortcut directly.
    $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]1}
    $result=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=('"'+$exe+'"');CurrentDirectory=$target;ProcessStartupInformation=$startup}
    if($result.ReturnValue -ne 0){throw "Independent launch failed: $($result.ReturnValue). Please use the desktop shortcut."}
    Write-Output "Independent application process: $($result.ProcessId)"
}
