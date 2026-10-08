using DesktopLife.Core;
using System.Text.Json;
using Xunit;

namespace DesktopLife.Tests;

public sealed class BackgroundSaveTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"DesktopLifeBackgroundSave",Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset epoch=new(2026,10,2,0,0,0,TimeSpan.Zero);
    private static OrganismSnapshot State(long revision)=>new(){Pet=new(){LastSaveTime=epoch,TotalRuntimeSeconds=revision},Learning=new(){PositiveRewards=revision}};

    [Fact]
    public void CaptureOwnsAllMutablePersistedStateAndPreservesCompleteGraph()
    {
        var snapshot=State(5) with{OtherCharacter=CharacterProfile.Create(PetAppearance.Girl,epoch),AdditionalCharacter=CharacterProfile.Create(PetAppearance.BorderCollie,epoch)};
        foreach(var learning in new[]{snapshot.Learning,snapshot.OtherCharacter!.Learning,snapshot.AdditionalCharacter!.Learning})
        {
            learning.ActionPreference[BodyAction.Sleep]=.1;learning.CategoryPreference[ActionCategory.Rest]=.2;learning.ContextAssociation["Unknown:Sleep"]=.3;
            learning.FlyWeights=new FlyInspiredBrain().ExportWeights();learning.ConnectomeFingerprint="hash";learning.ConnectomeDeltas=[.1];
            learning.Artworks.Add(new(DoodlePattern.Heart,null,3,4,1,epoch,Drawing:new([[new(1,1),new(2,2)]],40,40,1,0)));
            learning.LocationHabits.Add(new(Guid.NewGuid(),FurnitureUse.Sleep,.3,.2,1,1));
            learning.Adaptation.Traits[AdaptiveTrait.Social]=new(){Offset=.01,Evidence=1,LastEventMinute=1};
            learning.Transitions.Pairs.Add(new(LifeBehavior.Rest,LifeBehavior.Play,.1,1,1));
            learning.Milestones.Seen.Add(MilestoneKind.BoxSleep);learning.Milestones.LastReason="BoxSleep";
            learning.Room=new(){Items=[new(Guid.NewGuid(),FurnitureKind.Box,3,4)],Ball=new(12,13),Square=new(21,22),WorkArea=new(0,0,1920,1080)};
            learning.Companion.Memories.Add(new(epoch,"要保留"));learning.Companion.LastCare[CareKind.Play]=epoch;
            learning.Variation.DrawStylePreference[StyleFeature.Size]=.1;learning.Variation.TextStylePreference[StyleFeature.Direct]=.2;
            learning.Variation.MovementStylePreference[StyleFeature.Speed]=.3;learning.Variation.ShapePreference[PrimitiveKind.Line]=.4;
            learning.Variation.RecentOutputHistory.Add(new(BodyAction.Sleep,"sleep",null,epoch));
        }
        var expected=JsonSerializer.Serialize(snapshot);
        var frozen=FrozenOrganismSnapshot.Capture(snapshot);
        foreach(var learning in new[]{snapshot.Learning,snapshot.OtherCharacter!.Learning,snapshot.AdditionalCharacter!.Learning})
        {
            learning.ActionPreference.Clear();learning.CategoryPreference.Clear();learning.ContextAssociation.Clear();learning.FlyWeights![0][0]=.999;learning.ConnectomeDeltas![0]=999;
            learning.Artworks[0].Drawing!.Strokes[0][0]=new(39,39);learning.Artworks.Clear();learning.LocationHabits.Clear();
            learning.Adaptation.Traits[AdaptiveTrait.Social].Offset=.12;learning.Transitions.Pairs.Clear();learning.Milestones.Seen.Clear();
            learning.Room.Items.Clear();learning.Room.Ball=new(999,999);learning.Companion.Name="已改名";learning.Companion.Memories.Clear();learning.Companion.LastCare.Clear();
            learning.Variation.DrawStylePreference.Clear();learning.Variation.TextStylePreference.Clear();learning.Variation.MovementStylePreference.Clear();learning.Variation.ShapePreference.Clear();learning.Variation.RecentOutputHistory.Clear();
        }
        var store=new OrganismStore(root);frozen.SaveTo(store);
        Assert.Equal(expected,JsonSerializer.Serialize(store.Load()));
    }

    [Fact]
    public async Task OneWriterCoalescesWaitingSnapshotsAndAcknowledgesOnlyAfterDurableWrite()
    {
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var store=new OrganismStore(root);var writes=0;
        var queue=new OrganismSaveQueue(snapshot=>{if(Interlocked.Increment(ref writes)==1){entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));}snapshot.SaveTo(store);});
        var first=queue.Enqueue(FrozenOrganismSnapshot.Capture(State(1)));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var second=queue.Enqueue(FrozenOrganismSnapshot.Capture(State(2)));
        var third=queue.Enqueue(FrozenOrganismSnapshot.Capture(State(3)));
        Assert.False(first.IsCompleted);Assert.False(second.IsCompleted);Assert.False(third.IsCompleted);
        release.Set();var receipts=await Task.WhenAll(first,second,third).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(3,store.Load().Learning.PositiveRewards);Assert.Equal(2,writes);
        Assert.Equal(1,receipts[0].Revision);Assert.Equal(3,receipts[1].Revision);Assert.Equal(3,receipts[2].Revision);
        Assert.Equal(1,queue.CaptureMetrics().Coalesced);Assert.Equal(0,queue.CaptureMetrics().Failures);
    }

    [Fact]
    public async Task FailedWriteKeepsExistingFileAndNextRequestCanRetry()
    {
        var store=new OrganismStore(root);store.Save(State(1));var original=File.ReadAllText(store.PathName);
        var queue=new OrganismSaveQueue(store);
        using(var locked=new FileStream(store.PathName,FileMode.Open,FileAccess.Read,FileShare.Read))
        {await Assert.ThrowsAsync<IOException>(()=>queue.Enqueue(FrozenOrganismSnapshot.Capture(State(2))));Assert.Equal(original,File.ReadAllText(store.PathName));}
        await queue.Enqueue(FrozenOrganismSnapshot.Capture(State(3)));
        Assert.Equal(3,store.Load().Learning.PositiveRewards);Assert.Equal(1,queue.CaptureMetrics().Failures);
        Assert.Empty(Directory.GetFiles(root,"*.tmp-*"));
    }

    [Fact]
    public async Task FutureSchemaStillBlocksBackgroundSave()
    {
        var store=new OrganismStore(root);store.Save(State(1));var future=JsonSerializer.Serialize(new{SchemaVersion=OrganismSnapshot.CurrentSchemaVersion+1,history="preserve"});
        File.WriteAllText(store.PathName,future);var frozen=FrozenOrganismSnapshot.Capture(State(2));
        await Assert.ThrowsAsync<NotSupportedException>(()=>new OrganismSaveQueue(store).Enqueue(frozen));
        Assert.Equal(future,File.ReadAllText(store.PathName));
    }

    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
