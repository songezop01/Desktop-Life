using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class RoomActivityTests
{
    [Fact] public void NewFurnitureAndHabitsRoundTripWithoutCrossCharacterCopy()
    {
        var root=Path.Combine(Path.GetTempPath(),"DesktopLife09Tests",Guid.NewGuid().ToString("N"));
        try
        {
            var snapshot=new OrganismSnapshot{Pet=new(){LastSaveTime=DateTimeOffset.UtcNow},OtherCharacter=CharacterProfile.CreateOther(PetAppearance.Cat,DateTimeOffset.UtcNow)};
            var id=Guid.NewGuid();snapshot.Learning.Room.Items.Add(new(id,FurnitureKind.HumanBed,100,100));
            snapshot.Learning.LocationHabits.Add(new(id,FurnitureUse.Sleep,.5,.5,4,100));
            snapshot.OtherCharacter.Learning.LocationHabits.Add(new(id,FurnitureUse.Read,.2,.1,1,100));
            var store=new OrganismStore(root);store.Save(snapshot);var loaded=store.Load();
            Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,loaded.SchemaVersion);Assert.Equal(FurnitureKind.HumanBed,loaded.Learning.Room.Items.Single().Kind);
            Assert.Equal(FurnitureUse.Sleep,loaded.Learning.LocationHabits.Single().Use);
            Assert.Equal(FurnitureUse.Read,loaded.OtherCharacter!.Learning.LocationHabits.Single().Use);
            loaded.OtherCharacter.Learning.LocationHabits.Clear();Assert.Single(loaded.Learning.LocationHabits);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(FurnitureKind.HumanBed,FurnitureUse.Sleep)]
    [InlineData(FurnitureKind.DiningTable,FurnitureUse.Eat)]
    [InlineData(FurnitureKind.Desk,FurnitureUse.Draw)]
    [InlineData(FurnitureKind.Computer,FurnitureUse.Compute)]
    [InlineData(FurnitureKind.LegoBox,FurnitureUse.Build)]
    public void GirlHasHumanFurnitureUses(FurnitureKind kind,FurnitureUse use)=>Assert.True(FurnitureCompatibility.CanUse(PetAppearance.Girl,kind,use));
    [Theory]
    [InlineData(FurnitureKind.CatBowl,FurnitureUse.Eat,PetAppearance.Girl)]
    [InlineData(FurnitureKind.DiningTable,FurnitureUse.Eat,PetAppearance.Cat)]
    [InlineData(FurnitureKind.LegoBox,FurnitureUse.Build,PetAppearance.Cat)]
    [InlineData(FurnitureKind.Computer,FurnitureUse.Compute,PetAppearance.Cat)]
    public void WrongIdentityIsRejected(FurnitureKind kind,FurnitureUse use,PetAppearance who)=>Assert.False(FurnitureCompatibility.CanUse(who,kind,use));
    [Theory]
    [InlineData(FurnitureKind.HumanBed,FurnitureUse.Sleep)]
    [InlineData(FurnitureKind.Desk,FurnitureUse.Platform)]
    [InlineData(FurnitureKind.Bookshelf,FurnitureUse.Sleep)]
    public void HumanFurnitureRemainsShared(FurnitureKind kind,FurnitureUse use)=>Assert.True(FurnitureCompatibility.CanUse(PetAppearance.Cat,kind,use));
    [Fact] public void PairingPrefersDiningThenDesk()
    {
        var table=new RoomItem(Guid.NewGuid(),FurnitureKind.DiningTable,200,200);
        var desk=new RoomItem(Guid.NewGuid(),FurnitureKind.Desk,900,200);
        var chair=new RoomItem(Guid.NewGuid(),FurnitureKind.Chair,240,260);
        RoomItem[] room=[table,desk,chair];
        Assert.Equal(chair,RoomActivityPolicy.NearbyChair(table,room));
        Assert.Null(RoomActivityPolicy.NearbyChair(desk,room));
        Assert.True(RoomActivityPolicy.Rank(PetAppearance.Girl,table,RoomActivity.Feed,room)<RoomActivityPolicy.Rank(PetAppearance.Girl,desk,RoomActivity.Feed,room));
    }
    [Theory][InlineData(FurnitureKind.HumanBed)][InlineData(FurnitureKind.Sofa)][InlineData(FurnitureKind.Desk)]
    public void SharedZonesAllowCoexistenceWithoutSameReservation(FurnitureKind kind)
    {
        var item=new RoomItem(Guid.NewGuid(),kind,0,0);var world=new HouseholdOccupancy();
        var cat=FurnitureCompatibility.Reservation(item,PetAppearance.Cat);var girl=FurnitureCompatibility.Reservation(item,PetAppearance.Girl);
        Assert.NotEqual(cat,girl);Assert.True(world.TryAcquire(cat,PetAppearance.Cat));Assert.True(world.TryAcquire(girl,PetAppearance.Girl));
        Assert.False(world.TryAcquire(cat,PetAppearance.Girl));world.Release(PetAppearance.Cat);Assert.True(world.Available(cat,PetAppearance.Girl));
    }
    [Theory][InlineData(RoomActivity.Feed)][InlineData(RoomActivity.Draw)][InlineData(RoomActivity.HairCare)][InlineData(RoomActivity.Computer)][InlineData(RoomActivity.Lego)][InlineData(RoomActivity.Read)]
    public void GirlActivityRequiresApproachAndCompletesHumanPhases(RoomActivity activity)
    {
        var s=new BehaviorSequence(SequenceKind.Activity,new(),40,character:PetAppearance.Girl,activity:activity);
        for(var i=0;i<30;i++)s.Step(.1,new(Reached:false));
        Assert.Equal(BehaviorPhase.Approach,s.Phase);
        var seen=new HashSet<BehaviorPhase>();
        for(var i=0;i<400&&!s.Finished;i++){seen.Add(s.Phase);s.Step(.1,new(Reached:true));}
        Assert.True(s.Finished);Assert.Contains(BehaviorPhase.Sit,seen);Assert.Contains(BehaviorPhase.Work,seen);Assert.Contains(BehaviorPhase.Close,seen);
        Assert.DoesNotContain(BehaviorPhase.LickPaw,seen);Assert.DoesNotContain(BehaviorPhase.Crouch,seen);
    }
    [Fact] public void ActivityTargetRemovalDoesNotProduceSuccessfulCompletion()
    {
        var s=new BehaviorSequence(SequenceKind.Activity,new(),40,"desk",character:PetAppearance.Girl,activity:RoomActivity.Draw);
        s.Step(.1,new(TargetExists:false));for(var i=0;i<20;i++)s.Step(.1,new());
        Assert.True(s.Finished);Assert.Equal(BehaviorInterruptReason.TargetLost,s.InterruptedBy);
        Assert.Throws<ArgumentException>(()=>new BehaviorSequence(SequenceKind.Activity,new(),40,character:PetAppearance.Cat,activity:RoomActivity.Draw));
    }
}
