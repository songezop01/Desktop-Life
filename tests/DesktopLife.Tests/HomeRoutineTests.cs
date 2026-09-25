using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class HomeRoutineTests
{
    [Fact]public void OneHourRoutineHasVarietyAndCooldown()
    {
        var routine=new HomeRoutine(70);var counts=new Dictionary<BodyAction,int>();var last=-100d;
        for(var t=0;t<3600;t++)
        {
            routine.Advance(1,t%240<60?BodyAction.Sleep:BodyAction.Walk);
            var action=routine.Opportunity(new(){Energy=75,Fatigue=30,Boredom=70},new(),FurnitureUse.Hide|FurnitureUse.Scratch|FurnitureUse.Observe|FurnitureUse.Rest,false,false);
            if(action is not {} a)continue;
            Assert.True(t-last>=30);last=t;counts[a]=counts.GetValueOrDefault(a)+1;
        }
        Assert.Contains(BodyAction.Hide,counts.Keys);Assert.Contains(BodyAction.Stretch,counts.Keys);Assert.Contains(BodyAction.ObserveCursor,counts.Keys);Assert.Contains(BodyAction.Sleep,counts.Keys);
        Assert.InRange(counts.Values.Sum(),40,120);Assert.All(counts.Values,c=>Assert.InRange(c,1,65));
    }
    [Fact]public void EditingAndUrgentNeedsOverrideFurnitureOpportunity()
    {
        var r=new HomeRoutine(3);for(var i=0;i<40;i++)r.Advance(1,BodyAction.Idle);
        Assert.Null(r.Opportunity(new(),new(),FurnitureUse.Hide,false,true));
        Assert.Equal(BodyAction.Sleep,r.Opportunity(new(){Fatigue=95},new(),FurnitureUse.Hide,false,false));
        r.Finished(SequenceKind.Play);Assert.True(r.PlayCooling);for(var i=0;i<80;i++)r.Advance(1,BodyAction.Idle);Assert.False(r.PlayCooling);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void WakeVariantsEndWithoutLoop(int variant)
    {
        var s=new BehaviorSequence(SequenceKind.Sleep,new(),50,"bed",FurnitureKind.PetBed,1,variant);var seen=new HashSet<BehaviorPhase>();
        for(var i=0;i<1800;i++){seen.Add(s.Phase);s.Step(.05,new(Reached:true,Rested:true));}
        Assert.True(s.Finished);Assert.Contains(BehaviorPhase.Stretch,seen);
        if(variant==1)Assert.Contains(BehaviorPhase.WashFace,seen);if(variant==2)Assert.Contains(BehaviorPhase.Watch,seen);
    }
    [Fact]public void FinishedPlayWatchesBallBeforeLeaving()
    {var s=new BehaviorSequence(SequenceKind.Play,new(),50,"ball");var seen=new HashSet<BehaviorPhase>();for(var i=0;i<1000;i++){seen.Add(s.Phase);s.Step(.05,new(Reached:true,Contact:true));}Assert.Contains(BehaviorPhase.WatchBall,seen);Assert.True(s.Finished);}
}
