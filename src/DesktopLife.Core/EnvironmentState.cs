namespace DesktopLife.Core;

public sealed record SensorValue(double? Value, string Status)
{
    public static SensorValue Available(double value) => double.IsFinite(value) && value >= 0
        ? new(value, "OK") : Missing("無效數值");
    public static SensorValue Missing(string reason) => new(null, reason);
}

// Only the latest aggregate snapshot is held in memory. No input contents or paths.
public sealed record EnvironmentState(
    DateTimeOffset Timestamp,
    SensorValue CpuPercent, SensorValue GpuPercent, SensorValue RamPercent,
    SensorValue DiskReadBytesPerSecond, SensorValue DiskWriteBytesPerSecond,
    SensorValue NetworkUploadBytesPerSecond, SensorValue NetworkDownloadBytesPerSecond,
    SensorValue IdleSeconds, SensorValue MousePixelsPerSecond,
    double SampleMilliseconds);

public interface IEnvironmentSensor : IDisposable
{
    EnvironmentState Sample();
}

public static class SensorMath
{
    public static double? CpuPercent(ulong oldIdle, ulong oldKernel, ulong oldUser, ulong idle, ulong kernel, ulong user)
    {
        if (idle < oldIdle || kernel < oldKernel || user < oldUser) return null;
        var total = (double)(kernel - oldKernel) + (user - oldUser); // kernel includes idle
        var inactive = (double)(idle - oldIdle);
        return total <= 0 || inactive > total ? null : Math.Clamp(100 * (total - inactive) / total, 0, 100);
    }
    public static double? Rate(long previous, long current, double seconds) =>
        previous < 0 || current < previous || !double.IsFinite(seconds) || seconds <= 0 || seconds > 10
            ? null : (current - previous) / seconds;
    public static double IdleSeconds(uint now, uint lastInput) => unchecked(now - lastInput) / 1000.0;
    public static double? MouseSpeed(int oldX, int oldY, int x, int y, double seconds) =>
        !double.IsFinite(seconds) || seconds <= 0 || seconds > 10 ? null :
        Math.Sqrt(Math.Pow((double)x - oldX, 2) + Math.Pow((double)y - oldY, 2)) / seconds;

    // Sum process contributions on the same physical engine, then report the busiest engine.
    // Instance identifiers are discarded immediately after aggregation.
    public static double? GpuPercent(IEnumerable<(string Instance, double Value)> samples)
    {
        var engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (instance, value) in samples)
        {
            var start = instance.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
            if (start < 0 || !double.IsFinite(value) || value < 0) continue;
            var key = instance[start..];
            engines[key] = engines.GetValueOrDefault(key) + value;
        }
        return engines.Count == 0 ? null : Math.Clamp(engines.Values.Max(), 0, 100);
    }
}
