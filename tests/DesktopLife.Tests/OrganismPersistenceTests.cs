using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public sealed class OrganismPersistenceTests:IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"DesktopLifeOrganismTests",Guid.NewGuid().ToString("N"));
    [Fact] public void LegacyMigrationPreservesStateAndPreferences()
    {var pet=new PetSnapshot{State=new(){Mood=91},LastSaveTime=DateTimeOffset.UtcNow,TotalRuntimeSeconds=42};new PetStateStore(Path.Combine(directory,"pet-state.json")).Save(pet);var l=new LearningState();l.ActionPreference[BodyAction.ChaseCursor]=.2;new LearningStore(Path.Combine(directory,"learning.json")).Save(l);var store=new OrganismStore(directory);var migrated=store.LoadOrMigrate(new(),DateTimeOffset.UtcNow);Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,migrated.SchemaVersion);Assert.Equal(pet,migrated.Pet);Assert.Equal(.2,migrated.Learning.ActionPreference[BodyAction.ChaseCursor]);store.Save(migrated);Assert.True(File.Exists(Path.Combine(directory,"pet-state.json")));}
    [Fact] public void FullCheckpointPreservesNeuralAndConnectomeData()
    {var store=new OrganismStore(directory);var s=store.LoadOrMigrate(new(),DateTimeOffset.UtcNow);s.Learning.FlyWeights=new FlyInspiredBrain().ExportWeights();s.Learning.ConnectomeFingerprint="dataset-hash";s.Learning.ConnectomeDeltas=[.1,0,-.1];s.Learning.PositiveRewards=33;store.Save(s);var restored=store.Load();Assert.Equal(s.Learning.FlyWeights.SelectMany(r=>r),restored.Learning.FlyWeights!.SelectMany(r=>r));Assert.Equal(s.Learning.ConnectomeDeltas,restored.Learning.ConnectomeDeltas);Assert.Equal(33,restored.Learning.PositiveRewards);Assert.Equal(s.Personality,restored.Personality);}
    [Fact] public void FutureSchemaIsNeverMigratedAsFreshPet()
    {Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"organism.json"),"{\"SchemaVersion\":99}");Assert.ThrowsAny<Exception>(()=>new OrganismStore(directory).LoadOrMigrate(new(),DateTimeOffset.UtcNow));Assert.Contains("99",File.ReadAllText(Path.Combine(directory,"organism.json")));}
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void SupportedLegacySchemasPreserveBothResidentsAndOriginalBackup(int schema)
    {
        var epoch=new DateTimeOffset(2026,9,29,0,0,0,TimeSpan.Zero);
        var cat=CharacterProfile.Create(PetAppearance.Cat,epoch);
        cat.Learning.Companion.Name="舊栗子";cat.Learning.Companion.Bond=81;
        cat.Learning.Companion.Remember(epoch,"原本的摸摸記憶");
        cat.Learning.ActionPreference[BodyAction.Groom]=.2;
        cat.Learning.PositiveRewards=33;
        cat.Learning.Room.Items.Add(new(Guid.NewGuid(),FurnitureKind.Box,100,200));
        cat.Learning.Artworks.Add(new(DoodlePattern.Heart,null,3,5,7,epoch,true));
        var girl=CharacterProfile.Create(PetAppearance.Girl,epoch) with{Position=new(456,789)};
        girl.Learning.Companion.Name="小花";girl.Learning.Companion.Bond=63;
        girl.Learning.Companion.Remember(epoch.AddMinutes(1),"她自己的閱讀記憶");
        girl.Learning.ActionPreference[BodyAction.WriteNote]=.17;
        girl.Learning.Punishments=4;
        var snapshot=new OrganismSnapshot{Pet=cat.Pet,Learning=cat.Learning,Personality=cat.Personality,
            Settings=new(){PetAppearance=PetAppearance.Cat,Presence=PresenceMode.Both},
            OtherCharacter=girl,PrimaryPosition=new(123,345)};
        snapshot.Validate();
        var original=JsonSerializer.Serialize(snapshot with{SchemaVersion=schema});
        var store=new OrganismStore(directory);Directory.CreateDirectory(directory);
        File.WriteAllText(store.PathName,original);
        var migrated=store.Load();
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,migrated.SchemaVersion);
        Assert.Equal(JsonSerializer.Serialize(snapshot),JsonSerializer.Serialize(migrated));
        Assert.Equal(original,File.ReadAllText(store.PathName));
        store.Save(migrated);
        Assert.Equal(original,File.ReadAllText(store.PathName+".bak"));
        Assert.Equal(JsonSerializer.Serialize(snapshot),JsonSerializer.Serialize(store.Load()));
    }
    [Fact]
    public void EarlySchemaFiveThirdResidentIsPreservedDuringMigration()
    {
        var epoch=new DateTimeOffset(2026,10,1,0,0,0,TimeSpan.Zero);
        var dog=CharacterProfile.Create(PetAppearance.BorderCollie,epoch) with{Position=new(300,400)};
        dog.Learning.Companion.Name="雪球";dog.Learning.Companion.Bond=47;
        dog.Learning.Companion.Remember(epoch,"牠自己的第一場遊戲");
        var snapshot=new OrganismSnapshot{Pet=new(){LastSaveTime=epoch},
            Settings=new(){PetAppearance=PetAppearance.Cat,Presence=PresenceMode.Both},
            OtherCharacter=CharacterProfile.Create(PetAppearance.Girl,epoch),AdditionalCharacter=dog};
        snapshot.Validate();
        var store=new OrganismStore(directory);Directory.CreateDirectory(directory);
        File.WriteAllText(store.PathName,JsonSerializer.Serialize(snapshot with{SchemaVersion=5}));
        var migrated=store.Load();store.Save(migrated);
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,migrated.SchemaVersion);
        Assert.Equal(JsonSerializer.Serialize(snapshot),JsonSerializer.Serialize(store.Load()));
        Assert.Equal("雪球",store.Load().AdditionalCharacter!.Learning.Companion.Name);
    }
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
