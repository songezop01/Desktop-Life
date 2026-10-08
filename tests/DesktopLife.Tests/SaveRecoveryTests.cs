using DesktopLife.Core;
using System.Text.Json;
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
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,store.Load().SchemaVersion);Assert.NotNull(store.RecoveryMessage);
        Assert.Equal("{broken",File.ReadAllText(Directory.GetFiles(root,"*.damaged-*").Single()));
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,store.Load().SchemaVersion);
    }
    [Fact]public void BrokenRecentBackupFallsBackToOlderBackup()
    {
        var store=new OrganismStore(root);for(var i=0;i<4;i++)store.Save(Valid());
        File.WriteAllText(store.PathName,"bad");File.WriteAllText(store.PathName+".bak","bad");
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,store.Load().SchemaVersion);Assert.Contains(".bak.1",store.RecoveryMessage);
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
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,store.Load().SchemaVersion);
        File.Delete(store.PathName);File.Delete(store.PathName+".bak");
        Assert.True(store.Exists);
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,store.LoadOrMigrate(new(),DateTimeOffset.UtcNow).SchemaVersion);
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
    [Fact]
    public void NextUnsupportedVersionBlocksReadSaveExportAndImportDespiteLegacyBackup()
    {
        var store=new OrganismStore(root);Directory.CreateDirectory(root);
        var old=JsonSerializer.Serialize(Valid() with{SchemaVersion=5});
        File.WriteAllText(store.PathName,old);store.Save(store.Load());
        var future=JsonSerializer.Serialize(new{SchemaVersion=OrganismSnapshot.CurrentSchemaVersion+1,AdditionalCharacter=new{UnknownHistory="preserve"}});
        File.WriteAllText(store.PathName,future);
        var exported=Path.Combine(root,"export.json");File.WriteAllText(exported,"keep existing export");
        var pending=Path.Combine(root,"organism.import.json");File.WriteAllText(pending,future);
        Assert.Throws<NotSupportedException>(()=>store.Load());
        Assert.Throws<NotSupportedException>(()=>store.Save(Valid()));
        Assert.Throws<NotSupportedException>(()=>store.Export(exported));
        Assert.Throws<NotSupportedException>(()=>store.StageImport(store.PathName));
        Assert.Throws<NotSupportedException>(()=>store.ApplyPendingImport());
        Assert.Equal(future,File.ReadAllText(store.PathName));
        Assert.Equal(old,File.ReadAllText(store.PathName+".bak"));
        Assert.Equal(future,File.ReadAllText(pending));
        Assert.Equal("keep existing export",File.ReadAllText(exported));
        Assert.Empty(Directory.GetFiles(root,"*.damaged-*"));
        Assert.Null(store.RecoveryMessage);
    }
    [Fact]
    public void OutdatedSnapshotCannotDowngradeAnExistingThreeResidentSave()
    {
        var epoch=new DateTimeOffset(2026,10,1,0,0,0,TimeSpan.Zero);
        var dog=CharacterProfile.Create(PetAppearance.BorderCollie,epoch);
        dog.Learning.Companion.Remember(epoch,"別覆寫這段記憶");
        var snapshot=Valid() with{OtherCharacter=CharacterProfile.Create(PetAppearance.Girl,epoch),AdditionalCharacter=dog};
        var store=new OrganismStore(root);store.Save(snapshot);
        var before=File.ReadAllText(store.PathName);
        Assert.Throws<InvalidDataException>(()=>store.Save(snapshot with{SchemaVersion=5,AdditionalCharacter=null}));
        Assert.Equal(before,File.ReadAllText(store.PathName));
        Assert.Single(store.Load().AdditionalCharacter!.Learning.Companion.Memories);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
