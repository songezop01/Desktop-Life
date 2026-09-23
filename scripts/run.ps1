$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $release=Join-Path $root 'artifacts/release/DesktopLife.App.exe'
    $latest=Join-Path $root 'artifacts/standalone/latest.json'
    if(Test-Path -LiteralPath $latest){$release=(Get-Content -LiteralPath $latest -Raw | ConvertFrom-Json).Executable}
    if(Test-Path -LiteralPath $release){
        $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]1}
        $result=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=('"'+$release+'"');CurrentDirectory=(Split-Path $release -Parent);ProcessStartupInformation=$startup}
        if($result.ReturnValue -ne 0){throw "Independent launch failed: $($result.ReturnValue). Please double-click DesktopLife.exe."}
    }
    else { & "$root/tools/dotnet/dotnet.exe" run --project "$root/src/DesktopLife.App" -c Release }
} finally { Pop-Location }
