using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using DesktopLife.Core;

namespace DesktopLife.Windows;

public sealed class WindowsEnvironmentSensor : IEnvironmentSensor
{
    private readonly PdhCounters pdh = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double? lastTime;
    private (ulong Idle, ulong Kernel, ulong User)? lastCpu;
    private Point? lastMouse;
    private Dictionary<string, (long Sent, long Received)> lastNetwork = new();
    private bool disposed;
    public EnvironmentState Sample()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var started = clock.Elapsed.TotalSeconds;
        var seconds = lastTime is { } t ? started - t : 0;
        lastTime = started;
        var gap = seconds <= 0 || seconds > 10;
        var cpu = SensorValue.Missing("暖機中");
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (!gap && lastCpu is { } old && SensorMath.CpuPercent(old.Idle, old.Kernel, old.User, idle, kernel, user) is { } usage)
                cpu = SensorValue.Available(usage);
            lastCpu = (idle, kernel, user);
        }
        else { lastCpu = null; cpu = SensorValue.Missing("GetSystemTimes 不可用"); }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        var ram = GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0
            ? SensorValue.Available(100.0 * (memory.TotalPhysical - memory.AvailablePhysical) / memory.TotalPhysical)
            : SensorValue.Missing("GlobalMemoryStatusEx 不可用");
        var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        var idleTime = GetLastInputInfo(ref input)
            ? SensorValue.Available(SensorMath.IdleSeconds(unchecked((uint)Environment.TickCount64), input.Time))
            : SensorValue.Missing("目前 session 無法讀取閒置時間");
        var mouse = SensorValue.Missing("暖機中");
        if (GetCursorPos(out var point))
        {
            if (lastMouse is { } previous && SensorMath.MouseSpeed(previous.X, previous.Y, point.X, point.Y, seconds) is { } speed)
                mouse = SensorValue.Available(speed);
            lastMouse = point;
        }
        else { lastMouse = null; mouse = SensorValue.Missing("目前桌面無法取得游標"); }
        var network = ReadNetwork(seconds);
        pdh.Collect();
        var warming = SensorValue.Missing("暖機／長時間中斷後重新取樣");
        return new(DateTimeOffset.UtcNow, cpu, gap ? warming : pdh.ReadGpu(), ram,
            gap ? warming : pdh.Read("read"), gap ? warming : pdh.Read("write"),
            network.Up, network.Down, idleTime, mouse, (clock.Elapsed.TotalSeconds - started) * 1000);
    }
    private (SensorValue Up, SensorValue Down) ReadNetwork(double seconds)
    {
        try
        {
            var current = new Dictionary<string, (long Sent, long Received)>();
            double up = 0, down = 0;
            var valid = 0;
            var failed = false;
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                try
                {
                    var stats = adapter.GetIPStatistics();
                    current[adapter.Id] = (stats.BytesSent, stats.BytesReceived);
                    if (lastNetwork.TryGetValue(adapter.Id, out var previous)
                        && SensorMath.Rate(previous.Sent, stats.BytesSent, seconds) is { } tx
                        && SensorMath.Rate(previous.Received, stats.BytesReceived, seconds) is { } rx)
                    { up += tx; down += rx; valid++; }
                }
                catch (NetworkInformationException) { failed = true; }
            }
            lastNetwork = current;
            if (failed) return MissingNetwork("部分網路介面無法取樣");
            if (current.Count == 0) return MissingNetwork("無啟用的非 loopback 介面");
            if (valid != current.Count) return MissingNetwork("介面暖機／重設中");
            return (SensorValue.Available(up), SensorValue.Available(down));
        }
        catch (NetworkInformationException) { lastNetwork.Clear(); return MissingNetwork("網路統計不可用"); }
    }
    private static (SensorValue, SensorValue) MissingNetwork(string reason) => (SensorValue.Missing(reason), SensorValue.Missing(reason));
    public void Dispose() { if (!disposed) { disposed = true; pdh.Dispose(); } }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Time; }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual; }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetLastInputInfo(ref LastInput input);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
}
