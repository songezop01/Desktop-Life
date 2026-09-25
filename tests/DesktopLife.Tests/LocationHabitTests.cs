using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class LocationHabitTests
{
    [Fact]public void InvalidHabitsCannotConsumeTheBoundedList()
    {
        var id=Guid.NewGuid();var state=new LearningState{LocationHabits=[new(id,FurnitureUse.Rest,double.NaN,1,1,1),new(Guid.Empty,FurnitureUse.Rest,1,1,1,1),new(id,FurnitureUse.Rest,1,1,1,1),new(id,FurnitureUse.Rest,1,1,1,1)]};
        state.Validate();Assert.Single(state.LocationHabits);
    }
    [Fact]public void MalformedEnclosingJsonIsNotSilentlyReset()
    {Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<LearningState>("{\"LocationHabits\":["));}
    private static readonly DateTimeOffset Now=new(2026,9,25,0,0,0,TimeSpan.Zero);
    [Fact]public void HabitGrowthIsBoundedAndOldItemsAreEvicted()
    {
        var p=new RestSpotPreference();var id=Guid.NewGuid();
        for(var i=0;i<11000;i++)p.Learn(id,FurnitureUse.Sleep,Now);
        var habit=Assert.Single(p.Habits);Assert.Equal(1,habit.Familiarity);Assert.Equal(1,habit.Preference);Assert.Equal(10000,habit.Uses);
        for(var i=0;i<50;i++)p.Learn(Guid.NewGuid(),FurnitureUse.Rest,Now.AddMinutes(i));
        Assert.Equal(32,p.Habits.Count);Assert.All(p.Habits,h=>Assert.True(h.Valid));
    }
    [Fact]public void RecentUsePenaltyThenPreferenceDecay()
    {
        var p=new RestSpotPreference();var id=Guid.NewGuid();for(var i=0;i<8;i++)p.Learn(id,FurnitureUse.Sleep,Now);
        var immediate=p.HabitWeight(id,Now,new());var tomorrow=p.HabitWeight(id,Now.AddDays(1),new());
        Assert.True(immediate<tomorrow);Assert.True(p.HabitWeight(id,Now.AddDays(30),new())<tomorrow);
        Assert.True(p.HabitWeight(Guid.NewGuid(),Now,new(){Curiosity=1})>p.HabitWeight(Guid.NewGuid(),Now,new(){Curiosity=0}));
    }
    [Fact]public void FavoriteIsNotPermanentLock()
    {
        var p=new RestSpotPreference();var favorite=Guid.NewGuid();for(var i=0;i<20;i++)p.Learn(favorite,FurnitureUse.Sleep,Now.AddDays(-1));
        RestSpot[] spots=[new(favorite.ToString(),FurnitureKind.Box,300,800),new(Guid.NewGuid().ToString(),FurnitureKind.PetBed,350,800)];
        var random=new Random(27);var counts=new Dictionary<string,int>();
        for(var i=0;i<1000;i++){var chosen=p.ChooseHome(spots,new(),50,300,null,Now,20,random)!;counts[chosen.Id]=counts.GetValueOrDefault(chosen.Id)+1;}
        Assert.Equal(2,counts.Count);Assert.All(counts.Values,c=>Assert.InRange(c,100,900));
    }
    [Fact]public void FatigueChoosesNearbyReachableAlternatives()
    {
        var p=new RestSpotPreference();RestSpot[] spots=[new("near",FurnitureKind.Cushion,100,800),new("far",FurnitureKind.PetBed,1400,800)];
        Assert.Equal("near",p.ChooseHome(spots,new(),50,100,null,Now,95,new(1))!.Id);
        Assert.Null(p.ChooseHome([],new(),50,100,null,Now,20,new(1)));
    }
    [Fact]public void ForgetOnlyClearsHabitsAndPruneRemovesDeletedIds()
    {
        var state=new LearningState();state.Companion.Name="栗子";state.Companion.Bond=72;state.Companion.Remember(Now,"真正發生的照顧");state.ActionPreference[BodyAction.Groom]=.2;
        var p=new RestSpotPreference();p.Attach(state.LocationHabits);var id=Guid.NewGuid();p.Learn(id,FurnitureUse.Hide,Now);p.Prune([]);Assert.Empty(state.LocationHabits);
        p.Learn(id,FurnitureUse.Sleep,Now);p.Forget();Assert.Empty(state.LocationHabits);Assert.Equal(72,state.Companion.Bond);Assert.Single(state.Companion.Memories);Assert.Equal("栗子",state.Companion.Name);Assert.Equal(.2,state.ActionPreference[BodyAction.Groom]);
    }
    [Fact]public void V06SaveMigratesWithoutHabitsAndRetainsIdentity()
    {
        var root=Path.Combine(Path.GetTempPath(),"HomeCompatibility",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var id=Guid.NewGuid();var snapshot=new OrganismSnapshot{Pet=new(){LastSaveTime=Now}};snapshot.Learning.Room.Items.Add(new(id,FurnitureKind.Cushion,100,200));
            var node=JsonNode.Parse(JsonSerializer.Serialize(snapshot))!;node["SchemaVersion"]=2;node["Learning"]!.AsObject().Remove("LocationHabits");
            File.WriteAllText(Path.Combine(root,"organism.json"),node.ToJsonString());var store=new OrganismStore(root);var migrated=store.Load();
            Assert.Equal(3,migrated.SchemaVersion);Assert.Empty(migrated.Learning.LocationHabits);Assert.Equal(id,Assert.Single(migrated.Learning.Room.Items).Id);
            var p=new RestSpotPreference();p.Attach(migrated.Learning.LocationHabits);p.Learn(id,FurnitureUse.Sleep,Now);store.Save(migrated);
            Assert.Equal(id,Assert.Single(store.Load().Learning.LocationHabits).FurnitureId);
        }finally{Directory.Delete(root,true);}
    }
    [Theory][InlineData("null")][InlineData("{\"unexpected\":true}")][InlineData("[{\"FurnitureId\":\"invalid\"}]")]
    public void OptionalHabitCorruptionDefaultsSafely(string bad)
    {
        var node=JsonNode.Parse(JsonSerializer.Serialize(new LearningState()))!;node["LocationHabits"]=JsonNode.Parse(bad);
        var state=JsonSerializer.Deserialize<LearningState>(node.ToJsonString())!;state.Validate();Assert.Empty(state.LocationHabits);
    }
}
