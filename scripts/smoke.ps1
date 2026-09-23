param([switch]$RequireAllSensors)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = & "$PSScriptRoot/dotnet-path.ps1"
Push-Location $root
try {
    $sensorArgs = @('run','--project','tests/DesktopLife.SensorSmoke','-c','Release','--no-build')
    if ($RequireAllSensors) { $sensorArgs += @('--','--require-all') }
    & $dotnet @sensorArgs
    if ($LASTEXITCODE -ne 0) { throw 'Sensor smoke failed' }
    $process = Start-Process -FilePath $dotnet -ArgumentList @('"src/DesktopLife.App/bin/Release/net10.0-windows/DesktopLife.App.dll"','--smoke-test') -WorkingDirectory $root -WindowStyle Hidden -PassThru
    $processHandle=$process.Handle
    if (!$process.WaitForExit(45000)) { $process.Kill(); throw 'WPF smoke timed out' }
    if ($process.ExitCode -ne 0) { throw "WPF smoke failed: $($process.ExitCode)" }
    Write-Output 'WPF smoke passed. Isolated data/logs: %TEMP%/DesktopLifeSmoke/<run-id> (does not change your pet).'
} finally { Pop-Location }
