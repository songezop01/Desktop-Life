using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class CompanionTests
{
    [Fact]public void FeedingIsBoundedAndCannotBeSpamFarmed()
    {
        var memory=new CompanionState();var now=DateTimeOffset.UtcNow;
        var fed=CompanionCare.Apply(new PetState{Hunger=30},memory,CareKind.Feed,now);
        Assert.True(fed.Accepted);Assert.Equal(2,fed.State.Hunger);Assert.Single(memory.Memories);
        var bond=memory.Bond;
        Assert.False(CompanionCare.Apply(fed.State,memory,CareKind.Feed,now.AddSeconds(1)).Accepted);
        Assert.False(CompanionCare.Apply(fed.State,memory,CareKind.Feed,now.AddMinutes(2)).Accepted);
        Assert.Equal(bond,memory.Bond);
    }
    [Fact]public void SleepRestoresAndPlayRelievesBoredomWithoutHardware()
    {
        var p=new PetState();var sleep=CompanionCare.Step(p,TimeSpan.FromSeconds(10),BodyAction.Sleep,false);
        var play=CompanionCare.Step(p,TimeSpan.FromSeconds(10),BodyAction.PlayToy,true);
        Assert.True(sleep.Energy>p.Energy);Assert.True(sleep.Fatigue<p.Fatigue);
        Assert.True(play.Boredom<p.Boredom);Assert.True(play.Energy<p.Energy);
        Assert.True(play.Hunger>p.Hunger);
    }
    [Fact]public void TiredPetCanDeclinePlay()
    {
        var memory=new CompanionState();var p=new PetState{Energy=10};
        var result=CompanionCare.Apply(p,memory,CareKind.Play,DateTimeOffset.UtcNow);
        Assert.False(result.Accepted);Assert.Equal(p,result.State);Assert.Empty(memory.Memories);
    }
    [Fact]public void OldLearningLoadsAndCareMemoriesSurviveCheckpoint()
    {
        var original=new LearningState();var json=JsonSerializer.SerializeToNode(original)!.AsObject();json.Remove("Companion");
        var old=JsonSerializer.Deserialize<LearningState>(json.ToJsonString())!;old.Validate();
        var now=DateTimeOffset.UtcNow;
        for(var i=0;i<40;i++)CompanionCare.Apply(new PetState(),old.Companion,CareKind.Pet,now.AddMinutes(i));
        var copy=JsonSerializer.Deserialize<LearningState>(JsonSerializer.Serialize(old))!;copy.Validate();
        Assert.Equal(30,copy.Companion.Memories.Count);Assert.Equal(40,copy.Companion.CareCount);
        Assert.Equal(old.Companion.Bond,copy.Companion.Bond);
        Assert.False(CompanionCare.Apply(new(),copy.Companion,CareKind.Pet,now.AddMinutes(39)).Accepted);
    }
    [Fact]public void EatingRequiresUserProvidedFood()
    {
        var context=new EnvironmentContext(new(),new(),null,LearningContext.QuietDesktop,Affordances:new(true,true,true,true,true));
        Assert.False(new PetAction(BodyAction.Eat).CanExecute(context));
        Assert.DoesNotContain(new ActionSelection().Evaluate(context,new UtilityBrain().Evaluate(context),new RewardLearning()),s=>s.Action==BodyAction.Eat);
    }
    [Theory][InlineData(BodyAction.Eat)][InlineData(BodyAction.Groom)][InlineData(BodyAction.Nuzzle)][InlineData(BodyAction.Greet)]
    public void CareHasAnimatedDistinctPose(BodyAction action)
    {Assert.NotEqual(PetPose.At(BodyAction.Idle,.2),PetPose.At(action,.2));Assert.NotEqual(PetPose.At(action,.2),PetPose.At(action,.6));}
    [Fact]public void LongAbsenceIsBoundedRestAndDoesNotPunishBond()
    {
        var session=new HomeostasisSession(new PetState());session.ApplyCompanionOffline(TimeSpan.FromDays(30));
        var bounded=new HomeostasisSession(new PetState());bounded.ApplyCompanionOffline(TimeSpan.FromHours(8));
        Assert.Equal(bounded.State,session.State);Assert.True(session.State.Energy>65);Assert.Equal(65,session.State.Mood);
        session.State.Validate();
    }
    [Fact]public void InvalidCompanionCannotOverwriteSave()
    {Assert.Throws<InvalidDataException>(()=>new CompanionState{Bond=double.NaN}.Validate());Assert.Throws<InvalidDataException>(()=>new CompanionState{Name="\n"}.Validate());}
}
