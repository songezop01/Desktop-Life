param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/release-verification.ps1"
$repository=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory){$OutputDirectory=Join-Path $repository ('artifacts/verification/'+(Get-DesktopLifeReleaseVersion $repository)+'/release-gates-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if(![IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path $repository $OutputDirectory}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new isolated release-gate test output directory.'}
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
$results=[Collections.Generic.List[object]]::new()
$expectedRejections=@{
    'missing-house-stage'='HouseMixedDpi';'unsealed-source'='seal';'shorter-short-test'='duration';'shorter-soak'='duration';
    'wrong-presence'='all three';'wrong-floor-count'='all three';'unexpected-navigation'='reliability counter';'house-route-failure'='reliability counter';
    'approach-timeout'='ApproachTimeouts';'missing-approach-counter'='ApproachTimeouts';'malformed-approach-counter'='ApproachTimeouts';
    'no-stair-workload'='stair traversal';'restart-failed'='restart';'analysis-review'='analysis';'source-edited-after-test'='Source changed';
    'source-added-after-test'='Source inventory changed';'duplicate-source-path'='Source inventory changed';'source-path-escape'='invalid path';
    'build-edited-after-test'='Build changed';'stress-tested-different-binary'='different application binaries';'native-presets-incomplete'='fixed floor preset';
    'patch-wrong-version'='passing 0.11.1 Standard';'patch-wrong-mode'='passing 0.11.1 Standard';'patch-stopped-run'='passing 0.11.1 Standard';
    'patch-shorter-short-test'='duration';'patch-stale-build'='Build changed';'initial-minor-standard'='initial minor release requires Full';
    'initial-minor-explicit-standard'='initial minor release requires Full';'next-minor-standard'='initial minor release requires Full';
    'mismatched-file-version'='FileVersion must match';'invalid-release-version'='stable major.minor.patch';
    '012-missing-food-stage'='FoodInteraction';'012-missing-motion-stage'='HouseMotion';'012-missing-component-report'='changed or is missing';
    '012-failed-component-report'='did not pass';'012-component-wrong-binary'='different application binaries';'012-component-wrong-argument'='execution evidence';
    '012-component-nonzero-exit'='execution evidence';'012-component-unisolated-profile'='isolated TEMP';'012-component-wrong-process'='process identity';
    '012-component-edited-report'='changed or is missing';'012-direct-missing-teaser'='changed or is missing';'012-direct-incomplete-comb'='missing required case';
    '012-runtime-no-stop-evidence'='missing lifecycle evidence';'012-runtime-false-success'='invalid isolation or terminal-state';
    '012-motion-empty-cases'='no case evidence';'012-component-malformed-success'='did not pass';'012-component-duplicate-report-hash'='report inventory'
}
foreach($name in @('012-stress-missing-features','012-soak-missing-features','012-stress-wrong-feature-version','012-stress-incomplete','012-stress-stopped',
    '012-stress-coverage-string','012-stress-missing-coverage-list','012-stress-listed-missing-coverage','012-stress-listed-violation','012-stress-periodic-reset',
    '012-stress-synthetic-reward','012-stress-no-auto-food','012-stress-no-hover','012-stress-no-comb','012-stress-no-cat-teaser','012-stress-girl-teaser',
    '012-stress-food-counter-mismatch','012-stress-cancellation-missing','012-stress-no-empty-refill','012-stress-invalid-initial-stock',
    '012-stress-duplicate-initial-stock','012-stress-no-initial-table','012-stress-malformed-counter','012-stress-missing-resident',
    '012-stress-unexpected-resident','012-stress-bowl-total-mismatch','012-stress-cancellation-total-mismatch','012-stress-probe-total-mismatch',
    '012-stress-impossible-refills','012-stress-invalid-input-duration','012-stress-skip-provenance-mismatch','012-stress-initial-provenance-reset',
    '012-stress-missing-skip-provenance')){$expectedRejections[$name]='0\.12 interaction'}
function Write-TestJson([string]$Path,$Value){New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent)|Out-Null;$Value|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $Path -Encoding UTF8}
function New-Feature012Fixture{
    $residents=[ordered]@{}
    foreach($kind in @('Cat','Girl','BorderCollie')){
        $petToy=if($kind -eq 'Girl'){0}else{1}
        $residents[$kind]=[ordered]@{AcceptedBites=1;FoodContacts=1;AutomaticFoodStarts=1;PetContacts=1;GroomContacts=1;
            HoverAttempts=1;CombAttempts=1;HoverCancellations=1;CombCancellations=1;TeaserRequests=$petToy;TeaserStarts=$petToy;TeaserContacts=$petToy;
            InputUnavailable=0;InputMisses=0;Skips=@{}}
    }
    [ordered]@{Version='0.12';Scope='Synthetic gate fixture, not an elapsed workload.';CoverageComplete=$true;AllResidentCoverage=$true;
        MissingCoverage=@();Violations=@();ViolationCount=0;PeriodicNeedResets=0;SyntheticRewardCalls=0;InitialHunger=75;
        InitialHungerAssignmentsPerResident=1;InitialHungerAssignmentCount=3;
        InitialStock=@(@{Id=[guid]::NewGuid();Furniture='CatBowl';Food='SharedKibble';Portions=3},@{Id=[guid]::NewGuid();Furniture='DiningTable';Food='Ramen';Portions=3});
        BowlPortionsConsumed=2;TablePortionsConsumed=1;RefillRequests=2;AcceptedRefills=2;RefillsAfterConsumption=2;EmptyRefills=1;
        CancelledInputChecks=6;InputProbeCount=6;ElevatedInputSeconds=3.6;MaximumElevatedInputSeconds=.6;Residents=$residents}
}
function New-GateFixture([string]$Name,[string]$Version='0.11.0',[string]$Mode='Full'){
    # Keep fixture paths short enough for the Windows PowerShell 5.1 worker in
    # managed worktrees. Human-readable case names remain in the result JSON.
    $caseDirectory=Join-Path $OutputDirectory ('case-'+$results.Count.ToString('D3'))
    $project=Join-Path $caseDirectory 'p';$run=Join-Path $caseDirectory 'r'
    foreach($directory in @('src','tests','scripts','src/DesktopLife.App/bin/Release/net10.0-windows')){New-Item -ItemType Directory -Force -Path (Join-Path $project $directory)|Out-Null}
    'source-v1'|Set-Content -LiteralPath (Join-Path $project 'src/main.cs') -Encoding UTF8
    "<Project><PropertyGroup><Version>$Version</Version><AssemblyVersion>$Version.0</AssemblyVersion><FileVersion>$Version.0</FileVersion></PropertyGroup></Project>"|Set-Content -LiteralPath (Join-Path $project 'src/DesktopLife.App/DesktopLife.App.csproj') -Encoding UTF8
    foreach($name in @('DesktopLife.App.exe','DesktopLife.App.dll','DesktopLife.Core.dll','DesktopLife.Windows.dll','DesktopLife.App.deps.json','DesktopLife.App.runtimeconfig.json')){
        $name|Set-Content -LiteralPath (Join-Path $project ('src/DesktopLife.App/bin/Release/net10.0-windows/'+$name)) -Encoding UTF8
    }
    $stages=[ordered]@{};foreach($name in @('Build','Tests','Analyzer','Deployment','WpfSmoke','HouseMixedDpi','Identity','Longitudinal','ShortStress','Soak','DualPresence')){$stages[$name]='PASS'}
    if($Mode -eq 'Standard'){$stages.Soak='NOT RUN'}
    $summary=[ordered]@{Version=$Version;Mode=$Mode;Conclusion='PASS';SourceSealed=$true;Stages=$stages;Tests=@{passed=1;failed=0};SourceHashes=@(Get-DesktopLifeSourceInventory $project);BuildHashes=@(Get-DesktopLifeBuildInventory $project)}
    $labels=if($Mode -eq 'Full'){@('short','soak')}else{@('short')}
    foreach($label in $labels){
        $seconds=if($label -eq 'short'){600}else{7200};$directory=Join-Path $run ('raw/'+$label)
        $stress=[ordered]@{RequestedSeconds=$seconds;Seconds=$seconds+1;Presence='All';Floors=3;Characters=@('Cat','Girl','BorderCollie');Exceptions=0;StuckSequences=0;InvalidFurnitureInteractions=0;HouseRouteFailures=0;ApproachTimeouts=0;NavigationRecoveryTimeouts=0;UnexpectedNavigationRecoveryAttempts=0;StairTrips=3;DurableReloadVerification=@{Succeeded=$true}}
        if($Version -match '^0\.12\.'){$stress.Features012=New-Feature012Fixture;$stress.Stopped=$false;$stress.CompletedWorkload=$true}
        Write-TestJson (Join-Path $directory 'stress-report.json') $stress
        Write-TestJson (Join-Path $directory 'analysis.json') @{Conclusion='PASS'}
        Write-TestJson (Join-Path $directory 'restart-report.json') @{Succeeded=$true}
        Write-TestJson (Join-Path $directory 'stress-binary.json') @{AppAssemblySha256=($summary.BuildHashes|Where-Object Path -like '*DesktopLife.App.dll').Sha256;CoreAssemblySha256=($summary.BuildHashes|Where-Object Path -like '*DesktopLife.Core.dll').Sha256}
    }
    Write-TestJson (Join-Path $run 'raw/house-mixed-dpi/isolated-run/house-summary.json') @{Succeeded=$true}
    Write-TestJson (Join-Path $run 'raw/house-mixed-dpi/isolated-run/display-report.json') @{Succeeded=$true;VisitedDisplays=@(@{Id='primary';Floors=1},@{Id='primary';Floors=2},@{Id='primary';Floors=3},@{Id='spacedesk';Floors=1},@{Id='spacedesk';Floors=2},@{Id='spacedesk';Floors=3})}
    foreach($component in @(Get-DesktopLifeComponentSpecifications $Version)){
        $stages[$component.Stage]='PASS'
        $directory=Join-Path $run ('raw/'+$component.Directory)
        $executable=Join-Path $directory 'binary/DesktopLife.App.exe';$profile=Join-Path ([IO.Path]::GetTempPath()) ('DesktopLifeSmoke/'+[guid]::NewGuid().ToString('N'))
        $reports=@(for($index=0;$index -lt @($component.Reports).Count;$index++){
            $name=$component.Reports[$index]
            $report=[ordered]@{Scope='Isolated release gate fixture, not an executed application workload.'}
            if($component.Stage -eq 'RuntimeDiagnostics'){
                $report.Succeeded=$true
                foreach($flag in @('VisibleControls','BinaryCopyHashVerified','SecondJobRejected','ActiveReconnection','ParentOutputDisconnected','StopButtonRouted','CompletingWorkerStayedAlive','NoPrematurePassOrNotification','CompletingWorkerReconnected','ReopenedPersistentReport','IsolatedProfiles')){$report[$flag]=$true}
                $report.ProductionProfileTouched=$false;$report.StoppedState='Stopped';$report.CrashedState='Interrupted'
                $report.StoppedRunId=[guid]::NewGuid().ToString('N');$report.PassedProtocolRunId=[guid]::NewGuid().ToString('N');$report.CrashedProtocolRunId=[guid]::NewGuid().ToString('N')
            }else{
                $report[$component.SuccessProperty]=$component.SuccessValue
                $names=if($index -gt 0){@('Cat-illustrated-physical-contact-and-real-support','BorderCollie-illustrated-physical-contact-and-real-support','Cat-unreachable-contact-rejected','BorderCollie-unreachable-contact-rejected','Cat-cancelled-string-restored','BorderCollie-cancelled-string-restored','girl-is-not-a-pet-toy-resident','paused-editing-and-other-resident-occupancy-rejected')}elseif(@($component.RequiredCases).Count){@($component.RequiredCases)}else{@('fixture-motion-contact-case')}
                $report.Cases=@($names|ForEach-Object {@{Case=$_;Passed=$true}})
            }
            $path=Join-Path $directory ('isolated-run/'+$name);Write-TestJson $path $report
            [ordered]@{Name=$name;Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
        })
        Write-TestJson (Join-Path $directory 'component-binary.json') @{Component=$component.Stage;Argument=$component.Argument;Executable=$executable;Files=@($summary.BuildHashes|ForEach-Object {@{Name=[IO.Path]::GetFileName($_.Path);Sha256=$_.Sha256}})}
        Write-TestJson (Join-Path $directory 'isolated-run/diagnostic-process.json') @{ProcessId=123;Arguments=@($component.Argument);Executable=$executable}
        Write-TestJson (Join-Path $directory 'component-summary.json') @{Succeeded=$true;Component=$component.Stage;Argument=$component.Argument;ExitCode=0;TimedOut=$false;NativeAppHost=$true;IsolatedProfileVerified=$true;IsolatedProfile=$profile;ProcessId=123;ReportHashes=$reports}
    }
    [pscustomobject]@{Project=$project;Run=$run;Summary=$summary}
}
function Invoke-GateCase([string]$Name,[scriptblock]$Mutation,[bool]$ExpectPass=$false,[string]$Version='0.11.0',[string]$Mode='Full',[string]$RequestedMode){
    $fixture=New-GateFixture $Name $Version $Mode;$caught=$null
    try{
        & $Mutation $fixture
        if($RequestedMode){Assert-DesktopLifeVerificationEvidence $fixture.Summary $fixture.Project $fixture.Run $RequestedMode}
        else{Assert-DesktopLifeVerificationEvidence $fixture.Summary $fixture.Project $fixture.Run}
    }catch{$caught=$_.Exception.Message}
    $pass=($ExpectPass -and !$caught) -or (!$ExpectPass -and [bool]$caught -and $caught -match $expectedRejections[$Name])
    $results.Add([ordered]@{Name=$Name;Passed=$pass;ExpectedPass=$ExpectPass;Rejection=$caught;FixtureDirectory=(Split-Path $fixture.Project -Parent)})
    if($pass){Write-Output ('PASS '+$Name)}else{Write-Output ('FAIL '+$Name+': '+$caught)}
}
function Edit-Stress($Fixture,[string]$Label,[scriptblock]$Change){
    $path=Join-Path $Fixture.Run ('raw/'+$Label+'/stress-report.json');$value=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json;& $Change $value;Write-TestJson $path $value
}
function Edit-Component($Fixture,[string]$Stage,[string]$Name,[scriptblock]$Change,[switch]$Reseal){
    $specification=Get-DesktopLifeComponentSpecifications '0.12.0'|Where-Object Stage -CEQ $Stage
    $directory=Join-Path $Fixture.Run ('raw/'+$specification.Directory);$path=Join-Path $directory $Name
    $value=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json;& $Change $value;Write-TestJson $path $value
    if($Reseal){
        $summaryPath=Join-Path $directory 'component-summary.json';$summary=Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8|ConvertFrom-Json
        $reportName=[IO.Path]::GetFileName($Name)
        ($summary.ReportHashes|Where-Object Name -CEQ $reportName).Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        Write-TestJson $summaryPath $summary
    }
}
Invoke-GateCase 'valid-full' {} $true
Invoke-GateCase 'valid-patch-standard-without-soak' {} $true '0.11.1' 'Standard'
Invoke-GateCase 'valid-next-minor-full' {} $true '0.12.0' 'Full'
Invoke-GateCase 'valid-012-patch-standard-with-components' {} $true '0.12.1' 'Standard'
Invoke-GateCase '012-missing-food-stage' {param($f)$f.Summary.Stages.FoodInteraction='NOT RUN'} $false '0.12.0' 'Full'
Invoke-GateCase '012-missing-motion-stage' {param($f)$f.Summary.Stages.HouseMotion='NOT RUN'} $false '0.12.0' 'Full'
Invoke-GateCase '012-missing-component-report' {param($f)Remove-Item -LiteralPath (Join-Path $f.Run 'raw/food-interaction/isolated-run/food-interaction-checks.json')} $false '0.12.0' 'Full'
Invoke-GateCase '012-failed-component-report' {param($f)Edit-Component $f FoodInteraction 'isolated-run/food-interaction-checks.json' {param($r)$r.Passed=$false} -Reseal} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-wrong-binary' {param($f)Edit-Component $f FoodInteraction 'component-binary.json' {param($r)$r.Files[0].Sha256=('A'*64)}} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-wrong-argument' {param($f)Edit-Component $f FoodInteraction 'component-summary.json' {param($r)$r.Argument='--smoke-test'}} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-nonzero-exit' {param($f)Edit-Component $f DirectInteraction 'component-summary.json' {param($r)$r.ExitCode=2}} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-unisolated-profile' {param($f)Edit-Component $f FoodInteraction 'component-summary.json' {param($r)$r.IsolatedProfile=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DesktopLife'}} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-wrong-process' {param($f)Edit-Component $f FoodInteraction 'isolated-run/diagnostic-process.json' {param($r)$r.ProcessId=456}} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-edited-report' {param($f)Edit-Component $f FoodInteraction 'isolated-run/food-interaction-checks.json' {param($r)$r.Passed=$false}} $false '0.12.0' 'Full'
Invoke-GateCase '012-direct-missing-teaser' {param($f)Remove-Item -LiteralPath (Join-Path $f.Run 'raw/direct-interaction/isolated-run/shared-teaser-report.json')} $false '0.12.0' 'Full'
Invoke-GateCase '012-direct-incomplete-comb' {param($f)Edit-Component $f DirectInteraction 'isolated-run/direct-care-report.json' {param($r)$r.Cases=@($r.Cases|Where-Object Case -CNE 'Girl-actual-comb-contact')} -Reseal} $false '0.12.0' 'Full'
Invoke-GateCase '012-runtime-no-stop-evidence' {param($f)Edit-Component $f RuntimeDiagnostics 'isolated-run/runtime-diagnostics-protocol.json' {param($r)$r.StopButtonRouted=$false} -Reseal} $false '0.12.0' 'Full'
Invoke-GateCase '012-runtime-false-success' {param($f)Edit-Component $f RuntimeDiagnostics 'isolated-run/runtime-diagnostics-protocol.json' {param($r)$r.StoppedState='Passed'} -Reseal} $false '0.12.0' 'Full'
Invoke-GateCase '012-motion-empty-cases' {param($f)Edit-Component $f HouseMotion 'isolated-run/house-motion-report.json' {param($r)$r.Cases=@()} -Reseal} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-malformed-success' {param($f)Edit-Component $f FoodInteraction 'isolated-run/food-interaction-checks.json' {param($r)$r.Passed='True'} -Reseal} $false '0.12.0' 'Full'
Invoke-GateCase '012-component-duplicate-report-hash' {param($f)Edit-Component $f DirectInteraction 'component-summary.json' {param($r)$r.ReportHashes[1].Name=$r.ReportHashes[0].Name}} $false '0.12.0' 'Full'
Invoke-GateCase '012-stress-missing-features' {param($f)Edit-Stress $f short {param($s)$s.PSObject.Properties.Remove('Features012')}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-soak-missing-features' {param($f)Edit-Stress $f soak {param($s)$s.PSObject.Properties.Remove('Features012')}} $false '0.12.0' 'Full'
Invoke-GateCase '012-stress-wrong-feature-version' {param($f)Edit-Stress $f short {param($s)$s.Features012.Version='0.11'}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-incomplete' {param($f)Edit-Stress $f short {param($s)$s.CompletedWorkload=$false}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-stopped' {param($f)Edit-Stress $f short {param($s)$s.Stopped=$true}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-coverage-string' {param($f)Edit-Stress $f short {param($s)$s.Features012.CoverageComplete='true'}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-missing-coverage-list' {param($f)Edit-Stress $f short {param($s)$s.Features012.PSObject.Properties.Remove('MissingCoverage')}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-listed-missing-coverage' {param($f)Edit-Stress $f short {param($s)$s.Features012.MissingCoverage=@('cat-comb')}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-listed-violation' {param($f)Edit-Stress $f short {param($s)$s.Features012.Violations=@('unsupported-bite')}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-periodic-reset' {param($f)Edit-Stress $f short {param($s)$s.Features012.PeriodicNeedResets=1}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-synthetic-reward' {param($f)Edit-Stress $f short {param($s)$s.Features012.SyntheticRewardCalls=1}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-no-auto-food' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Girl.AutomaticFoodStarts=0}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-no-hover' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Cat.PetContacts=0}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-no-comb' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.BorderCollie.GroomContacts=0}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-no-cat-teaser' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Cat.TeaserContacts=0}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-girl-teaser' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Girl.TeaserContacts=1}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-food-counter-mismatch' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Cat.FoodContacts=2}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-cancellation-missing' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Girl.CombCancellations=0}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-no-empty-refill' {param($f)Edit-Stress $f short {param($s)$s.Features012.EmptyRefills=0}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-invalid-initial-stock' {param($f)Edit-Stress $f short {param($s)$s.Features012.InitialStock[0].Portions='3'}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-duplicate-initial-stock' {param($f)Edit-Stress $f short {param($s)$s.Features012.InitialStock[1].Id=$s.Features012.InitialStock[0].Id}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-no-initial-table' {param($f)Edit-Stress $f short {param($s)$s.Features012.InitialStock[1].Furniture='CatBowl';$s.Features012.InitialStock[1].Food='SharedKibble'}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-malformed-counter' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Cat.AcceptedBites=$true}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-missing-resident' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.PSObject.Properties.Remove('Girl')}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-unexpected-resident' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents|Add-Member -NotePropertyName Other -NotePropertyValue @{}}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-bowl-total-mismatch' {param($f)Edit-Stress $f short {param($s)$s.Features012.BowlPortionsConsumed=3}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-cancellation-total-mismatch' {param($f)Edit-Stress $f short {param($s)$s.Features012.CancelledInputChecks=7}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-probe-total-mismatch' {param($f)Edit-Stress $f short {param($s)$s.Features012.InputProbeCount=7}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-impossible-refills' {param($f)Edit-Stress $f short {param($s)$s.Features012.AcceptedRefills=3}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-invalid-input-duration' {param($f)Edit-Stress $f short {param($s)$s.Features012.ElevatedInputSeconds='3.6'}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-skip-provenance-mismatch' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Cat.Skips|Add-Member -NotePropertyName Busy -NotePropertyValue 1}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-initial-provenance-reset' {param($f)Edit-Stress $f short {param($s)$s.Features012.InitialHungerAssignmentsPerResident=2}} $false '0.12.1' 'Standard'
Invoke-GateCase '012-stress-missing-skip-provenance' {param($f)Edit-Stress $f short {param($s)$s.Features012.Residents.Girl.PSObject.Properties.Remove('Skips')}} $false '0.12.1' 'Standard'
Invoke-GateCase 'patch-wrong-version' {param($f)$f.Summary.Version='0.11.0'} $false '0.11.1' 'Standard'
Invoke-GateCase 'patch-wrong-mode' {param($f)$f.Summary.Mode='Full'} $false '0.11.1' 'Standard'
Invoke-GateCase 'patch-stopped-run' {param($f)$f.Summary.Conclusion='STOPPED'} $false '0.11.1' 'Standard'
Invoke-GateCase 'patch-shorter-short-test' {param($f)Edit-Stress $f short {param($s)$s.RequestedSeconds=599}} $false '0.11.1' 'Standard'
Invoke-GateCase 'patch-stale-build' {param($f)'changed'|Set-Content -LiteralPath (Join-Path $f.Project 'src/DesktopLife.App/bin/Release/net10.0-windows/DesktopLife.Core.dll')} $false '0.11.1' 'Standard'
Invoke-GateCase 'initial-minor-standard' {} $false '0.11.0' 'Standard' 'Standard'
Invoke-GateCase 'initial-minor-explicit-standard' {} $false '0.11.0' 'Full' 'Standard'
Invoke-GateCase 'next-minor-standard' {} $false '0.12.0' 'Standard' 'Standard'
Invoke-GateCase 'mismatched-file-version' {param($f)$path=Join-Path $f.Project 'src/DesktopLife.App/DesktopLife.App.csproj';(Get-Content -LiteralPath $path -Raw).Replace('<FileVersion>0.11.0.0</FileVersion>','<FileVersion>0.10.1.0</FileVersion>')|Set-Content -LiteralPath $path}
Invoke-GateCase 'invalid-release-version' {param($f)$path=Join-Path $f.Project 'src/DesktopLife.App/DesktopLife.App.csproj';(Get-Content -LiteralPath $path -Raw).Replace('<Version>0.11.0</Version>','<Version>0.11.1-preview</Version>')|Set-Content -LiteralPath $path}
Invoke-GateCase 'missing-house-stage' {param($f)$f.Summary.Stages.HouseMixedDpi='NOT RUN'}
Invoke-GateCase 'unsealed-source' {param($f)$f.Summary.SourceSealed=$false}
Invoke-GateCase 'shorter-short-test' {param($f)Edit-Stress $f short {param($s)$s.RequestedSeconds=599}}
Invoke-GateCase 'shorter-soak' {param($f)Edit-Stress $f soak {param($s)$s.RequestedSeconds=7199}}
Invoke-GateCase 'wrong-presence' {param($f)Edit-Stress $f soak {param($s)$s.Presence='Both'}}
Invoke-GateCase 'wrong-floor-count' {param($f)Edit-Stress $f short {param($s)$s.Floors=1}}
Invoke-GateCase 'unexpected-navigation' {param($f)Edit-Stress $f short {param($s)$s.UnexpectedNavigationRecoveryAttempts=1}}
Invoke-GateCase 'house-route-failure' {param($f)Edit-Stress $f soak {param($s)$s.HouseRouteFailures=1}}
Invoke-GateCase 'approach-timeout' {param($f)Edit-Stress $f short {param($s)$s.ApproachTimeouts=1}}
Invoke-GateCase 'missing-approach-counter' {param($f)Edit-Stress $f soak {param($s)$s.PSObject.Properties.Remove('ApproachTimeouts')}}
Invoke-GateCase 'malformed-approach-counter' {param($f)Edit-Stress $f short {param($s)$s.ApproachTimeouts=$false}}
Invoke-GateCase 'no-stair-workload' {param($f)Edit-Stress $f short {param($s)$s.StairTrips=0}}
Invoke-GateCase 'restart-failed' {param($f)Write-TestJson (Join-Path $f.Run 'raw/soak/restart-report.json') @{Succeeded=$false}}
Invoke-GateCase 'analysis-review' {param($f)Write-TestJson (Join-Path $f.Run 'raw/short/analysis.json') @{Conclusion='REVIEW'}}
Invoke-GateCase 'source-edited-after-test' {param($f)'changed'|Set-Content -LiteralPath (Join-Path $f.Project 'src/main.cs')}
Invoke-GateCase 'source-added-after-test' {param($f)'added'|Set-Content -LiteralPath (Join-Path $f.Project 'src/new.cs')}
Invoke-GateCase 'duplicate-source-path' {param($f)$f.Summary.SourceHashes+=@($f.Summary.SourceHashes[0])}
Invoke-GateCase 'source-path-escape' {param($f)$f.Summary.SourceHashes[0].Path='../outside.cs'}
Invoke-GateCase 'build-edited-after-test' {param($f)'changed'|Set-Content -LiteralPath (Join-Path $f.Project 'src/DesktopLife.App/bin/Release/net10.0-windows/DesktopLife.Core.dll')}
Invoke-GateCase 'stress-tested-different-binary' {param($f)Write-TestJson (Join-Path $f.Run 'raw/short/stress-binary.json') @{AppAssemblySha256=('A'*64);CoreAssemblySha256=('B'*64)}}
Invoke-GateCase 'native-presets-incomplete' {param($f)Write-TestJson (Join-Path $f.Run 'raw/house-mixed-dpi/isolated-run/display-report.json') @{Succeeded=$true;VisitedDisplays=@(@{Id='primary';Floors=1},@{Id='primary';Floors=3},@{Id='spacedesk';Floors=3})}}
$failed=@($results|Where-Object Passed -eq $false)
Write-TestJson (Join-Path $OutputDirectory 'release-gate-tests.json') @{Succeeded=$failed.Count -eq 0;Cases=$results.Count;Failed=$failed.Count;Results=$results.ToArray()}
if($failed.Count){throw ('Release verification regressions failed: '+$failed.Count)}
