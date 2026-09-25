param([ValidateRange(60,900)][int]$Seconds=600)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet=& "$PSScriptRoot/dotnet-path.ps1"
$process=Start-Process -FilePath $dotnet -ArgumentList @('"src/DesktopLife.App/bin/Release/net10.0-windows/DesktopLife.App.dll"','--stress-test',"--stress-seconds=$Seconds") -WorkingDirectory $root -WindowStyle Hidden -PassThru
$handle=$process.Handle
Write-Output "Isolated stress process: $($process.Id), requested seconds: $Seconds"
while(!$process.WaitForExit(30000)){Write-Output 'Stress scenario running; report is saved in its isolated DesktopLifeSmoke folder.'}
if($process.ExitCode -ne 0){throw "Stress failed: $($process.ExitCode)"}
$result=Get-ChildItem "$env:TEMP/DesktopLifeSmoke" -Filter stress-report.json -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if(!$result){throw 'Stress report missing'}
$destination=Join-Path $root 'artifacts/verification/0.7'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item -LiteralPath $result.FullName -Destination (Join-Path $destination 'stress-report.json')
Write-Output "Stress complete: $($result.FullName)"
