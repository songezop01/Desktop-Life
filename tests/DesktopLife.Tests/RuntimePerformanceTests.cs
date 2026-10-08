using DesktopLife.App;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class RuntimePerformanceTests
{
    [Fact]
    public void PercentilesCountEverySampleAndKeepRareStallsVisible()
    {
        var performance=new RuntimePerformance();
        for(var i=0;i<99;i++)performance.ObserveDispatchDelay(5);
        performance.ObserveDispatchDelay(2000);
        var result=performance.Capture();
        Assert.Equal(100,result.DispatcherSamples);Assert.Equal(10,result.DispatcherDelayP50UpperMs);
        Assert.Equal(10,result.DispatcherDelayP95UpperMs);Assert.Equal(2000,result.DispatcherDelayMaxMs);
        Assert.Equal(1,result.DispatcherDelaysOver50Ms);
        performance.ResetObservation();performance.ObserveDispatchDelay(2000);
        Assert.Equal(2000,performance.Capture().DispatcherDelayP95UpperMs);
    }

    [Fact]
    public void SnapshotUiLatencyAndSynchronousBarrierHaveSeparateDistributions()
    {
        var performance=new RuntimePerformance();
        performance.ObserveSnapshotCapture(2.1);performance.ObserveSnapshotCapture(3.1);
        performance.ObserveSave(120.2);
        var result=performance.Capture();
        Assert.Equal(2,result.SnapshotCaptureCount);Assert.Equal(2.6,result.SnapshotCaptureMeanMs,8);
        Assert.Equal(3,result.SnapshotCaptureP50UpperMs);Assert.Equal(4,result.SnapshotCaptureP95UpperMs);
        Assert.Equal(1,result.SaveCount);Assert.Equal(120.2,result.SaveMaxMs);
    }

    [Fact]
    public void SteadyQueueMetricsSubtractSetupWithoutMislabelingLifetimeMaxima()
    {
        var performance=new RuntimePerformance();
        performance.ObserveSave(150);performance.SkippedDiagnosticRefreshes=10;
        performance.ResetObservation(new(2,2,0,0,2,100,150,105,160,false));
        performance.ObserveSnapshotCapture(4);
        var result=performance.Capture(new(5,4,1,0,5,75,150,72,160,false));
        var queue=result.PersistenceObservation!;
        Assert.Equal(3,queue.Requested);Assert.Equal(2,queue.Written);Assert.Equal(1,queue.Coalesced);
        Assert.Equal(3,queue.CompletedRequests);Assert.Equal(50,queue.WriteMeanMs);Assert.Equal(50,queue.DurableMeanMs);
        Assert.Equal(150,queue.LifetimeWriteMaxMs);Assert.Equal(0,result.SaveCount);
        Assert.Equal(10,result.SkippedDiagnosticRefreshes);Assert.Equal(1,result.SnapshotCaptureCount);
    }

    [Fact]
    public void InvalidTimingDoesNotCorruptHistograms()
    {
        var performance=new RuntimePerformance();
        foreach(var ms in new[]{double.NaN,double.PositiveInfinity,-1d})
        {performance.ObserveDispatchDelay(ms);performance.ObserveSave(ms);performance.ObserveSnapshotCapture(ms);}
        var result=performance.Capture();
        Assert.Equal(0,result.DispatcherSamples);Assert.Equal(0,result.SaveCount);Assert.Equal(0,result.SnapshotCaptureCount);
        Assert.Equal(0,result.DispatcherDelaysOver50Ms);Assert.Equal(0,result.DispatcherDelayP95UpperMs);
    }
}
