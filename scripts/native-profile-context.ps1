function Write-DesktopLifeNativeResult([string]$Path,[bool]$Succeeded,[string]$Message){
    if(!$Path){return}
    $temporary=$Path+'.tmp-'+[guid]::NewGuid().ToString('N')
    [ordered]@{Succeeded=$Succeeded;Message=$Message;CompletedUtc=[DateTime]::UtcNow;DataDirectory=(Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DesktopLife')} | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}

function Invoke-DesktopLifeNative([string]$ScriptPath,[string[]]$Arguments){
    # Codex's packaged child shell can read a redirected AppData tree. Launch
    # the same operation outside that package, like the independently running app.
    $run=Join-Path (Split-Path (Split-Path $ScriptPath -Parent) -Parent) ('artifacts/native-runs/'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $run -Force | Out-Null
    $resultPath=Join-Path $run 'result.json'
    $nativePowerShell=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $tokens=@($nativePowerShell,'-NoProfile','-ExecutionPolicy','Bypass','-File',$ScriptPath,'-NativeWorker','-NativeResult',$resultPath)+$Arguments
    foreach($token in $tokens){if($token.Contains('"')){throw 'Native worker arguments contain an invalid quote.'}}
    $command=($tokens | ForEach-Object {'"'+$_+'"'}) -join ' '
    $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
    $process=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=(Split-Path $ScriptPath -Parent);ProcessStartupInformation=$startup}
    if($process.ReturnValue -ne 0){throw ('Native worker could not start: '+$process.ReturnValue)}
    Write-Output ('Native profile operation started. PID: '+$process.ProcessId+'. Result: '+$resultPath)
    $endedPolls=0
    while(!(Test-Path -LiteralPath $resultPath)){
        Start-Sleep -Seconds 1
        if(!(Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue)){
            $endedPolls++
            if($endedPolls -ge 3){throw ('Native worker exited without a result. Diagnostic log: '+$resultPath+'.log')}
        }
    }
    $result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if(!$result.Succeeded){throw ('Native profile operation failed: '+$result.Message+'. Log: '+$resultPath+'.log')}
    Write-Output ('Native profile operation completed. Data: '+$result.DataDirectory)
}
