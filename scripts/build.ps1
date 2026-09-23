$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = & "$PSScriptRoot/dotnet-path.ps1"
Push-Location $root
try {
    & $dotnet build DesktopLife.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    & $dotnet test DesktopLife.slnx -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally { Pop-Location }
