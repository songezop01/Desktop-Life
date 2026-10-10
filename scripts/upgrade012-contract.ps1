# Pure upgrade guards. Callers load deployment-transaction.ps1 and
# release-verification.ps1 first; no process or user-profile operation occurs here.
function Assert-DesktopLife012InitialRelease([string]$Version,$Summary){
    # This entry point handles the first cross-minor release only. Future 0.12.x
    # patches must use Standard rather than silently inheriting a Full gate.
    if($Version -cne '0.12.0'){throw 'This upgrade entry point is restricted to the stable initial 0.12.0 release.'}
    if($Summary.Version -cne $Version -or $Summary.Mode -cne 'Full' -or $Summary.Conclusion -cne 'PASS' -or $Summary.SourceSealed -isnot [bool] -or $Summary.SourceSealed -cne $true){throw 'A matching sealed 0.12.0 Full PASS is required before packaging or shutdown.'}
}

function Assert-DesktopLife012PublishedRelease($Release,[string]$ProjectRoot){
    if($Release.Version -cne '0.12.0' -or $Release.Build -notmatch '^0\.12\.0-\d{8}-\d{6}$' -or $Release.Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or $Release.Architecture -cne 'win-x64' -or $Release.SelfContained -isnot [bool] -or $Release.SelfContained -cne $true -or $Release.SingleFile -isnot [bool] -or $Release.SingleFile -cne $true){throw 'The published package is not a stable self-contained 0.12.0 release.'}
    $standalone=Join-Path $ProjectRoot 'artifacts/standalone'
    $expected=Join-Path (Join-Path $standalone $Release.Build) 'DesktopLife.exe'
    $executable=Assert-DesktopLifeChildPath ([string]$Release.Executable) $standalone
    if($executable -ine [IO.Path]::GetFullPath($expected) -or !(Test-Path -LiteralPath $executable -PathType Leaf)){throw 'Published executable path does not match the verified build identifier.'}
    $directory=Split-Path $executable -Parent
    Assert-DesktopLifePlainTree $directory
    if((Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash -ne $Release.Sha256){throw 'Published executable checksum mismatch.'}
    $manifest=Read-DesktopLifeEvidence (Join-Path $directory 'release.json')
    foreach($field in @('Version','Build','Executable','Sha256','Architecture','SelfContained','SingleFile')){
        if($manifest.$field -cne $Release.$field){throw "Published release index and package manifest differ: $field"}
    }
    return $directory
}

function Assert-DesktopLife012PackageSnapshot($Release,[object[]]$PackageInventory,$Binary){
    $source=[IO.Path]::GetFullPath((Split-Path ([string]$Release.Executable) -Parent)).TrimEnd([char[]]@('\','/'))
    if(![IO.Path]::IsPathRooted([string]$Binary.BinarySource) -or [IO.Path]::GetFullPath([string]$Binary.BinarySource).TrimEnd([char[]]@('\','/')) -ine $source){throw 'Packaged component used a different binary source.'}
    if([IO.Path]::GetFileName([string]$Binary.Executable) -cne 'DesktopLife.exe'){throw 'Packaged component did not use the standalone apphost.'}
    $snapshot=@($Binary.Files|ForEach-Object {[ordered]@{Path=$_.Name;Sha256=$_.Sha256}})
    Assert-DesktopLifeInventory $PackageInventory $snapshot 'Packaged component'
    $app=@($snapshot|Where-Object Path -CEQ 'DesktopLife.exe')
    if($app.Count -ne 1 -or $app[0].Sha256 -ne $Release.Sha256){throw 'Packaged component executable checksum mismatch.'}
}

function Get-DesktopLife012InstalledInventory([object[]]$PackageInventory){
    # Install-DesktopLifeReleaseFiles copies these entries. README.md remains in
    # the immutable published package and is still included in component checks.
    Assert-DesktopLifeInventory $PackageInventory $PackageInventory 'Published package'
    $installed=@($PackageInventory|Where-Object {$_.Path -ceq 'DesktopLife.exe' -or $_.Path -ceq 'release.json' -or $_.Path -cmatch '^docs[\\/]'} )
    if(@($installed|Where-Object Path -CEQ 'DesktopLife.exe').Count -ne 1 -or @($installed|Where-Object Path -CEQ 'release.json').Count -ne 1 -or @($installed|Where-Object {$_.Path -cmatch '^docs[\\/]'}).Count -eq 0){throw 'The package lacks the executable, manifest or installed documentation.'}
    return $installed
}

function Assert-DesktopLife012DataRoots([string]$DataDirectory){
    $data=[IO.Path]::GetFullPath($DataDirectory).TrimEnd([char[]]@('\','/'))
    foreach($directory in @($data,(Join-Path $data 'upgrade-backups'))){
        if(Test-Path -LiteralPath $directory){
            $entry=Get-Item -LiteralPath $directory -Force
            if(!$entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Upgrade data and backup roots must be ordinary directories.'}
        }
    }
}

function Get-DesktopLife012ActiveInventory([string]$DataDirectory){
    $data=[IO.Path]::GetFullPath($DataDirectory).TrimEnd([char[]]@('\','/'))
    Assert-DesktopLife012DataRoots $data
    if(!(Test-Path -LiteralPath $data -PathType Container)){throw 'Upgrade profile directory is missing.'}
    $files=[Collections.Generic.List[object]]::new();$directories=[Collections.Generic.List[string]]::new()
    foreach($entry in @(Get-DesktopLifeActiveEntries $data)){
        if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Upgrade data cannot contain redirected entries.'}
        $items=if($entry.PSIsContainer){Assert-DesktopLifePlainTree $entry.FullName;@($entry)+@(Get-ChildItem -LiteralPath $entry.FullName -Recurse -Force)}else{@($entry)}
        foreach($item in $items){
            $path=Assert-DesktopLifeChildPath $item.FullName $data
            $relative=$path.Substring($data.Length+1)
            if($item.PSIsContainer){$directories.Add($relative)}else{$files.Add([ordered]@{Path=$relative;Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash})}
        }
    }
    [pscustomobject]@{Files=@($files.ToArray()|Sort-Object Path);Directories=@($directories.ToArray()|Sort-Object)}
}

function Assert-DesktopLife012Backup([string]$BackupDirectory,$Expected){
    $actual=@(Get-DesktopLifeTreeInventory $BackupDirectory)
    if(@($Expected.Files).Count -gt 0 -or $actual.Count -gt 0){Assert-DesktopLifeInventory @($Expected.Files) $actual 'Upgrade backup'}
    $base=[IO.Path]::GetFullPath($BackupDirectory).TrimEnd([char[]]@('\','/'))
    $directories=@(Get-ChildItem -LiteralPath $base -Recurse -Directory -Force|ForEach-Object {$_.FullName.Substring($base.Length+1)}|Sort-Object)
    if(($directories -join "`n") -cne (@($Expected.Directories|Sort-Object) -join "`n")){throw 'Upgrade backup directory inventory differs, including empty folders.'}
    return $actual
}
