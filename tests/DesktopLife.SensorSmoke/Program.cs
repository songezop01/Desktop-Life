using DesktopLife.Core;
using DesktopLife.Windows;

using var sensor = new WindowsEnvironmentSensor();
var snapshots = new List<EnvironmentState>();
for (var i = 0; i < 6; i++)
{
    snapshots.Add(sensor.Sample());
    if (i < 5) await Task.Delay(1000);
}
var last = snapshots[^1];
var values = new Dictionary<string, SensorValue>
{
    ["CPU %"] = last.CpuPercent, ["GPU %"] = last.GpuPercent, ["RAM %"] = last.RamPercent,
    ["Disk read B/s"] = last.DiskReadBytesPerSecond, ["Disk write B/s"] = last.DiskWriteBytesPerSecond,
    ["Network up B/s"] = last.NetworkUploadBytesPerSecond, ["Network down B/s"] = last.NetworkDownloadBytesPerSecond,
    ["Idle seconds"] = last.IdleSeconds, ["Mouse sampled px/s"] = last.MousePixelsPerSecond
};
foreach (var (name, value) in values) Console.WriteLine($"{name}: {(value.Value is { } v ? v.ToString("F2") : "N/A")} [{value.Status}]");
Console.WriteLine($"Sample time mean/max: {snapshots.Average(s => s.SampleMilliseconds):F2}/{snapshots.Max(s => s.SampleMilliseconds):F2} ms");
var invalid = values.Values.Any(v => v.Value is { } x && (!double.IsFinite(x) || x < 0));
var missing = values.Values.Count(v => v.Value is null);
Console.WriteLine($"Available: {values.Count-missing}/{values.Count}; no raw process or interface identifiers printed.");
return invalid || last.CpuPercent.Value is null || last.RamPercent.Value is null || (args.Contains("--require-all") && missing > 0) ? 1 : 0;
