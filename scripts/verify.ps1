param(
    [ValidateSet('Quick','Standard','Full')][string]$Mode='Quick',
    [ValidateRange(7200,28800)][int]$SoakSeconds=7200,
    [switch]$Worker,
    [string]$RunDirectory
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/release-verification.ps1"
$version=Get-DesktopLifeReleaseVersion $root
$components=@(Get-DesktopLifeComponentSpecifications $version)
$base=Join-Path $root 'artifacts/verification'
if(!$Worker){
    $RunDirectory=Join-Path $base ('runs/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Force $RunDirectory | Out-Null
    if($Mode -ne 'Quick'){
        $runtimePaths=[ordered]@{}
        foreach($name in @('git','python')){
            $resolved=Get-Command $name -ErrorAction SilentlyContinue
            if($resolved){$runtimePaths[$name]=$resolved.Source}
        }
        $runtimePaths | ConvertTo-Json | Set-Content (Join-Path $RunDirectory 'runtime-paths.json') -Encoding UTF8
        $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
        $command='powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -Worker -Mode '+$Mode+' -SoakSeconds '+$SoakSeconds+' -RunDirectory "'+$RunDirectory+'"'
        $result=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=$root;ProcessStartupInformation=$startup}
        if($result.ReturnValue -ne 0){throw "Worker launch failed: $($result.ReturnValue)"}
        [ordered]@{ProcessId=$result.ProcessId;Mode=$Mode;Directory=$RunDirectory;StartedUtc=[DateTime]::UtcNow} | ConvertTo-Json | Set-Content (Join-Path $RunDirectory 'launcher.json') -Encoding UTF8
        Write-Output "Verification started independently. PID: $($result.ProcessId). Mode: $Mode. Expected: $(if($Mode -eq 'Full'){[math]::Ceiling($SoakSeconds/60)+15}else{15}) minutes."
        Write-Output "Report: $base/latest/verification-summary.md"
        return
    }
}
New-Item -ItemType Directory -Force $RunDirectory | Out-Null
$lock=$null
try{$lock=[IO.File]::Open((Join-Path $base 'verification.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Another verification is running; no overlapping run started.'}
$raw=Join-Path $RunDirectory 'raw'
$latest=Join-Path $base 'latest'
New-Item -ItemType Directory -Force $raw,$latest | Out-Null
$start=[DateTime]::UtcNow
$runtimeFile=Join-Path $RunDirectory 'runtime-paths.json'
$runtimePaths=if(Test-Path $runtimeFile){Get-Content $runtimeFile -Raw | ConvertFrom-Json}else{$null}
$git=Get-Command git -ErrorAction SilentlyContinue
if(!$git -and $runtimePaths.git -and (Test-Path -LiteralPath $runtimePaths.git)){$git=[pscustomobject]@{Source=$runtimePaths.git}}
if(!$git){$knownGit=@('C:\Program Files\Git\cmd\git.exe','C:\Program Files\Git\bin\git.exe')|Where-Object Test-Path|Select-Object -First 1;if($knownGit){$git=[pscustomobject]@{Source=$knownGit}}}
$branch='unavailable';$commit='unavailable';$dirty=$false
if($git){$branch=& $git.Source -C $root branch --show-current;$commit=& $git.Source -C $root rev-parse HEAD;$dirty=[bool](& $git.Source -C $root status --porcelain)}
$report=[ordered]@{Version=$version;Branch=$branch;Commit=$commit;Dirty=$dirty;Mode=$Mode;StartedUtc=$start;DurationSeconds=0;Conclusion='RUNNING';SourceSealed=$false;Stages=[ordered]@{};Warnings=@();RunDirectory=$RunDirectory}
$report['SourceHashes']=@(Get-DesktopLifeSourceInventory $root)
foreach($stage in @('Build','Tests','Analyzer','Deployment','WpfSmoke','HouseMixedDpi','Identity','Longitudinal','ShortStress','Soak','DualPresence')){$report.Stages[$stage]='NOT RUN'}
foreach($component in $components){$report.Stages[$component.Stage]='NOT RUN'}
function Publish-Summary {
    $report.DurationSeconds=[math]::Round(([DateTime]::UtcNow-$start).TotalSeconds,1)
    $json=$report | ConvertTo-Json -Depth 12
    $lines=@('# Desktop Life verification',"", "Version: $($report.Version)","Branch: $($report.Branch)","Commit: $($report.Commit) (dirty: $($report.Dirty))","Timestamp: $($report.StartedUtc.ToString('o'))","Mode: $Mode; duration: $($report.DurationSeconds)s","Conclusion: $($report.Conclusion)","")
    foreach($key in $report.Stages.Keys){$lines+="$key`: $($report.Stages[$key])"}
    if($report.Tests){$lines+="Tests: total=$($report.Tests.total), passed=$($report.Tests.passed), failed=$($report.Tests.failed), skipped=$($report.Tests.notExecuted)"}
    $lines+=@('', 'Warnings:')+$report.Warnings+@('',"Run: $RunDirectory",'Raw data is kept locally; read only a failure package referenced by this summary.')
    foreach($directory in @($RunDirectory,$latest)){
        $json | Set-Content (Join-Path $directory 'verification-summary.json.tmp') -Encoding UTF8
        Move-Item -LiteralPath (Join-Path $directory 'verification-summary.json.tmp') -Destination (Join-Path $directory 'verification-summary.json') -Force
        $lines | Set-Content (Join-Path $directory 'verification-summary.md.tmp') -Encoding UTF8
        Move-Item -LiteralPath (Join-Path $directory 'verification-summary.md.tmp') -Destination (Join-Path $directory 'verification-summary.md') -Force
        [ordered]@{RunDirectory=$RunDirectory;ProcessId=$PID;State=$report.Conclusion;UpdatedUtc=[DateTime]::UtcNow} | ConvertTo-Json | Set-Content (Join-Path $directory 'manifest.json') -Encoding UTF8
    }
}
function Invoke-Stage([string]$Name,[scriptblock]$Action){
    $report.Stages[$Name]='RUNNING';Publish-Summary
    try{& $Action *> (Join-Path $raw "$Name.log");$report.Stages[$Name]='PASS'}
    catch{
        $report.Stages[$Name]='FAIL'
        $failure=Join-Path $RunDirectory ('failures/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+$Name)
        New-Item -ItemType Directory -Force $failure | Out-Null
        [ordered]@{Stage=$Name;Message=$_.Exception.Message;RawLog=(Join-Path $raw "$Name.log");Commit=$report.Commit} | ConvertTo-Json | Set-Content (Join-Path $failure 'failure-summary.json') -Encoding UTF8
        Get-Content (Join-Path $raw "$Name.log") -Tail 60 | Set-Content (Join-Path $failure 'log-tail.txt') -Encoding UTF8
        $report.Warnings+="Failure: $failure"
        throw
    }finally{Publish-Summary}
}
function Invoke-StressAnalysis([ValidateSet('short','soak')][string]$Label){
    $stressReport=Join-Path $raw "$Label/stress-report.json"
    $analysisPath=Join-Path $raw "$Label/analysis.json"
    $python=if($runtimePaths.python){$runtimePaths.python}else{(Get-Command python -ErrorAction Stop).Source}
    & $python "$PSScriptRoot/analyze-soak.py" $stressReport --output $analysisPath *> (Join-Path $raw "$Label/analysis.log")
    if($LASTEXITCODE){throw "$Label stress analysis failed"}
    $analysis=Get-Content -LiteralPath $analysisPath -Raw | ConvertFrom-Json
    $report[$Label+'Metrics']=$analysis
    if($analysis.Conclusion -ne 'PASS'){
        $failure=Join-Path $RunDirectory ('failures/'+$Label+'-metrics')
        New-Item -ItemType Directory -Force $failure | Out-Null
        Copy-Item -LiteralPath $analysisPath -Destination (Join-Path $failure 'failure-summary.json')
        $report.Warnings+="Resource/navigation review: $failure"
        throw "$Label stress analysis requires attention: $($analysis.Conclusion). No later workload started."
    }
    Assert-DesktopLifeStressEvidence $report.BuildHashes $RunDirectory $Label $version
    # Avoid a two-hour workload if the short run already tested stale source/binaries.
    Assert-DesktopLifeSourceInventory $report.SourceHashes $root
    Assert-DesktopLifeBuildInventory $report.BuildHashes $root
}
try{
    Push-Location $root
    $dotnet=& "$PSScriptRoot/dotnet-path.ps1"
    Publish-Summary
    Invoke-Stage Build {& $dotnet build DesktopLife.slnx -c Release --nologo;if($LASTEXITCODE){throw 'Release build failed'}}
    $report['BuildHashes']=@(Get-DesktopLifeBuildInventory $root)
    Invoke-Stage Tests {& $dotnet test DesktopLife.slnx -c Release --no-build --nologo --logger 'trx;LogFileName=tests.trx' --results-directory $raw;if($LASTEXITCODE){throw 'Unit tests failed'}}
    Invoke-Stage Analyzer {
        $python=if($runtimePaths.python){$runtimePaths.python}else{(Get-Command python -ErrorAction Stop).Source}
        & $python "$PSScriptRoot/test-analyze-soak.py"
        if($LASTEXITCODE){throw 'Verification analyzer regression failed'}
    }
    Invoke-Stage Deployment {
        & "$PSScriptRoot/test-deployment-transactions.ps1" -OutputDirectory (Join-Path $raw 'deployment')
        & "$PSScriptRoot/test-release-verification.ps1" -OutputDirectory (Join-Path $raw 'release-gates')
        if($components.Count -gt 0){& "$PSScriptRoot/test-upgrade012-contract.ps1" -OutputDirectory (Join-Path $raw 'upgrade012-contracts')}
    }
    [xml]$trx=Get-Content (Join-Path $raw 'tests.trx') -Raw
    $report['Tests']=$trx.TestRun.ResultSummary.Counters | Select-Object total,passed,failed,notExecuted
    foreach($simulation in @('Identity','Longitudinal')){
        $filter="Verification=$simulation"
        Invoke-Stage $simulation {& $dotnet test tests/DesktopLife.Tests -c Release --no-build --nologo --filter $filter;if($LASTEXITCODE){throw "$simulation simulation failed"}}
    }
    Invoke-Stage DualPresence {& $dotnet test tests/DesktopLife.Tests -c Release --no-build --nologo --filter 'FullyQualifiedName~HouseholdTests|FullyQualifiedName~DualSaveTests|FullyQualifiedName~BorderCollieTests';if($LASTEXITCODE){throw 'Dual Presence model tests failed'}}
    if($Mode -ne 'Quick'){
        Invoke-Stage HouseMixedDpi {& "$PSScriptRoot/house-smoke.ps1" -OutputDirectory (Join-Path $raw 'house-mixed-dpi')}
        Invoke-Stage WpfSmoke {& "$PSScriptRoot/smoke.ps1"}
        foreach($component in $components){
            Invoke-Stage $component.Stage {
                & "$PSScriptRoot/component-smoke.ps1" -Component $component.Stage -OutputDirectory (Join-Path $raw $component.Directory)
                Assert-DesktopLifeComponentEvidence $report.BuildHashes $RunDirectory $component
            }
        }
        Invoke-Stage ShortStress {
            & "$PSScriptRoot/stress.ps1" -Seconds 600 -Label short -OutputDirectory $raw -Presence All -Floors 3
            Invoke-StressAnalysis short
        }
    }
    if($Mode -eq 'Full'){
        Invoke-Stage Soak {
            & "$PSScriptRoot/stress.ps1" -Seconds $SoakSeconds -Label soak -OutputDirectory $raw -Presence All -Floors 3
            Invoke-StressAnalysis soak
        }
    }
    # Never seal a source tree or binary set different from the one captured before testing.
    Assert-DesktopLifeSourceInventory $report.SourceHashes $root
    Assert-DesktopLifeBuildInventory $report.BuildHashes $root
    $report.SourceSealed=$true;$report['SourceSealedUtc']=[DateTime]::UtcNow
    if($Mode -eq 'Quick'){$report.Warnings+='WPF smoke and native stress remain outside Quick mode.';$report.Conclusion='REVIEW'}
    elseif($report.Stages.WpfSmoke -ne 'PASS'){$report.Warnings+='WPF dual-presence smoke did not run.';$report.Conclusion='REVIEW'}
    elseif($Mode -eq 'Standard' -and $report.Stages.ShortStress -eq 'PASS' -and $report.Warnings.Count -eq 0){$report.Conclusion='PASS'}
    elseif($Mode -eq 'Full' -and $report.Stages.Soak -eq 'PASS' -and $report.Warnings.Count -eq 0){$report.Conclusion='PASS'}
    else{$report.Conclusion='REVIEW'}
    if($report.Conclusion -eq 'PASS'){Assert-DesktopLifeVerificationEvidence $report $root $RunDirectory $Mode}
}catch{$report.Conclusion='FAIL';$report.Warnings+=$_.Exception.Message}
finally{Publish-Summary;Pop-Location;$lock.Dispose()}
Write-Output "Result: $($report.Conclusion). Report: $latest/verification-summary.md"
if($report.Conclusion -eq 'FAIL'){exit 1}

