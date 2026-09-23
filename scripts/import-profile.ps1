param([Parameter(Mandatory=$true)][string]$SourceDirectory, [Parameter(Mandatory=$true)][string]$ReportPath)
$ErrorActionPreference='Stop'
$data=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DesktopLife'
$source=[IO.Path]::GetFullPath($SourceDirectory)
if($source.TrimEnd('\') -eq $data.TrimEnd('\')){throw 'Source must be an exported profile directory.'}
foreach($name in @('organism.json','settings.json')){
    $parsed=Get-Content -LiteralPath (Join-Path $source $name) -Raw | ConvertFrom-Json
    if(!$parsed.SchemaVersion){throw "Invalid profile file: $name"}
}
New-Item -ItemType Directory -Force $data | Out-Null
# This command must run outside a packaged host when migrating that host's virtualized AppData.
# Never replace data while a normal instance owns the profile.
$lock=[IO.File]::Open((Join-Path $data 'instance.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
    if(Test-Path -LiteralPath (Join-Path $data 'desktop-icons-backup.json.pending')){throw 'Restore the pending desktop icon layout before importing a profile.'}
    $backup=Join-Path $data ('upgrade-backups/before-profile-import-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $backup | Out-Null
    Get-ChildItem -LiteralPath $data -File | Where-Object {$_.Name -like '*.json*' -and $_.Name -notlike '*.lock'} | Copy-Item -Destination $backup
    $files=Get-ChildItem -LiteralPath $source -File | Where-Object {$_.Name -like '*.json*' -and $_.Name -notlike '*.lock' -and $_.Name -ne 'backup-hashes.json'}
    $records=foreach($file in $files){
        $destination=Join-Path $data $file.Name
        Copy-Item -LiteralPath $file.FullName -Destination ($destination+'.importing')
        Move-Item -LiteralPath ($destination+'.importing') -Destination $destination -Force
        $hash=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if($hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash){throw "Import verification failed: $($file.Name)"}
        [ordered]@{Name=$file.Name;Sha256=$hash}
    }
    [ordered]@{DataRoot=$data;Source=$source;OriginalProfileBackup=$backup;Files=@($records)} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
}finally{$lock.Dispose()}
