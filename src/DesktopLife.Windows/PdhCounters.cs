using System.Runtime.InteropServices;
using DesktopLife.Core;

namespace DesktopLife.Windows;

// Native PDH uses English counter paths even on Traditional Chinese Windows.
// This object is owned, sampled and disposed by a single worker.
internal sealed class PdhCounters : IDisposable
{
    private nint query;
    private readonly Dictionary<string, (nint Handle, uint Error)> counters = new();
    private uint collectStatus;
    private const uint DoubleFormat = 0x200 | 0x8000; // DOUBLE | NOCAP100
    public PdhCounters()
    {
        var status = PdhOpenQueryW(null, 0, out query);
        Add("read", @"\PhysicalDisk(_Total)\Disk Read Bytes/sec", status);
        Add("write", @"\PhysicalDisk(_Total)\Disk Write Bytes/sec", status);
        Add("gpu", @"\GPU Engine(*)\Utilization Percentage", status);
    }
    private void Add(string name, string path, uint queryStatus)
    {
        nint handle = 0;
        var status = queryStatus == 0 ? PdhAddEnglishCounterW(query, path, 0, out handle) : queryStatus;
        counters[name] = (handle, status);
    }
    public void Collect() => collectStatus = query == 0 ? 0xC0000BBC : PdhCollectQueryData(query);
    public SensorValue Read(string name)
    {
        var (handle, error) = counters[name];
        if (error != 0) return Failure(error);
        if (collectStatus != 0) return Failure(collectStatus);
        var result = PdhGetFormattedCounterValue(handle, DoubleFormat, out _, out var value);
        return result == 0 && value.Status <= 1 ? SensorValue.Available(value.Value) : Failure(result != 0 ? result : value.Status);
    }
    public SensorValue ReadGpu()
    {
        var (handle, error) = counters["gpu"];
        if (error != 0) return Failure(error);
        if (collectStatus != 0) return Failure(collectStatus);
        // Wildcard counter is read as an array; no fixed process-instance list.
        uint bytes = 0;
        var result = PdhGetFormattedCounterArrayW(handle, DoubleFormat, ref bytes, out _, 0);
        if (result != 0x800007D2 || bytes == 0) return Failure(result);
        if (bytes > 16 * 1024 * 1024) return SensorValue.Missing("GPU counter 超出 16 MB 預算");
        var buffer = Marshal.AllocHGlobal((int)bytes);
        try
        {
            result = PdhGetFormattedCounterArrayW(handle, DoubleFormat, ref bytes, out var count, buffer);
            if (result != 0) return Failure(result); // instance churn: retry next scheduled sample
            var stride = Marshal.SizeOf<CounterItem>();
            if ((ulong)count * (uint)stride > bytes) return SensorValue.Missing("GPU counter 格式錯誤");
            var values = new List<(string, double)>((int)count);
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<CounterItem>(buffer + i * stride);
                if (item.Value.Status <= 1)
                    values.Add((Marshal.PtrToStringUni(item.Name) ?? "", item.Value.Value));
            }
            var percent = SensorMath.GpuPercent(values);
            return percent is { } v ? SensorValue.Available(v) : SensorValue.Missing("GPU 無有效引擎樣本／暖機中");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static SensorValue Failure(uint code) => SensorValue.Missing($"PDH 0x{code:X8}／暖機或計數器不可用");
    public void Dispose() { if (query != 0) { PdhCloseQuery(query); query = 0; } }
    [StructLayout(LayoutKind.Explicit, Size = 16)] private struct CounterValue
    { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Value; }
    [StructLayout(LayoutKind.Sequential)] private struct CounterItem { public nint Name; public CounterValue Value; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQueryW(string? source, nuint data, out nint query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounterW(nint query, string path, nuint data, out nint counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(nint query);
    [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhGetFormattedCounterArrayW(nint counter, uint format, ref uint bytes, out uint count, nint buffer);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(nint query);
}
