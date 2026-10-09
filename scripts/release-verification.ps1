function Get-DesktopLifeReleaseVersion([string]$ProjectRoot){
    [xml]$project=Get-Content -LiteralPath (Join-Path $ProjectRoot 'src/DesktopLife.App/DesktopLife.App.csproj') -Raw -Encoding UTF8
    $versions=@($project.Project.PropertyGroup.Version|Where-Object {$_}|Select-Object -Unique)
    if($versions.Count -ne 1 -or $versions[0] -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){
        throw 'The application must declare one stable major.minor.patch Version.'
    }
    $version=[string]$versions[0]
    foreach($name in @('AssemblyVersion','FileVersion')){
        $values=@($project.Project.PropertyGroup.$name|Where-Object {$_}|Select-Object -Unique)
        if($values.Count -ne 1 -or $values[0] -ne ($version+'.0')){throw "$name must match the release Version."}
    }
    $version
}
function Get-DesktopLifeReleaseVerificationMode([string]$Version){
    if($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){throw 'A stable release Version is required to select the verification policy.'}
    # A new minor series retains the Full release gate. Same-series patches
    # default to Standard, so an installation never silently starts a long soak.
    if($Matches[3] -eq '0'){'Full'}else{'Standard'}
}
function Get-DesktopLifeSourceInventory([string]$ProjectRoot){
    $extensions=@('.cs','.xaml','.csproj','.ps1','.psm1','.psd1','.py','.png','.ico','.json','.manifest','.props','.targets','.resx','.config','.svg','.jpg','.jpeg','.webp','.wav','.mp3','.otf','.ttf','.cmd','.bat')
    $prefixLength=$ProjectRoot.TrimEnd('\','/').Length+1
    $files=@(Get-ChildItem (Join-Path $ProjectRoot 'src'),(Join-Path $ProjectRoot 'tests'),(Join-Path $ProjectRoot 'scripts') -Recurse -File | Where-Object {$_.FullName.Substring($prefixLength) -notmatch '(^|[\\/])(bin|obj|artifacts)[\\/]' -and $_.Extension -in $extensions})
    $files+=Get-ChildItem -LiteralPath $ProjectRoot -File | Where-Object {$_.Name -eq 'global.json' -or $_.Extension -in @('.slnx','.props','.targets','.config')}
    @($files|Sort-Object FullName|ForEach-Object {[ordered]@{Path=$_.FullName.Substring($ProjectRoot.TrimEnd('\','/').Length+1);Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
}
function Get-DesktopLifeBuildInventory([string]$ProjectRoot){
    $directory=Join-Path $ProjectRoot 'src/DesktopLife.App/bin/Release/net10.0-windows'
    @('DesktopLife.App.exe','DesktopLife.App.dll','DesktopLife.Core.dll','DesktopLife.Windows.dll','DesktopLife.App.deps.json','DesktopLife.App.runtimeconfig.json')|ForEach-Object {
        $file=Join-Path $directory $_
        [ordered]@{Path=$file.Substring($ProjectRoot.TrimEnd('\','/').Length+1);Sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
    }
}
function Assert-DesktopLifeInventory($Expected,$Current,[string]$Label){
    if(!$Expected -or @($Expected).Count -eq 0){throw "$Label inventory is missing."}
    foreach($entry in $Expected){
        if(!$entry.Path -or [IO.Path]::IsPathRooted($entry.Path) -or $entry.Path -match '(^|[\\/])\.\.([\\/]|$)' -or $entry.Sha256 -notmatch '^[0-9a-fA-F]{64}$'){
            throw "$Label inventory contains an invalid path or hash."
        }
    }
    $expectedPaths=@($Expected|ForEach-Object Path|Sort-Object)
    $currentPaths=@($Current|ForEach-Object Path|Sort-Object)
    if(@($expectedPaths|Select-Object -Unique).Count -ne $expectedPaths.Count -or ($expectedPaths -join "`n") -cne ($currentPaths -join "`n")){
        throw "$Label inventory changed after verification."
    }
    $currentByPath=@{};foreach($entry in $Current){$currentByPath[$entry.Path]=$entry.Sha256}
    foreach($entry in $Expected){if($currentByPath[$entry.Path] -ne $entry.Sha256){throw "$Label changed after verification: $($entry.Path)"}}
}
function Assert-DesktopLifeSourceInventory($Expected,[string]$ProjectRoot){
    Assert-DesktopLifeInventory $Expected (Get-DesktopLifeSourceInventory $ProjectRoot) 'Source'
}
function Assert-DesktopLifeBuildInventory($Expected,[string]$ProjectRoot){
    Assert-DesktopLifeInventory $Expected (Get-DesktopLifeBuildInventory $ProjectRoot) 'Build'
}
function Read-DesktopLifeEvidence([string]$Path){
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)){throw "Required verification evidence is missing: $Path"}
    Get-Content -LiteralPath $Path -Raw -Encoding UTF8|ConvertFrom-Json
}
function Assert-DesktopLifeStressEvidence($BuildHashes,[string]$RunDirectory,[ValidateSet('short','soak')][string]$Label){
    $appHash=($BuildHashes|Where-Object {[IO.Path]::GetFileName($_.Path) -eq 'DesktopLife.App.dll'}).Sha256
    $coreHash=($BuildHashes|Where-Object {[IO.Path]::GetFileName($_.Path) -eq 'DesktopLife.Core.dll'}).Sha256
    $directory=Join-Path $RunDirectory ('raw/'+$Label)
    $stress=Read-DesktopLifeEvidence (Join-Path $directory 'stress-report.json')
    $analysis=Read-DesktopLifeEvidence (Join-Path $directory 'analysis.json')
    $restart=Read-DesktopLifeEvidence (Join-Path $directory 'restart-report.json')
    $binary=Read-DesktopLifeEvidence (Join-Path $directory 'stress-binary.json')
    $minimum=if($Label -eq 'soak'){7200}else{600}
    if([int]$stress.RequestedSeconds -lt $minimum -or [double]$stress.Seconds -lt [int]$stress.RequestedSeconds){throw "$Label workload is shorter than the required duration."}
    if($stress.Presence -ne 'All' -or [int]$stress.Floors -ne 3 -or (@($stress.Characters|Sort-Object) -join ',') -ne 'BorderCollie,Cat,Girl'){
        throw "$Label did not verify all three characters in the three-floor house."
    }
    foreach($counter in @('Exceptions','StuckSequences','InvalidFurnitureInteractions','HouseRouteFailures','ApproachTimeouts','NavigationRecoveryTimeouts','UnexpectedNavigationRecoveryAttempts')){
        if(($stress.$counter -isnot [int] -and $stress.$counter -isnot [long]) -or $stress.$counter -ne 0){throw "$Label has a missing or nonzero reliability counter: $counter"}
    }
    if([int]$stress.StairTrips -lt 1){throw "$Label did not exercise any stair traversal."}
    if($analysis.Conclusion -ne 'PASS' -or $restart.Succeeded -ne $true -or $stress.DurableReloadVerification.Succeeded -ne $true){throw "$Label analysis, durable reload, or process restart did not pass."}
    if($binary.AppAssemblySha256 -ne $appHash -or $binary.CoreAssemblySha256 -ne $coreHash){throw "$Label tested different application binaries."}
}
function Assert-DesktopLifeVerificationEvidence($Summary,[string]$ProjectRoot,[string]$RunDirectory,[ValidateSet('Standard','Full')][string]$Mode){
    $version=Get-DesktopLifeReleaseVersion $ProjectRoot
    $requiredMode=Get-DesktopLifeReleaseVerificationMode $version
    if(!$Mode){$Mode=$requiredMode}
    if($requiredMode -eq 'Full' -and $Mode -ne 'Full'){throw "The $version initial minor release requires Full verification."}
    if($Summary.Version -ne $version -or $Summary.Conclusion -ne 'PASS' -or $Summary.Mode -ne $Mode){throw "A passing $version $Mode verification is required."}
    if($Summary.SourceSealed -ne $true){throw 'Verification did not seal its source and build after testing.'}
    $required=@('Build','Tests','Analyzer','Deployment','WpfSmoke','HouseMixedDpi','Identity','Longitudinal','ShortStress','DualPresence')
    if($Mode -eq 'Full'){$required+='Soak'}
    foreach($stage in $required){if($Summary.Stages.$stage -ne 'PASS'){throw "Required verification stage did not pass: $stage"}}
    if([int]$Summary.Tests.passed -lt 1 -or [int]$Summary.Tests.failed -ne 0){throw 'Passing unit-test evidence is missing.'}
    Assert-DesktopLifeSourceInventory $Summary.SourceHashes $ProjectRoot
    Assert-DesktopLifeBuildInventory $Summary.BuildHashes $ProjectRoot
    $labels=if($Mode -eq 'Full'){@('short','soak')}else{@('short')}
    foreach($label in $labels){
        Assert-DesktopLifeStressEvidence $Summary.BuildHashes $RunDirectory $label
    }
    $house=Read-DesktopLifeEvidence (Join-Path $RunDirectory 'raw/house-mixed-dpi/isolated-run/house-summary.json')
    $displays=Read-DesktopLifeEvidence (Join-Path $RunDirectory 'raw/house-mixed-dpi/isolated-run/display-report.json')
    if($house.Succeeded -ne $true -or $displays.Succeeded -ne $true -or @($displays.VisitedDisplays).Count -lt 3){throw 'Native house/display verification evidence is incomplete.'}
    foreach($group in @($displays.VisitedDisplays|Group-Object Id)){
        if((@($group.Group|ForEach-Object Floors|Sort-Object -Unique) -join ',') -ne '1,2,3'){throw 'Native display verification did not cover each fixed floor preset.'}
    }
}
