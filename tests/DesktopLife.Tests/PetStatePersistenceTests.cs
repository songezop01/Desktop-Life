using System.Text.Json;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;
public sealed class PetStatePersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"DesktopLifeStateTests",Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(root,"pet-state.json");
    private static PetSnapshot Snapshot() => new() { State = new() { Energy=73.25,Hunger=41.1 },LastSaveTime=DateTimeOffset.UtcNow,TotalRuntimeSeconds=1234 };
    [Fact] public void NewPetHasNoStoredState() => Assert.Null(new PetStateStore(PathName).Load());
    [Fact] public void StateAndRuntimeSurviveRestart()
    {
        var snapshot=Snapshot(); new PetStateStore(PathName).Save(snapshot);
        var loaded=new PetStateStore(PathName).Load(); Assert.Equal(snapshot,loaded);
    }
    [Fact] public void AtomicReplacementPreservesPreviousBackup()
    {
        var store=new PetStateStore(PathName); var before=Snapshot(); store.Save(before);
        store.Save(before with { State = before.State with { Mood=90 } });
        Assert.Equal(before,new PetStateStore(PathName+".bak").Load()); Assert.False(File.Exists(PathName+".tmp"));
    }
    [Theory] [InlineData("{}")] [InlineData("null")] [InlineData("{bad json")]
    public void DamagedSaveIsNotSilentlyReset(string text)
    {
        Directory.CreateDirectory(root); File.WriteAllText(PathName,text);
        Assert.ThrowsAny<Exception>(() => new PetStateStore(PathName).Load()); Assert.Equal(text,File.ReadAllText(PathName));
    }
    [Fact] public void FutureSchemaIsPreserved()
    {
        Directory.CreateDirectory(root); var text=JsonSerializer.Serialize(Snapshot() with { SchemaVersion=99 }); File.WriteAllText(PathName,text);
        Assert.Throws<InvalidDataException>(() => new PetStateStore(PathName).Load()); Assert.Equal(text,File.ReadAllText(PathName));
    }
    [Fact] public void IncompletePetStateFailsInsteadOfFillingDefaults()
    {
        Directory.CreateDirectory(root); File.WriteAllText(PathName,"{\"SchemaVersion\":1,\"State\":{},\"LastSaveTime\":\"2026-09-11T00:00:00Z\",\"TotalRuntimeSeconds\":0}");
        Assert.Throws<JsonException>(() => new PetStateStore(PathName).Load());
    }
    [Fact] public void InvalidStateCannotReplaceGoodSave()
    {
        var store=new PetStateStore(PathName); var good=Snapshot(); store.Save(good);
        Assert.Throws<InvalidDataException>(() => store.Save(good with { State = good.State with { Energy=-1 } })); Assert.Equal(good,store.Load());
    }
    [Fact] public void OfflineIntervalNotReappliedAfterCheckpoint()
    {
        var store=new PetStateStore(PathName); var now=DateTimeOffset.UtcNow; var original=Snapshot() with { LastSaveTime=now.AddDays(-7) }; store.Save(original);
        var loaded=store.Load()!; var session=new HomeostasisSession(loaded.State,loaded.TotalRuntimeSeconds);
        session.ApplyOffline(now-loaded.LastSaveTime); store.Save(original with { State=session.State,LastSaveTime=now });
        var restarted=store.Load()!; Assert.Equal(session.State,Homeostasis.ApplyOffline(restarted.State,now-restarted.LastSaveTime));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }
}
