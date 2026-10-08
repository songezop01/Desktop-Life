using DesktopLife.Core;

namespace DesktopLife.App;

// Dispatcher-owned counters. Disk-worker statistics are copied from the queue under its own lock.
internal sealed class RuntimePerformance
{
    private readonly LatencyHistogram dispatch=new(10,52),save=new(1,1002),capture=new(1,1002);
    private long dispatchOver50;
    private SaveQueueMetrics? persistenceBaseline;
    public long LearningDisplayRefreshes {get;set;}
    public long SkippedDiagnosticRefreshes {get;set;}
    public long HomeostasisDisplayRefreshes {get;set;}
    public long SensorDisplayRefreshes {get;set;}
    public long DebouncedSettingsWrites {get;set;}

    public void ObserveDispatchDelay(double milliseconds)
    {dispatch.Observe(milliseconds);if(milliseconds>50&&double.IsFinite(milliseconds))dispatchOver50++;}
    // Critical synchronous barriers retain their whole UI duration, including waiting for a prior queued write.
    public void ObserveSave(double milliseconds)=>save.Observe(milliseconds);
    public void ObserveSnapshotCapture(double milliseconds)=>capture.Observe(milliseconds);

    public void ResetObservation(SaveQueueMetrics? persistence=null)
    {dispatch.Reset();save.Reset();capture.Reset();dispatchOver50=0;persistenceBaseline=persistence;}

    public PerformanceSnapshot Capture(SaveQueueMetrics? persistence=null)
    {
        var baseline=persistenceBaseline;
        var observation=persistence is null?null:new PersistenceObservation(
            persistence.Requested-(baseline?.Requested??0),persistence.Written-(baseline?.Written??0),
            persistence.Coalesced-(baseline?.Coalesced??0),persistence.Failures-(baseline?.Failures??0),
            persistence.CompletedRequests-(baseline?.CompletedRequests??0),
            DifferenceMean(persistence.Written,persistence.WriteMeanMs,baseline?.Written??0,baseline?.WriteMeanMs??0),
            DifferenceMean(persistence.CompletedRequests,persistence.DurableMeanMs,baseline?.CompletedRequests??0,baseline?.DurableMeanMs??0),
            persistence.WriteMaxMs,persistence.DurableMaxMs,persistence.Busy);
        return new(dispatch.Count,dispatch.Mean,dispatch.Upper(.5),dispatch.Upper(.95),dispatch.Max,dispatchOver50,
            save.Count,save.Mean,save.Upper(.5),save.Upper(.95),save.Max,
            capture.Count,capture.Mean,capture.Upper(.5),capture.Upper(.95),capture.Max,
            LearningDisplayRefreshes,SkippedDiagnosticRefreshes,HomeostasisDisplayRefreshes,SensorDisplayRefreshes,DebouncedSettingsWrites,
            persistence,observation);
    }

    private static double DifferenceMean(long count,double mean,long baselineCount,double baselineMean)
        =>count>baselineCount?Math.Max(0,(count*mean-baselineCount*baselineMean)/(count-baselineCount)):0;

    // Fixed storage, all observed samples counted. Percentiles are nearest-rank upper bounds, not render FPS.
    private sealed class LatencyHistogram(double width,int buckets)
    {
        private readonly long[] counts=new long[buckets];
        public long Count {get;private set;}
        public double Total {get;private set;}
        public double Max {get;private set;}
        public double Mean=>Count==0?0:Total/Count;
        public void Observe(double ms)
        {
            if(!double.IsFinite(ms)||ms<0)return;
            Count++;Total+=ms;Max=Math.Max(Max,ms);
            counts[ms>=width*(counts.Length-1)?counts.Length-1:(int)(ms/width)]++;
        }
        public double Upper(double quantile)
        {
            if(Count==0)return 0;
            var rank=(long)Math.Ceiling(Count*quantile);long seen=0;
            for(var i=0;i<counts.Length;i++)
            {seen+=counts[i];if(seen>=rank)return i==counts.Length-1?Max:(i+1)*width;}
            return Max;
        }
        public void Reset(){Array.Clear(counts);Count=0;Total=0;Max=0;}
    }
}

internal sealed record PersistenceObservation(long Requested,long Written,long Coalesced,long Failures,long CompletedRequests,
    double WriteMeanMs,double DurableMeanMs,double LifetimeWriteMaxMs,double LifetimeDurableMaxMs,bool Busy);
internal sealed record PerformanceSnapshot(long DispatcherSamples,double DispatcherDelayMeanMs,double DispatcherDelayP50UpperMs,
    double DispatcherDelayP95UpperMs,double DispatcherDelayMaxMs,long DispatcherDelaysOver50Ms,
    long SaveCount,double SaveMeanMs,double SaveP50UpperMs,double SaveP95UpperMs,double SaveMaxMs,
    long SnapshotCaptureCount,double SnapshotCaptureMeanMs,double SnapshotCaptureP50UpperMs,double SnapshotCaptureP95UpperMs,double SnapshotCaptureMaxMs,
    long LearningDisplayRefreshes,long SkippedDiagnosticRefreshes,long HomeostasisDisplayRefreshes,long SensorDisplayRefreshes,long DebouncedSettingsWrites,
    SaveQueueMetrics? Persistence,PersistenceObservation? PersistenceObservation);
