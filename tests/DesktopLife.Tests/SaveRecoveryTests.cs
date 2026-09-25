using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public sealed class SaveRecoveryTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"DesktopLifeRecoveryTests",Guid.NewGuid().ToString("N"));
    private static OrganismSnapshot Valid()=>new(){Pet=new(){LastSaveTime=DateTimeOffset.UtcNow}};
    [Fact]public void CorruptPrimaryRecoversAndKeepsOriginal()
    {
        var store=new OrganismStore(root);store.Save(Valid());store.Save(Valid());
        File.WriteAllText(store.PathName,"{broken");
        Assert.Equal(3,store.Load().SchemaVersion);Assert.NotNull(store.RecoveryMessage);
        Assert.Equal("{broken",File.ReadAllText(Directory.GetFiles(root,"*.damaged-*").Single()));
        Assert.Equal(3,store.Load().SchemaVersion);
    }
    [Fact]public void BrokenRecentBackupFallsBackToOlderBackup()
    {
        var store=new OrganismStore(root);for(var i=0;i<4;i++)store.Save(Valid());
        File.WriteAllText(store.PathName,"bad");File.WriteAllText(store.PathName+".bak","bad");
        Assert.Equal(3,store.Load().SchemaVersion);Assert.Contains(".bak.1",store.RecoveryMessage);
    }
    [Fact]public void FutureVersionCannotBeOverwrittenByRecovery()
    {
        var store=new OrganismStore(root);store.Save(Valid());store.Save(Valid());
        File.WriteAllText(store.PathName,"{\"SchemaVersion\":999}");
        Assert.Throws<NotSupportedException>(()=>store.Load());Assert.Throws<NotSupportedException>(()=>store.Save(Valid()));
        Assert.Contains("999",File.ReadAllText(store.PathName));
    }
    [Fact]public void MalformedSchemaAndMissingRecentFilesRecover()
    {
        var store=new OrganismStore(root);for(var i=0;i<4;i++)store.Save(Valid());
        File.WriteAllText(store.PathName,"{\"SchemaVersion\":\"invalid\"}");
        Assert.Equal(3,store.Load().SchemaVersion);
        File.Delete(store.PathName);File.Delete(store.PathName+".bak");
        Assert.True(store.Exists);
        Assert.Equal(3,store.LoadOrMigrate(new(),DateTimeOffset.UtcNow).SchemaVersion);
    }
    [Fact]public void ImportValidationAndRoundTripDoNotTouchIconBackup()
    {
        var store=new OrganismStore(root);store.Save(Valid());var export=Path.Combine(root,"export.json");store.Export(export);
        File.WriteAllText(Path.Combine(root,"desktop-icons-backup.json"),"unchanged");
        store.StageImport(export);Assert.True(store.ApplyPendingImport());Assert.False(store.ApplyPendingImport());
        Assert.Equal("unchanged",File.ReadAllText(Path.Combine(root,"desktop-icons-backup.json")));
        File.WriteAllText(export,"bad");Assert.ThrowsAny<Exception>(()=>store.StageImport(export));
        Assert.False(File.Exists(Path.Combine(root,"organism.import.json")));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
