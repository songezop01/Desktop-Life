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
    'mismatched-file-version'='FileVersion must match';'invalid-release-version'='stable major.minor.patch'
}
function Write-TestJson([string]$Path,$Value){New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent)|Out-Null;$Value|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $Path -Encoding UTF8}
function New-GateFixture([string]$Name,[string]$Version='0.11.0',[string]$Mode='Full'){
    $project=Join-Path $OutputDirectory ($Name+'/project');$run=Join-Path $OutputDirectory ($Name+'/run')
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
        Write-TestJson (Join-Path $directory 'stress-report.json') $stress
        Write-TestJson (Join-Path $directory 'analysis.json') @{Conclusion='PASS'}
        Write-TestJson (Join-Path $directory 'restart-report.json') @{Succeeded=$true}
        Write-TestJson (Join-Path $directory 'stress-binary.json') @{AppAssemblySha256=($summary.BuildHashes|Where-Object Path -like '*DesktopLife.App.dll').Sha256;CoreAssemblySha256=($summary.BuildHashes|Where-Object Path -like '*DesktopLife.Core.dll').Sha256}
    }
    Write-TestJson (Join-Path $run 'raw/house-mixed-dpi/isolated-run/house-summary.json') @{Succeeded=$true}
    Write-TestJson (Join-Path $run 'raw/house-mixed-dpi/isolated-run/display-report.json') @{Succeeded=$true;VisitedDisplays=@(@{Id='primary';Floors=1},@{Id='primary';Floors=2},@{Id='primary';Floors=3},@{Id='spacedesk';Floors=1},@{Id='spacedesk';Floors=2},@{Id='spacedesk';Floors=3})}
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
    $results.Add([ordered]@{Name=$Name;Passed=$pass;ExpectedPass=$ExpectPass;Rejection=$caught})
    if($pass){Write-Output ('PASS '+$Name)}else{Write-Output ('FAIL '+$Name+': '+$caught)}
}
function Edit-Stress($Fixture,[string]$Label,[scriptblock]$Change){
    $path=Join-Path $Fixture.Run ('raw/'+$Label+'/stress-report.json');$value=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json;& $Change $value;Write-TestJson $path $value
}
Invoke-GateCase 'valid-full' {} $true
Invoke-GateCase 'valid-patch-standard-without-soak' {} $true '0.11.1' 'Standard'
Invoke-GateCase 'valid-next-minor-full' {} $true '0.12.0' 'Full'
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
