using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class SensorTests
{
    [Fact] public void CpuKernelIncludesIdle() => Assert.Equal(75, SensorMath.CpuPercent(10,100,100,35,150,150));
    [Fact] public void CpuResetDoesNotProduceSpike() => Assert.Null(SensorMath.CpuPercent(10,100,100,0,50,50));
    [Fact] public void CpuNoElapsedCountersAreMissing() => Assert.Null(SensorMath.CpuPercent(10,100,100,10,100,100));
    [Fact] public void RateUsesActualElapsedTime() => Assert.Equal(200, SensorMath.Rate(100,600,2.5));
    [Theory] [InlineData(-1,100,1)] [InlineData(100,0,1)] [InlineData(0,100,0)] [InlineData(0,100,60)]
    public void ResetAndSuspendInvalidateRates(long before, long after, double seconds) => Assert.Null(SensorMath.Rate(before,after,seconds));
    [Fact] public void IdleTickWrapIsSupported() => Assert.Equal(0.048, SensorMath.IdleSeconds(32,0xfffffff0),6);
    [Fact] public void MouseDistanceSupportsNegativeMonitorCoordinates() => Assert.Equal(5,SensorMath.MouseSpeed(-5,-5,-2,-1,1));
    [Fact] public void SuspendDoesNotProduceMouseActivity() => Assert.Null(SensorMath.MouseSpeed(0,0,100,100,100));
    [Fact] public void GpuAggregatesProcessesButDoesNotSumDifferentEngines()
    {
        var values = new[] { ("pid_1_luid_a_phys_0_eng_0_engtype_3D", 30.0), ("pid_2_luid_a_phys_0_eng_0_engtype_3D", 40.0), ("pid_1_luid_a_phys_0_eng_1_engtype_Copy", 60.0), ("pid_1_luid_b_phys_0_eng_0_engtype_3D", 20.0) };
        Assert.Equal(70, SensorMath.GpuPercent(values));
    }
    [Fact] public void GpuMissingIsDifferentFromIdle() { Assert.Null(SensorMath.GpuPercent([])); Assert.Equal(0,SensorMath.GpuPercent([("pid_1_luid_a_phys_0_eng_0",0)])); }
    [Fact] public void GpuRejectsNaNAndClamps() { Assert.Null(SensorMath.GpuPercent([("pid_1_luid_a",double.NaN)])); Assert.Equal(100,SensorMath.GpuPercent([("pid_1_luid_a",120)])); }
    [Fact] public void InvalidMeasurementNeverBecomesZero() { Assert.Null(SensorValue.Available(double.NaN).Value); Assert.Null(SensorValue.Available(-1).Value); Assert.Equal(0,SensorValue.Available(0).Value); }
}
