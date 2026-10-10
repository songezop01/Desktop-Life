param([string]$OutputDirectory, [switch]$SkipArchive)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$root=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/release-verification.ps1"
$version=Get-DesktopLifeReleaseVersion $root
$dotnet=& "$PSScriptRoot/dotnet-path.ps1"
Push-Location $root
try {
    $buildId=$version+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')
    if(!$OutputDirectory){$OutputDirectory=Join-Path $root "artifacts/standalone/$buildId"}
    $outputPath=[IO.Path]::GetFullPath($OutputDirectory)
    if(Test-Path -LiteralPath (Join-Path $outputPath 'DesktopLife.exe')){throw 'Choose a new output directory; published builds are immutable.'}
    & $dotnet publish src/DesktopLife.App -c Release -r win-x64 --self-contained true -o $outputPath --nologo -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
    if($LASTEXITCODE -ne 0){throw 'Publish failed'}
    Move-Item -LiteralPath (Join-Path $outputPath 'DesktopLife.App.exe') -Destination (Join-Path $outputPath 'DesktopLife.exe')
    Copy-Item -LiteralPath README.md -Destination (Join-Path $outputPath 'README.md') -Force
    $docs=Join-Path $outputPath 'docs'
    New-Item -ItemType Directory -Force $docs | Out-Null
    Copy-Item -LiteralPath docs/VALIDATION.md,docs/BEHAVIOR_VARIATION.md,docs/DESKTOP_ICONS.md,docs/COMPANION.md,docs/ROOM.md,docs/AUDIO_AND_RENDERING.md,docs/DISTRIBUTION.md,docs/CAT_ROOM_UPDATE.md,docs/REVIEW_2026-09-19.md -Destination $docs -Force
    Copy-Item -LiteralPath docs/UPDATE_05.md,docs/SOUND_PACKS.md,docs/UPDATE_06.md,docs/UPDATE_08.md,docs/UPDATE_08_1.md,docs/CHARACTER_CAPABILITIES.md,docs/INDIVIDUALITY_08_WORKLOG.md,docs/UPDATE_07.md,docs/HOME_07_WORKLOG.md,docs/CHARACTER_ART_PIPELINE.md,docs/CURRENT_PRODUCT_DIRECTION.md,docs/VERIFICATION_WORKFLOW.md -Destination $docs
    Copy-Item -LiteralPath docs/UPDATE_09.md,docs/UPDATE_010.md,docs/UPGRADE_ROADMAP.md,docs/ART_DIRECTION_010.md,docs/ISSUES_010.md -Destination $docs
    Copy-Item -LiteralPath docs/UPDATE_0101.md,docs/INSTALLATION_0101.md,docs/ISSUES_0101.md -Destination $docs
    Copy-Item -LiteralPath docs/UPDATE_011.md,docs/UPDATE_0111.md,docs/DESIGN_011.md,docs/DESIGN_012.md,docs/UPDATE_012.md,docs/DEVELOPMENT_012.md -Destination $docs
    $manifest=[ordered]@{Version=$version;Build=$buildId;Executable=(Join-Path $outputPath 'DesktopLife.exe');Sha256=(Get-FileHash -LiteralPath (Join-Path $outputPath 'DesktopLife.exe') -Algorithm SHA256).Hash;Architecture='win-x64';SelfContained=$true;SingleFile=$true}
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'release.json') -Encoding UTF8
    $index=Join-Path $root 'artifacts/standalone'
    New-Item -ItemType Directory -Force $index | Out-Null
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $index 'latest.json') -Encoding UTF8
    if(!$SkipArchive){Compress-Archive -LiteralPath $outputPath -DestinationPath "$outputPath.zip" -CompressionLevel Optimal}
    Write-Output "Standalone release: $($manifest.Executable)"
    Write-Output "SHA256: $($manifest.Sha256)"
}finally{Pop-Location}
