using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class DualSaveTests
{
    [Fact] public void NewCharacterDoesNotCopyOldHistoryAndV6RoundTrips()
    {
        var root=Path.Combine(Path.GetTempPath(),"DesktopLifeDualTests",Guid.NewGuid().ToString("N"));
        try{
            var old=new OrganismSnapshot{SchemaVersion=4,Personality=new(){Curiosity=.91},Pet=new(){State=new(){Hunger=89},LastSaveTime=DateTimeOffset.UtcNow}};
            old.Learning.Companion.Bond=80;old.Learning.Companion.Name="原有角色";
            old.Learning.Room.Items.Add(new(Guid.NewGuid(),FurnitureKind.PetBed,100,200));
            Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"organism.json"),System.Text.Json.JsonSerializer.Serialize(old));
            var store=new OrganismStore(root);var migrated=store.Load();
            var other=CharacterProfile.CreateOther(migrated.Settings.PetAppearance,DateTimeOffset.UtcNow);
            Assert.Equal(80,migrated.Learning.Companion.Bond);Assert.Equal(15,other.Learning.Companion.Bond);
            Assert.NotEqual(migrated.Personality,other.Personality);Assert.NotEqual(migrated.Pet.State.Hunger,other.Pet.State.Hunger);
            Assert.Empty(other.Learning.Room.Items);Assert.Empty(other.Learning.Transitions.Pairs);
            store.Save(migrated with{OtherCharacter=other with{Position=new(350,200)},PrimaryPosition=new(100,200)});
            var loaded=store.Load();Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,loaded.SchemaVersion);Assert.Equal(old.Learning.Room.Items.Single(),loaded.Learning.Room.Items.Single());
            Assert.Equal(new RoomPoint(350,200),loaded.OtherCharacter!.Position);Assert.Equal(new RoomPoint(100,200),loaded.PrimaryPosition);
            CompanionCare.Apply(loaded.OtherCharacter.Pet.State,loaded.OtherCharacter.Learning.Companion,CareKind.Pet,DateTimeOffset.UtcNow);
            Assert.Equal(80,loaded.Learning.Companion.Bond);
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
