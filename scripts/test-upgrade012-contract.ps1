param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/deployment-transaction.ps1"
. "$PSScriptRoot/release-verification.ps1"
. "$PSScriptRoot/upgrade012-contract.ps1"
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('artifacts/verification/0.12/u-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Choose a new isolated contract output directory.'}
New-Item -ItemType Directory -Path $output | Out-Null
$results=[Collections.Generic.List[object]]::new()
$fixtureIndex=0

function Assert-Test([bool]$Condition,[string]$Message){if(!$Condition){throw $Message}}
function Assert-Rejected([scriptblock]$Action){
    $rejected=$false
    try{& $Action|Out-Null}catch{$rejected=$true}
    if(!$rejected){throw 'The invalid upgrade contract was accepted.'}
}
function Write-Fixture([string]$Path,[string]$Value){
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force|Out-Null
    [IO.File]::WriteAllText($Path,$Value,[Text.UTF8Encoding]::new($false))
}
function Write-Json([string]$Path,$Value){Write-Fixture $Path ($Value|ConvertTo-Json -Depth 10)}
function New-FixtureDirectory{
    $script:fixtureIndex++
    $directory=Join-Path $output ('f'+$script:fixtureIndex.ToString('000'))
    New-Item -ItemType Directory -Path $directory|Out-Null
    return $directory
}
function New-Summary{[ordered]@{Version='0.12.0';Mode='Full';Conclusion='PASS';SourceSealed=$true}}
function New-Package{
    $project=New-FixtureDirectory
    $build='0.12.0-20261010-120000'
    $directory=Join-Path $project ('artifacts/standalone/'+$build)
    $exe=Join-Path $directory 'DesktopLife.exe'
    Write-Fixture $exe 'synthetic apphost, never executable'
    Write-Fixture (Join-Path $directory 'README.md') 'package readme'
    Write-Fixture (Join-Path $directory 'docs/GUIDE.md') 'installed guide'
    Write-Fixture (Join-Path $directory 'docs/nested/DETAIL.md') 'installed detail'
    $release=[ordered]@{Version='0.12.0';Build=$build;Executable=$exe;Sha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;Architecture='win-x64';SelfContained=$true;SingleFile=$true}
    Write-Json (Join-Path $directory 'release.json') $release
    $inventory=@(Get-DesktopLifeTreeInventory $directory)
    $binary=[ordered]@{BinarySource=$directory;Executable=(Join-Path (Join-Path $project 'b') 'DesktopLife.exe');Files=@($inventory|ForEach-Object {[ordered]@{Name=$_.Path;Sha256=$_.Sha256}})}
    [pscustomobject]@{Project=$project;Directory=$directory;Release=$release;Inventory=$inventory;Binary=$binary}
}
function New-Profile{
    $directory=New-FixtureDirectory
    $data=Join-Path $directory 'p';$backup=Join-Path $data 'upgrade-backups/verified'
    Write-Fixture (Join-Path $data 'organism.json') 'live organism'
    Write-Fixture (Join-Path $data 'organism.json.bak.1') 'previous generation'
    Write-Fixture (Join-Path $data 'audio/custom.wav') 'custom user audio'
    Write-Fixture (Join-Path $data 'instance.lock') 'active lock sentinel'
    Write-Fixture (Join-Path $data 'upgrade-backups/old/organism.json') 'older recovery'
    New-Item -ItemType Directory -Path (Join-Path $data 'empty/nested') -Force|Out-Null
    $expected=Get-DesktopLife012ActiveInventory $data
    New-Item -ItemType Directory -Path $backup|Out-Null
    foreach($entry in @(Get-DesktopLifeActiveEntries $data)){Copy-Item -LiteralPath $entry.FullName -Destination $backup -Recurse}
    [pscustomobject]@{Data=$data;Backup=$backup;Expected=$expected;Directory=$directory}
}
function Invoke-Case([string]$Name,[scriptblock]$Action){
    try{& $Action|Out-Null;$results.Add([ordered]@{Case=$Name;Passed=$true});Write-Output ('PASS '+$Name)}
    catch{$results.Add([ordered]@{Case=$Name;Passed=$false;Failure=$_.Exception.Message;Stack=$_.ScriptStackTrace});Write-Output ('FAIL '+$Name+': '+$_.Exception.Message)}
}

Invoke-Case 'initial-stable-full-pass' {Assert-DesktopLife012InitialRelease '0.12.0' (New-Summary)}
Invoke-Case 'preview-cannot-install' {Assert-Rejected {Assert-DesktopLife012InitialRelease '0.12.0-preview.1' (New-Summary)}}
Invoke-Case 'patch-does-not-inherit-full-installer' {Assert-Rejected {Assert-DesktopLife012InitialRelease '0.12.1' (New-Summary)}}
Invoke-Case 'standard-is-not-full' {$s=New-Summary;$s.Mode='Standard';Assert-Rejected {Assert-DesktopLife012InitialRelease '0.12.0' $s}}
Invoke-Case 'stopped-is-not-pass' {$s=New-Summary;$s.Conclusion='STOPPED';Assert-Rejected {Assert-DesktopLife012InitialRelease '0.12.0' $s}}
Invoke-Case 'seal-must-be-boolean-true' {$s=New-Summary;$s.SourceSealed='true';Assert-Rejected {Assert-DesktopLife012InitialRelease '0.12.0' $s}}
Invoke-Case 'summary-version-must-match' {$s=New-Summary;$s.Version='0.11.0';Assert-Rejected {Assert-DesktopLife012InitialRelease '0.12.0' $s}}
Invoke-Case 'published-stable-bundle' {$p=New-Package;Assert-Test ((Assert-DesktopLife012PublishedRelease $p.Release $p.Project) -eq $p.Directory) 'Package path differs.'}
Invoke-Case 'published-preview-rejected' {$p=New-Package;$p.Release.Version='0.12.0-preview.1';Assert-Rejected {Assert-DesktopLife012PublishedRelease $p.Release $p.Project}}
Invoke-Case 'published-wrong-architecture-rejected' {$p=New-Package;$p.Release.Architecture='win-arm64';Assert-Rejected {Assert-DesktopLife012PublishedRelease $p.Release $p.Project}}
Invoke-Case 'published-bundle-booleans-strict' {$p=New-Package;$p.Release.SelfContained='true';Assert-Rejected {Assert-DesktopLife012PublishedRelease $p.Release $p.Project}}
Invoke-Case 'published-apphost-tamper-rejected' {$p=New-Package;Write-Fixture $p.Release.Executable 'changed';Assert-Rejected {Assert-DesktopLife012PublishedRelease $p.Release $p.Project}}
Invoke-Case 'published-path-must-match-build' {$p=New-Package;$p.Release.Executable=Join-Path $p.Project 'DesktopLife.exe';Write-Fixture $p.Release.Executable 'synthetic apphost, never executable';Assert-Rejected {Assert-DesktopLife012PublishedRelease $p.Release $p.Project}}
Invoke-Case 'index-must-match-package-manifest' {$p=New-Package;$m=[ordered]@{};foreach($key in $p.Release.Keys){$m[$key]=$p.Release[$key]};$m.Architecture='other';Write-Json (Join-Path $p.Directory 'release.json') $m;Assert-Rejected {Assert-DesktopLife012PublishedRelease $p.Release $p.Project}}
Invoke-Case 'component-snapshot-covers-entire-package' {$p=New-Package;Assert-DesktopLife012PackageSnapshot $p.Release $p.Inventory $p.Binary}
Invoke-Case 'component-source-must-be-published-package' {$p=New-Package;$p.Binary.BinarySource=$p.Project;Assert-Rejected {Assert-DesktopLife012PackageSnapshot $p.Release $p.Inventory $p.Binary}}
Invoke-Case 'component-must-use-standalone-apphost' {$p=New-Package;$p.Binary.Executable=Join-Path $p.Project 'DesktopLife.App.exe';Assert-Rejected {Assert-DesktopLife012PackageSnapshot $p.Release $p.Inventory $p.Binary}}
Invoke-Case 'component-cannot-omit-package-readme' {$p=New-Package;$p.Binary.Files=@($p.Binary.Files|Where-Object Name -CNE 'README.md');Assert-Rejected {Assert-DesktopLife012PackageSnapshot $p.Release $p.Inventory $p.Binary}}
Invoke-Case 'component-cannot-change-doc-hash' {$p=New-Package;($p.Binary.Files|Where-Object Name -CEQ 'docs\GUIDE.md').Sha256=('b'*64);Assert-Rejected {Assert-DesktopLife012PackageSnapshot $p.Release $p.Inventory $p.Binary}}
Invoke-Case 'component-cannot-add-unverified-file' {$p=New-Package;$p.Binary.Files+=@([ordered]@{Name='extra.txt';Sha256=('b'*64)});Assert-Rejected {Assert-DesktopLife012PackageSnapshot $p.Release $p.Inventory $p.Binary}}
Invoke-Case 'component-cannot-duplicate-file' {$p=New-Package;$p.Binary.Files+=@($p.Binary.Files[0]);Assert-Rejected {Assert-DesktopLife012PackageSnapshot $p.Release $p.Inventory $p.Binary}}
Invoke-Case 'installed-inventory-matches-installer-copy-contract' {
    $p=New-Package;$installed=@(Get-DesktopLife012InstalledInventory $p.Inventory)
    Assert-Test ($installed.Count -eq 4) 'Installed files must include both docs, exe and release.json.'
    Assert-Test (@($installed|Where-Object Path -CEQ 'README.md').Count -eq 0) 'Package-only README entered the installed inventory.'
    $target=Join-Path $p.Project 'installed';New-Item -ItemType Directory -Path $target|Out-Null
    foreach($entry in $installed){Write-Fixture (Join-Path $target $entry.Path) (Get-Content -LiteralPath (Join-Path $p.Directory $entry.Path) -Raw)}
    Assert-DesktopLifeTreeInventory $target $installed
}
Invoke-Case 'installed-documentation-required' {$p=New-Package;$bad=@($p.Inventory|Where-Object {$_.Path -notmatch '^docs[\\/]'});Assert-Rejected {Get-DesktopLife012InstalledInventory $bad}}
Invoke-Case 'installed-invalid-inventory-path-rejected' {$p=New-Package;$p.Inventory+=@([ordered]@{Path='../escape';Sha256=('a'*64)});Assert-Rejected {Get-DesktopLife012InstalledInventory $p.Inventory}}
Invoke-Case 'active-profile-excludes-lock-and-recovery' {
    $p=New-Profile
    Assert-Test (@($p.Expected.Files).Count -eq 3) 'Lock or old recovery entered the active inventory.'
    Assert-Test ($p.Expected.Directories -contains 'empty\nested') 'Empty nested folder was lost.'
    Assert-DesktopLife012Backup $p.Backup $p.Expected
}
Invoke-Case 'backup-file-tamper-rejected' {$p=New-Profile;Write-Fixture (Join-Path $p.Backup 'organism.json') 'changed';Assert-Rejected {Assert-DesktopLife012Backup $p.Backup $p.Expected}}
Invoke-Case 'backup-unexpected-file-rejected' {$p=New-Profile;Write-Fixture (Join-Path $p.Backup 'extra.txt') 'new';Assert-Rejected {Assert-DesktopLife012Backup $p.Backup $p.Expected}}
Invoke-Case 'backup-missing-file-rejected' {
    $p=New-Profile;$bad=Join-Path $p.Directory 'incomplete';New-Item -ItemType Directory -Path $bad|Out-Null
    foreach($directory in $p.Expected.Directories){New-Item -ItemType Directory -Path (Join-Path $bad $directory) -Force|Out-Null}
    foreach($entry in @($p.Expected.Files|Select-Object -Skip 1)){Copy-Item -LiteralPath (Join-Path $p.Backup $entry.Path) -Destination (Join-Path $bad $entry.Path)}
    Assert-Rejected {Assert-DesktopLife012Backup $bad $p.Expected}
}
Invoke-Case 'backup-missing-empty-directory-rejected' {
    $p=New-Profile;$bad=Join-Path $p.Directory 'no-empty';New-Item -ItemType Directory -Path $bad|Out-Null
    foreach($entry in $p.Expected.Files){Write-Fixture (Join-Path $bad $entry.Path) (Get-Content -LiteralPath (Join-Path $p.Backup $entry.Path) -Raw)}
    Assert-Rejected {Assert-DesktopLife012Backup $bad $p.Expected}
}
Invoke-Case 'backup-extra-empty-directory-rejected' {$p=New-Profile;New-Item -ItemType Directory -Path (Join-Path $p.Backup 'unexpected-empty')|Out-Null;Assert-Rejected {Assert-DesktopLife012Backup $p.Backup $p.Expected}}
Invoke-Case 'empty-profile-is-preserved' {$d=New-FixtureDirectory;$p=Join-Path $d 'p';$b=Join-Path $d 'b';New-Item -ItemType Directory -Path $p,$b|Out-Null;$expected=Get-DesktopLife012ActiveInventory $p;Assert-DesktopLife012Backup $b $expected}
Invoke-Case 'profile-root-file-rejected' {$d=New-FixtureDirectory;$p=Join-Path $d 'p';Write-Fixture $p 'file';Assert-Rejected {Assert-DesktopLife012DataRoots $p}}
Invoke-Case 'backup-root-file-rejected' {$d=New-FixtureDirectory;$p=Join-Path $d 'p';Write-Fixture (Join-Path $p 'upgrade-backups') 'file';Assert-Rejected {Assert-DesktopLife012DataRoots $p}}
Invoke-Case 'redirected-backup-root-rejected-before-copy' {
    $d=New-FixtureDirectory;$p=Join-Path $d 'p';$outside=Join-Path $d 'other'
    New-Item -ItemType Directory -Path $p,$outside|Out-Null
    New-Item -ItemType Junction -Path (Join-Path $p 'upgrade-backups') -Target $outside|Out-Null
    Assert-Rejected {Assert-DesktopLife012DataRoots $p}
}
Invoke-Case 'redirected-active-entry-rejected-before-copy' {
    $d=New-FixtureDirectory;$p=Join-Path $d 'p';$outside=Join-Path $d 'other'
    New-Item -ItemType Directory -Path $p,$outside|Out-Null
    New-Item -ItemType Junction -Path (Join-Path $p 'imported') -Target $outside|Out-Null
    Assert-Rejected {Get-DesktopLife012ActiveInventory $p}
}
Invoke-Case 'post-install-profile-change-compensates' {
    $p=New-Profile;$manifest=Join-Path $p.Directory 'i.json';Write-Fixture $manifest 'old manifest'
    $transaction=Join-Path $p.Data 'upgrade-backups/t';$caught=$null
    try{
        Invoke-DesktopLifeDeploymentTransaction -DataDirectory $p.Data -SnapshotDirectory $transaction -MetadataPaths @($manifest) -Action {
            Write-Fixture $manifest 'new manifest'
            Write-Fixture (Join-Path $p.Data 'organism.json') 'unexpected writer'
            $current=Get-DesktopLife012ActiveInventory $p.Data
            Assert-DesktopLifeInventory @($p.Expected.Files) @($current.Files) 'Post-install profile'
        }
    }catch{$caught=$_.Exception}
    Assert-Test ($caught -and $caught.Data['DesktopLifeCompensation'] -eq 'COMPENSATED') 'Failed postcheck did not compensate.'
    $restored=Get-DesktopLife012ActiveInventory $p.Data
    Assert-DesktopLifeInventory @($p.Expected.Files) @($restored.Files) 'Compensated profile'
    Assert-Test (($restored.Directories -join '|') -ceq ($p.Expected.Directories -join '|')) 'Compensation lost empty folders.'
    Assert-Test ((Get-Content -LiteralPath $manifest -Raw) -ceq 'old manifest') 'Old install manifest was not restored.'
    Assert-DesktopLife012Backup $p.Backup $p.Expected
}

$failed=@($results|Where-Object {!$_.Passed})
$report=[ordered]@{Status=if($failed.Count -eq 0){'PASS'}else{'FAIL'};Scope='Synthetic upgrade contracts only; no native app, publication, installation or live user profile was used.';Total=$results.Count;Passed=$results.Count-$failed.Count;Failed=$failed.Count;Cases=$results.ToArray()}
$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $output 'upgrade012-contract-tests.json') -Encoding UTF8
Write-Output ("Upgrade 0.12 contracts: $($report.Passed)/$($report.Total); $output")
if($failed.Count){throw 'Upgrade 0.12 contract tests failed.'}
