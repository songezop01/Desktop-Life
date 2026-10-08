using System.Diagnostics;

namespace DesktopLife.Core;

public sealed record SaveReceipt(long Revision,double WriteMilliseconds,double DurableMilliseconds);
public sealed record SaveQueueMetrics(long Requested,long Written,long Coalesced,long Failures,long CompletedRequests,double WriteMeanMs,double WriteMaxMs,double DurableMeanMs,double DurableMaxMs,bool Busy);

/// <summary>A single writer with at most one waiting snapshot. Superseded requests await the newer durable write.</summary>
public sealed class OrganismSaveQueue
{
    private readonly object gate=new();
    private readonly Action<FrozenOrganismSnapshot> write;
    private Pending? pending;
    private bool running;
    private long requested,written,coalesced,failures,completed;
    private double totalWriteMs,maxWriteMs,totalDurableMs,maxDurableMs;
    private sealed record Waiter(TaskCompletionSource<SaveReceipt> Completion,long Started);
    private sealed record Pending(long Revision,FrozenOrganismSnapshot Snapshot,List<Waiter> Waiters);

    public OrganismSaveQueue(OrganismStore store):this(snapshot=>snapshot.SaveTo(store)){}
    public OrganismSaveQueue(Action<FrozenOrganismSnapshot> write)=>this.write=write;

    public Task<SaveReceipt> Enqueue(FrozenOrganismSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var waiter=new TaskCompletionSource<SaveReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(gate)
        {
            var waiters=pending?.Waiters??new();
            if(pending is not null)coalesced++;
            waiters.Add(new(waiter,Stopwatch.GetTimestamp()));pending=new(++requested,snapshot,waiters);
            if(!running){running=true;_ = Task.Run(Drain);}
        }
        return waiter.Task;
    }

    public SaveQueueMetrics CaptureMetrics()
    {lock(gate)return new(requested,written,coalesced,failures,completed,written==0?0:totalWriteMs/written,maxWriteMs,completed==0?0:totalDurableMs/completed,maxDurableMs,running);}

    private void Drain()
    {
        while(true)
        {
            Pending next;
            lock(gate)
            {
                if(pending is null){running=false;return;}
                next=pending;pending=null;
            }
            var started=Stopwatch.GetTimestamp();
            try
            {
                write(next.Snapshot);
                var ms=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                lock(gate){written++;totalWriteMs+=ms;maxWriteMs=Math.Max(maxWriteMs,ms);}
                foreach(var waiter in next.Waiters)
                {
                    var durableMs=Stopwatch.GetElapsedTime(waiter.Started).TotalMilliseconds;
                    lock(gate){completed++;totalDurableMs+=durableMs;maxDurableMs=Math.Max(maxDurableMs,durableMs);}
                    waiter.Completion.TrySetResult(new(next.Revision,ms,durableMs));
                }
            }
            catch(Exception error)
            {
                lock(gate)failures++;
                foreach(var waiter in next.Waiters)waiter.Completion.TrySetException(error);
            }
        }
    }
}
