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
    $env:DOTNET_ROOT=Join-Path $root 'tools/dotnet'
    $process = Start-Process -FilePath (Join-Path $root 'src/DesktopLife.App/bin/Release/net10.0-windows/DesktopLife.App.exe') -ArgumentList '--smoke-test' -WorkingDirectory $root -WindowStyle Hidden -PassThru
    $processHandle=$process.Handle
    if (!$process.WaitForExit(90000)) { $process.Kill(); throw 'WPF smoke timed out' }
    if ($process.ExitCode -ne 0) {
        $marker=Get-ChildItem "$env:TEMP/DesktopLifeSmoke" -Filter diagnostic-process.json -Recurse | Where-Object {(Get-Content $_.FullName -Raw|ConvertFrom-Json).ProcessId -eq $process.Id} | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if($marker){Write-Output "Failure evidence: $($marker.DirectoryName)";Get-Content (Join-Path $marker.DirectoryName 'logs/app.log') -Tail 25 | Write-Output}
        throw "WPF smoke failed: $($process.ExitCode)"
    }
    Write-Output 'WPF smoke passed. Isolated data/logs: %TEMP%/DesktopLifeSmoke/<run-id> (does not change your pet).'
} finally { Pop-Location }
