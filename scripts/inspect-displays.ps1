param([string]$OutputPath='artifacts/verification/0.11/displays/native-inventory.json',[switch]$NativeWorker,[string]$NativeResult)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/native-profile-context.ps1"
$root=Split-Path $PSScriptRoot -Parent
if(![IO.Path]::IsPathRooted($OutputPath)){$OutputPath=Join-Path $root $OutputPath}
if(!$NativeWorker){Invoke-DesktopLifeNative -ScriptPath $PSCommandPath -Arguments @('-OutputPath',$OutputPath);return}
try {
    if($NativeResult){Start-Transcript -Path ($NativeResult+'.log') -Force | Out-Null}
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class DesktopLifeDisplayInventory {
    [StructLayout(LayoutKind.Sequential)]public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct Info {public int Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct Device {public int Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;}
    public sealed class Display {public string DeviceName,Adapter,MonitorName,MonitorIdentity;public long Handle;public Rect MonitorPixels,WorkAreaPixels;public uint EffectiveDpiX,EffectiveDpiY;public bool Primary,Portrait;}
    private delegate bool Callback(IntPtr monitor,IntPtr dc,ref Rect rectangle,IntPtr data);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,Callback callback,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfoW(IntPtr monitor,ref Info info);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool EnumDisplayDevicesW(string device,uint index,ref Device value,uint flags);
    [DllImport("shcore.dll")]private static extern int GetDpiForMonitor(IntPtr monitor,int kind,out uint x,out uint y);
    [DllImport("user32.dll")]private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    public static List<Display> Read(){
        var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
            var adapters=new Dictionary<string,string>();
            for(uint index=0;;index++){var device=new Device{Size=Marshal.SizeOf(typeof(Device))};if(!EnumDisplayDevicesW(null,index,ref device,0))break;adapters[device.Name]=device.Description;}
            var values=new List<Display>();
            EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr monitor,IntPtr dc,ref Rect rectangle,IntPtr data)=>{
                var info=new Info{Size=Marshal.SizeOf(typeof(Info))};if(!GetMonitorInfoW(monitor,ref info))return true;
                var child=new Device{Size=Marshal.SizeOf(typeof(Device))};EnumDisplayDevicesW(info.Device,0,ref child,1);
                uint x,y;if(GetDpiForMonitor(monitor,0,out x,out y)!=0){x=y=96;}
                string adapter;adapters.TryGetValue(info.Device,out adapter);
                values.Add(new Display{DeviceName=info.Device,Adapter=adapter,MonitorName=child.Description,MonitorIdentity=child.Id,Handle=monitor.ToInt64(),MonitorPixels=info.Monitor,WorkAreaPixels=info.Work,EffectiveDpiX=x,EffectiveDpiY=y,Primary=(info.Flags&1)!=0,Portrait=(info.Monitor.Bottom-info.Monitor.Top)>(info.Monitor.Right-info.Monitor.Left)});
                return true;
            },IntPtr.Zero);
            return values;
        } finally {SetThreadDpiAwarenessContext(previous);}
    }
}
'@
    $monitors=@([DesktopLifeDisplayInventory]::Read())
    $adapters=@(Get-CimInstance Win32_VideoController | Select-Object Name,PNPDeviceID,DriverVersion,CurrentHorizontalResolution,CurrentVerticalResolution)
    $nativeData=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DesktopLife'
    $nativeInstall=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/DesktopLife'
    $installationPath=Join-Path $nativeInstall 'installation.json'
    $installation=if(Test-Path -LiteralPath $installationPath){Get-Content -LiteralPath $installationPath -Raw -Encoding UTF8|ConvertFrom-Json}else{$null}
    $installedBinary=if($installation -and (Test-Path -LiteralPath $installation.Executable)){[ordered]@{Path=$installation.Executable;FileVersion=(Get-Item -LiteralPath $installation.Executable).VersionInfo.FileVersion;Sha256=(Get-FileHash -LiteralPath $installation.Executable -Algorithm SHA256).Hash}}else{$null}
    $running=@(Get-CimInstance Win32_Process|Where-Object {$_.ExecutablePath -and $_.ExecutablePath.StartsWith($nativeInstall+'\',[StringComparison]::OrdinalIgnoreCase)}|ForEach-Object {[ordered]@{ProcessId=$_.ProcessId;Path=$_.ExecutablePath;FileVersion=(Get-Item -LiteralPath $_.ExecutablePath).VersionInfo.FileVersion;Sha256=(Get-FileHash -LiteralPath $_.ExecutablePath -Algorithm SHA256).Hash}})
    $localReferences=@(Get-ChildItem -LiteralPath (Join-Path $root 'artifacts/standalone') -Directory -ErrorAction SilentlyContinue|Where-Object Name -like '0.10*'|ForEach-Object {Get-ChildItem -LiteralPath $_.FullName -Filter DesktopLife.exe -File -ErrorAction SilentlyContinue}|ForEach-Object {[ordered]@{Path=$_.FullName;FileVersion=$_.VersionInfo.FileVersion;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
    $report=[ordered]@{CapturedUtc=[DateTimeOffset]::UtcNow.ToString('O');NativeProfile=$true;Displays=$monitors;VideoAdapters=$adapters;DataDirectory=$nativeData;InstallationDirectory=$nativeInstall;InstalledBinary=$installedBinary;RunningInstalledExecutables=$running;NativeReferenceExecutables=$localReferences;Scope='Read-only native display/installed-process/reference inventory. No application shutdown, installation, disconnect, rotation, DPI, or OS layout change performed.'}
    New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath -Parent) | Out-Null
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    Write-DesktopLifeNativeResult $NativeResult $true ('Display inventory saved: '+$OutputPath)
} catch {Write-DesktopLifeNativeResult $NativeResult $false $_.Exception.Message;throw}
finally {if($NativeResult){Stop-Transcript | Out-Null}}
