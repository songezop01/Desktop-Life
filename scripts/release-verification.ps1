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
function Get-DesktopLifeComponentSpecifications([string]$Version){
    # Preserve the sealed 0.11 evidence contract. New interaction checks are
    # mandatory throughout the 0.12 series, including Standard patch releases.
    if($Version -notmatch '^0\.12\.') { return }
    @(
        [pscustomobject]@{Stage='FoodInteraction';Directory='food-interaction';Argument='--food-test';Reports=@('food-interaction-checks.json');SuccessProperty='Passed';SuccessValue=$true;RequiredCases=@('empty-bowl-no-eating','actual-eating-consumes-one','atomic-food-and-needs-reload','exclusive-shared-bowl','replacement-invalidates-old-approach','girl-table-eating','last-portion-stops-eating','legacy-command-cannot-grant-food','five-distinct-transparent-meals-and-decreasing-kibble')},
        [pscustomobject]@{Stage='DirectInteraction';Directory='direct-interaction';Argument='--direct-care-test';Reports=@('direct-care-report.json','shared-teaser-report.json');SuccessProperty='Status';SuccessValue='PASS';RequiredCases=@('old-care-menu-and-target-removed','Cat-stationary-intent-contact-cooldown-and-pause','Girl-stationary-intent-contact-cooldown-and-pause','BorderCollie-stationary-intent-contact-cooldown-and-pause','native-occlusion-blocks-care','actual-comb-drag-single-contact-and-return','Girl-actual-comb-contact','BorderCollie-actual-comb-contact','editing-cancels-held-tool','atomic-care-and-needs-reload','cat-and-dog-shared-teaser-native')},
        [pscustomobject]@{Stage='RuntimeDiagnostics';Directory='runtime-diagnostics';Argument='--runtime-diagnostics-test';Reports=@('runtime-diagnostics-protocol.json');SuccessProperty='Succeeded';SuccessValue=$true;RequiredCases=@()},
        [pscustomobject]@{Stage='HouseMotion';Directory='house-motion';Argument='--house-motion-test';Reports=@('house-motion-report.json');SuccessProperty='Status';SuccessValue='PASS';RequiredCases=@()}
    )
}
function Assert-DesktopLifeComponentReport($Report,$Specification,[bool]$NestedTeaser=$false){
    $label=$Specification.Stage
    $property=if($NestedTeaser){'Status'}else{$Specification.SuccessProperty}
    $expected=if($NestedTeaser){'PASS'}else{$Specification.SuccessValue}
    if(($expected -is [bool] -and $Report.$property -isnot [bool]) -or $Report.$property -cne $expected){throw "$label component report did not pass."}
    if([string]::IsNullOrWhiteSpace($Report.Scope) -and [string]::IsNullOrWhiteSpace($Report.Gate)){throw "$label component report has no verification scope."}
    if($label -eq 'RuntimeDiagnostics'){
        foreach($flag in @('VisibleControls','BinaryCopyHashVerified','SecondJobRejected','ActiveReconnection','ParentOutputDisconnected','StopButtonRouted','CompletingWorkerStayedAlive','NoPrematurePassOrNotification','CompletingWorkerReconnected','ReopenedPersistentReport','IsolatedProfiles')){
            if($Report.$flag -isnot [bool] -or $Report.$flag -cne $true){throw "$label component report has missing lifecycle evidence: $flag"}
        }
        if($Report.ProductionProfileTouched -isnot [bool] -or $Report.ProductionProfileTouched -cne $false -or $Report.StoppedState -ne 'Stopped' -or $Report.CrashedState -notin @('Failed','Interrupted')){throw "$label component report has invalid isolation or terminal-state evidence."}
        $runIds=@($Report.StoppedRunId,$Report.PassedProtocolRunId,$Report.CrashedProtocolRunId)
        foreach($runId in $runIds){$parsed=[guid]::Empty;if(![guid]::TryParseExact([string]$runId,'N',[ref]$parsed)){throw "$label component report has an invalid protocol run identity."}}
        if(@($runIds|Select-Object -Unique).Count -ne 3){throw "$label component report reused a protocol run identity."}
        return
    }
    $cases=@($Report.Cases)
    if($cases.Count -eq 0){throw "$label component report has no case evidence."}
    $names=@($cases|ForEach-Object {
        if($_.Passed -isnot [bool] -or $_.Passed -cne $true -or [string]::IsNullOrWhiteSpace($_.Case)){throw "$label component report has an incomplete case."}
        [string]$_.Case
    })
    if(@($names|Select-Object -Unique).Count -ne $names.Count){throw "$label component report has duplicate cases."}
    $required=if($NestedTeaser){@('Cat-illustrated-physical-contact-and-real-support','BorderCollie-illustrated-physical-contact-and-real-support','Cat-unreachable-contact-rejected','BorderCollie-unreachable-contact-rejected','Cat-cancelled-string-restored','BorderCollie-cancelled-string-restored','girl-is-not-a-pet-toy-resident','paused-editing-and-other-resident-occupancy-rejected')}else{@($Specification.RequiredCases)}
    foreach($case in $required){if($names -cnotcontains $case){throw "$label component report is missing required case: $case"}}
}
function Assert-DesktopLifeComponentEvidence($BuildHashes,[string]$RunDirectory,$Specification){
    $label=$Specification.Stage;$directory=Join-Path $RunDirectory ('raw/'+$Specification.Directory)
    $summary=Read-DesktopLifeEvidence (Join-Path $directory 'component-summary.json')
    $binary=Read-DesktopLifeEvidence (Join-Path $directory 'component-binary.json')
    foreach($flag in @('Succeeded','NativeAppHost','IsolatedProfileVerified')){if($summary.$flag -isnot [bool] -or $summary.$flag -cne $true){throw "$label component execution evidence is incomplete: $flag"}}
    if($summary.Component -cne $label -or $summary.Argument -cne $Specification.Argument -or ($summary.ExitCode -isnot [int] -and $summary.ExitCode -isnot [long]) -or $summary.ExitCode -ne 0 -or $summary.TimedOut -isnot [bool] -or $summary.TimedOut -cne $false){throw "$label component execution evidence is incomplete."}
    if(($summary.ProcessId -isnot [int] -and $summary.ProcessId -isnot [long]) -or $summary.ProcessId -le 0){throw "$label component native process identity is missing."}
    $profile=[IO.Path]::GetFullPath([string]$summary.IsolatedProfile).TrimEnd([char[]]@('\','/'))
    $isolatedRoot=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'DesktopLifeSmoke')).TrimEnd([char[]]@('\','/'))
    $profileId=[guid]::Empty
    if(![guid]::TryParseExact([IO.Path]::GetFileName($profile),'N',[ref]$profileId) -or [IO.Path]::GetDirectoryName($profile) -ine $isolatedRoot){throw "$label component profile is not an isolated TEMP/GUID profile."}
    if($binary.Component -cne $label -or $binary.Argument -cne $Specification.Argument){throw "$label component binary manifest has the wrong request."}
    $snapshotRoot=[IO.Path]::GetFullPath($directory).TrimEnd([char[]]@('\','/'))+[IO.Path]::DirectorySeparatorChar
    if(![IO.Path]::IsPathRooted([string]$binary.Executable) -or ![IO.Path]::GetFullPath([string]$binary.Executable).StartsWith($snapshotRoot,[StringComparison]::OrdinalIgnoreCase)){throw "$label component native process executable is outside its snapshot."}
    $tested=@($binary.Files);$testedByName=@{}
    foreach($file in $tested){
        if(!$file.Name -or [IO.Path]::IsPathRooted($file.Name) -or $file.Name -match '(^|[\\/])\.\.([\\/]|$)' -or $file.Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or $testedByName.ContainsKey($file.Name)){throw "$label component binary manifest has an invalid path or hash."}
        $testedByName[$file.Name]=$file.Sha256
    }
    foreach($expectedFile in $BuildHashes){
        $name=[IO.Path]::GetFileName($expectedFile.Path)
        if($testedByName[$name] -ne $expectedFile.Sha256){throw "$label component tested different application binaries: $name"}
    }
    $marker=Read-DesktopLifeEvidence (Join-Path $directory 'isolated-run/diagnostic-process.json')
    if($marker.ProcessId -ne $summary.ProcessId -or @($marker.Arguments) -cnotcontains $Specification.Argument -or $marker.Executable -ine $binary.Executable){throw "$label component native process identity is inconsistent."}
    $hashes=@($summary.ReportHashes)
    if($hashes.Count -ne @($Specification.Reports).Count -or @($hashes.Name|Select-Object -Unique).Count -ne $hashes.Count){throw "$label component report inventory is incomplete."}
    for($index=0;$index -lt @($Specification.Reports).Count;$index++){
        $name=$Specification.Reports[$index];$path=Join-Path $directory ('isolated-run/'+$name)
        $record=@($hashes|Where-Object Name -CEQ $name)
        if($record.Count -ne 1 -or $record[0].Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or !(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $record[0].Sha256){throw "$label component report changed or is missing: $name"}
        Assert-DesktopLifeComponentReport (Read-DesktopLifeEvidence $path) $Specification ($index -gt 0)
    }
}
function Get-DesktopLife012Counter($Value,[string]$Name,[string]$Label,[long]$Minimum=0){
    $number=$Value.$Name
    if(($number -isnot [int] -and $number -isnot [long]) -or $number -lt $Minimum){throw "$Label 0.12 interaction evidence has a missing or invalid counter: $Name"}
    return [long]$number
}
function Assert-DesktopLife012StressFeatures($Features,[string]$Label){
    if(!$Features -or $Features.Version -cne '0.12' -or [string]::IsNullOrWhiteSpace($Features.Scope)){throw "$Label 0.12 interaction evidence is missing or has the wrong scope/version."}
    foreach($flag in @('CoverageComplete','AllResidentCoverage')){
        if($Features.$flag -isnot [bool] -or $Features.$flag -cne $true){throw "$Label 0.12 interaction evidence did not cover all residents: $flag"}
    }
    foreach($name in @('MissingCoverage','Violations')){
        if($Features.PSObject.Properties.Name -cnotcontains $name -or $Features.$name -isnot [array] -or @($Features.$name).Count -ne 0){throw "$Label 0.12 interaction evidence contains missing coverage or invariant violations."}
    }
    foreach($name in @('ViolationCount','PeriodicNeedResets','SyntheticRewardCalls')){
        if((Get-DesktopLife012Counter $Features $name $Label) -ne 0){throw "$Label 0.12 interaction evidence contains violations, periodic needs resets or synthetic rewards: $name"}
    }
    foreach($pair in @(@('InitialHunger',75),@('InitialHungerAssignmentsPerResident',1),@('InitialHungerAssignmentCount',3))){
        if((Get-DesktopLife012Counter $Features $pair[0] $Label) -ne $pair[1]){throw "$Label 0.12 interaction evidence has invalid initial-condition provenance: $($pair[0])"}
    }
    $stock=@($Features.InitialStock);$ids=@{};$surfaces=@{}
    if($stock.Count -lt 2){throw "$Label 0.12 interaction evidence lacks finite initial bowl/table stock."}
    foreach($item in $stock){
        $id=[guid]::Empty
        if(![guid]::TryParse([string]$item.Id,[ref]$id) -or $id -eq [guid]::Empty -or $ids.ContainsKey([string]$id) -or
            (Get-DesktopLife012Counter $item Portions $Label) -ne 3 -or
            !(($item.Furniture -ceq 'CatBowl' -and $item.Food -ceq 'SharedKibble') -or ($item.Furniture -ceq 'DiningTable' -and $item.Food -ceq 'Ramen'))){throw "$Label 0.12 interaction evidence has an invalid or duplicate finite initial serving."}
        $ids[[string]$id]=$true;$surfaces[$item.Furniture]=$true
    }
    if(!$surfaces.ContainsKey('CatBowl') -or !$surfaces.ContainsKey('DiningTable')){throw "$Label 0.12 interaction evidence did not exercise both food furniture kinds."}
    $names=@($Features.Residents.PSObject.Properties.Name|Sort-Object)
    if(($names -join ',') -cne 'BorderCollie,Cat,Girl'){throw "$Label 0.12 interaction evidence has missing or unexpected residents."}
    [long]$bowl=0;[long]$table=0;[long]$cancelled=0;[long]$probes=0
    foreach($kind in $names){
        $resident=$Features.Residents.$kind
        $bites=Get-DesktopLife012Counter $resident AcceptedBites $Label 1
        if((Get-DesktopLife012Counter $resident FoodContacts $Label 1) -ne $bites){throw "$Label 0.12 interaction evidence has mismatched actual food contacts for $kind"}
        foreach($name in @('AutomaticFoodStarts','PetContacts','GroomContacts','HoverAttempts','CombAttempts','HoverCancellations','CombCancellations')){[void](Get-DesktopLife012Counter $resident $name $Label 1)}
        foreach($name in @('TeaserRequests','TeaserStarts','TeaserContacts')){
            $count=Get-DesktopLife012Counter $resident $name $Label $(if($kind -ne 'Girl' -and $name -eq 'TeaserContacts'){1}else{0})
            if($kind -eq 'Girl' -and $count -ne 0){throw "$Label 0.12 interaction evidence incorrectly let the girl use a pet hanging toy."}
        }
        if($resident.TeaserStarts -gt $resident.TeaserRequests){throw "$Label 0.12 interaction evidence has unrequested hanging-toy starts."}
        [void](Get-DesktopLife012Counter $resident InputUnavailable $Label)
        $misses=Get-DesktopLife012Counter $resident InputMisses $Label
        $attempts=[long]$resident.HoverAttempts+[long]$resident.CombAttempts
        if($misses -gt $attempts -or $resident.HoverCancellations -gt $resident.HoverAttempts -or $resident.CombCancellations -gt $resident.CombAttempts){throw "$Label 0.12 interaction evidence has impossible contact/cancellation attempts."}
        [long]$skipped=0
        if($resident.PSObject.Properties.Name -cnotcontains 'Skips' -or !$resident.Skips){throw "$Label 0.12 interaction evidence has no input availability provenance."}
        foreach($reason in $resident.Skips.PSObject.Properties){
            if([string]::IsNullOrWhiteSpace($reason.Name)){throw "$Label 0.12 interaction evidence has an unnamed skip reason."}
            $skipped+=Get-DesktopLife012Counter $resident.Skips $reason.Name $Label 1
        }
        if($skipped -ne $resident.InputUnavailable){throw "$Label 0.12 interaction evidence has inconsistent input availability provenance."}
        $cancelled+=[long]$resident.HoverCancellations+[long]$resident.CombCancellations;$probes+=$attempts
        if($kind -eq 'Girl'){$table+=$bites}else{$bowl+=$bites}
    }
    if((Get-DesktopLife012Counter $Features BowlPortionsConsumed $Label 1) -ne $bowl -or
        (Get-DesktopLife012Counter $Features TablePortionsConsumed $Label 1) -ne $table -or
        (Get-DesktopLife012Counter $Features CancelledInputChecks $Label 1) -ne $cancelled -or
        (Get-DesktopLife012Counter $Features InputProbeCount $Label 1) -ne $probes){throw "$Label 0.12 interaction evidence totals do not match resident transactions and input probes."}
    foreach($name in @('RefillRequests','AcceptedRefills','RefillsAfterConsumption','EmptyRefills')){[void](Get-DesktopLife012Counter $Features $name $Label 1)}
    if($Features.AcceptedRefills -gt $Features.RefillRequests -or $Features.RefillsAfterConsumption -gt $Features.AcceptedRefills -or $Features.EmptyRefills -gt $Features.RefillsAfterConsumption){throw "$Label 0.12 interaction evidence has impossible finite refill counts."}
    foreach($name in @('ElevatedInputSeconds','MaximumElevatedInputSeconds')){
        $seconds=$Features.$name
        if(($seconds -isnot [int] -and $seconds -isnot [long] -and $seconds -isnot [double] -and $seconds -isnot [decimal]) -or
            [double]::IsNaN([double]$seconds) -or [double]::IsInfinity([double]$seconds) -or $seconds -le 0){throw "$Label 0.12 interaction evidence has invalid input duration: $name"}
    }
    if($Features.MaximumElevatedInputSeconds -gt $Features.ElevatedInputSeconds){throw "$Label 0.12 interaction evidence has an impossible input duration total."}
}
function Assert-DesktopLifeStressEvidence($BuildHashes,[string]$RunDirectory,[ValidateSet('short','soak')][string]$Label,[Parameter(Mandatory=$true)][string]$Version){
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
    if($Version -match '^0\.12\.'){
        if($stress.Stopped -isnot [bool] -or $stress.Stopped -cne $false -or $stress.CompletedWorkload -isnot [bool] -or $stress.CompletedWorkload -cne $true){throw "$Label 0.12 interaction workload was stopped or incomplete."}
        Assert-DesktopLife012StressFeatures $stress.Features012 $Label
    }
}
function Assert-DesktopLifeVerificationEvidence($Summary,[string]$ProjectRoot,[string]$RunDirectory,[ValidateSet('Standard','Full')][string]$Mode){
    $version=Get-DesktopLifeReleaseVersion $ProjectRoot
    $requiredMode=Get-DesktopLifeReleaseVerificationMode $version
    if(!$Mode){$Mode=$requiredMode}
    if($requiredMode -eq 'Full' -and $Mode -ne 'Full'){throw "The $version initial minor release requires Full verification."}
    if($Summary.Version -ne $version -or $Summary.Conclusion -ne 'PASS' -or $Summary.Mode -ne $Mode){throw "A passing $version $Mode verification is required."}
    if($Summary.SourceSealed -ne $true){throw 'Verification did not seal its source and build after testing.'}
    $required=@('Build','Tests','Analyzer','Deployment','WpfSmoke','HouseMixedDpi','Identity','Longitudinal','ShortStress','DualPresence')
    $components=@(Get-DesktopLifeComponentSpecifications $version)
    $required+=@($components|ForEach-Object Stage)
    if($Mode -eq 'Full'){$required+='Soak'}
    foreach($stage in $required){if($Summary.Stages.$stage -ne 'PASS'){throw "Required verification stage did not pass: $stage"}}
    if([int]$Summary.Tests.passed -lt 1 -or [int]$Summary.Tests.failed -ne 0){throw 'Passing unit-test evidence is missing.'}
    Assert-DesktopLifeSourceInventory $Summary.SourceHashes $ProjectRoot
    Assert-DesktopLifeBuildInventory $Summary.BuildHashes $ProjectRoot
    foreach($component in $components){Assert-DesktopLifeComponentEvidence $Summary.BuildHashes $RunDirectory $component}
    $labels=if($Mode -eq 'Full'){@('short','soak')}else{@('short')}
    foreach($label in $labels){
        Assert-DesktopLifeStressEvidence $Summary.BuildHashes $RunDirectory $label $version
    }
    $house=Read-DesktopLifeEvidence (Join-Path $RunDirectory 'raw/house-mixed-dpi/isolated-run/house-summary.json')
    $displays=Read-DesktopLifeEvidence (Join-Path $RunDirectory 'raw/house-mixed-dpi/isolated-run/display-report.json')
    if($house.Succeeded -ne $true -or $displays.Succeeded -ne $true -or @($displays.VisitedDisplays).Count -lt 3){throw 'Native house/display verification evidence is incomplete.'}
    foreach($group in @($displays.VisitedDisplays|Group-Object Id)){
        if((@($group.Group|ForEach-Object Floors|Sort-Object -Unique) -join ',') -ne '1,2,3'){throw 'Native display verification did not cover each fixed floor preset.'}
    }
}
