using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public sealed class OrganismPersistenceTests:IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"DesktopLifeOrganismTests",Guid.NewGuid().ToString("N"));
    [Fact] public void LegacyMigrationPreservesStateAndPreferences()
    {var pet=new PetSnapshot{State=new(){Mood=91},LastSaveTime=DateTimeOffset.UtcNow,TotalRuntimeSeconds=42};new PetStateStore(Path.Combine(directory,"pet-state.json")).Save(pet);var l=new LearningState();l.ActionPreference[BodyAction.ChaseCursor]=.2;new LearningStore(Path.Combine(directory,"learning.json")).Save(l);var store=new OrganismStore(directory);var migrated=store.LoadOrMigrate(new(),DateTimeOffset.UtcNow);Assert.Equal(3,migrated.SchemaVersion);Assert.Equal(pet,migrated.Pet);Assert.Equal(.2,migrated.Learning.ActionPreference[BodyAction.ChaseCursor]);store.Save(migrated);Assert.True(File.Exists(Path.Combine(directory,"pet-state.json")));}
    [Fact] public void FullCheckpointPreservesNeuralAndConnectomeData()
    {var store=new OrganismStore(directory);var s=store.LoadOrMigrate(new(),DateTimeOffset.UtcNow);s.Learning.FlyWeights=new FlyInspiredBrain().ExportWeights();s.Learning.ConnectomeFingerprint="dataset-hash";s.Learning.ConnectomeDeltas=[.1,0,-.1];s.Learning.PositiveRewards=33;store.Save(s);var restored=store.Load();Assert.Equal(s.Learning.FlyWeights.SelectMany(r=>r),restored.Learning.FlyWeights!.SelectMany(r=>r));Assert.Equal(s.Learning.ConnectomeDeltas,restored.Learning.ConnectomeDeltas);Assert.Equal(33,restored.Learning.PositiveRewards);Assert.Equal(s.Personality,restored.Personality);}
    [Fact] public void FutureSchemaIsNeverMigratedAsFreshPet()
    {Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"organism.json"),"{\"SchemaVersion\":99}");Assert.ThrowsAny<Exception>(()=>new OrganismStore(directory).LoadOrMigrate(new(),DateTimeOffset.UtcNow));Assert.Contains("99",File.ReadAllText(Path.Combine(directory,"organism.json")));}
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
