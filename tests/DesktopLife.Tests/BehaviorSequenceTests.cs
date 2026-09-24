using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class BehaviorSequenceTests
{
    private static void Run(BehaviorSequence s,double seconds,SequenceContext c){for(var i=0;i<(int)(seconds/.05);i++)s.Step(.05,c);}
    [Theory][InlineData(SequenceKind.Play)][InlineData(SequenceKind.Sleep)][InlineData(SequenceKind.Stroke)][InlineData(SequenceKind.Brush)][InlineData(SequenceKind.SelfGroom)]
    public void ChainsProgressCompleteAndRecover(SequenceKind kind)
    {var s=new BehaviorSequence(kind,new(),50,"target");Assert.False(s.Finished);Run(s,90,new(Reached:true,Contact:true,Rested:true,TargetSpeed:40));Assert.True(s.Finished);}
    [Theory][InlineData(SequenceKind.Play)][InlineData(SequenceKind.Sleep)][InlineData(SequenceKind.Stroke)][InlineData(SequenceKind.Brush)][InlineData(SequenceKind.SelfGroom)]
    public void CriticalNeedInterruptsAndWaitsForGround(SequenceKind kind)
    {var s=new BehaviorSequence(kind,new(),50,"target");Assert.True(s.Interrupt(BehaviorInterruptReason.CriticalNeed));Run(s,2,new(Grounded:false));Assert.False(s.Finished);Run(s,1,new());Assert.True(s.Finished);}
    [Theory][InlineData(SequenceKind.Play)][InlineData(SequenceKind.Sleep)][InlineData(SequenceKind.Stroke)][InlineData(SequenceKind.Brush)][InlineData(SequenceKind.SelfGroom)]
    public void TargetLossFallsBackOrRecovers(SequenceKind kind)
    {var s=new BehaviorSequence(kind,new(),50,"target");s.Step(.05,new(TargetExists:false));if(kind==SequenceKind.Sleep){Assert.Null(s.TargetId);Assert.Equal(BehaviorPhase.Inspect,s.Phase);}else Assert.Equal(BehaviorPhase.Recover,s.Phase);Run(s,90,new(Rested:true));Assert.True(s.Finished);}
    [Fact]public void CommitmentRejectsOpportunitiesAndSleepingIgnoresStimulus()
    {var s=new BehaviorSequence(SequenceKind.Sleep,new(),50);Assert.False(s.Interrupt(BehaviorInterruptReason.Opportunity));Run(s,10,new());Assert.False(s.Interrupt(BehaviorInterruptReason.Stimulus));Assert.Equal(BehaviorPhase.Sleep,s.Phase);Assert.True(s.Interrupt(BehaviorInterruptReason.Care));}
    [Fact]public void BondChangesHesitationDurationAndPurrWithoutPunishment()
    {var low=new BehaviorSequence(SequenceKind.Stroke,new(),10);var high=new BehaviorSequence(SequenceKind.Stroke,new(),90);Run(low,.5,new());Run(high,.5,new());Assert.Equal(BehaviorPhase.Hesitate,low.Phase);Assert.Equal(BehaviorPhase.Accept,high.Phase);Assert.True(high.Style.StrokeDuration>low.Style.StrokeDuration);Assert.True(high.Style.PurrChance>low.Style.PurrChance);Run(low,10,new());Assert.True(low.Finished);}
    [Fact]public void InterestRecoversAndMotionAttracts()
    {var i=new ToyInterest();Assert.True(i.Attraction("a",300)>i.Attraction("a",0));i.Finish("a");Assert.Equal(0,i.Attraction("a",300));i.Step(26);Assert.True(i.Attraction("a",0)>0);}
    [Fact]public void ApproachTimeoutIsBounded()
    {var s=new BehaviorSequence(SequenceKind.Play,new(),50,"a");Run(s,20,new());Assert.True(s.Finished);Assert.Equal(BehaviorInterruptReason.Safety,s.InterruptedBy);}
    [Fact]public void PersonalityHasBoundedVisibleRhythm()
    {var shy=SequenceStyle.From(new(){Timidity=1,Curiosity=1,Playfulness=0},15);var playful=SequenceStyle.From(new(){Timidity=0,Curiosity=0,Playfulness=1},15);Assert.True(shy.Reaction>playful.Reaction);Assert.True(shy.Watch>playful.Watch);Assert.True(shy.Interest<playful.Interest);Assert.InRange(shy.ApproachSpeed,.8,1);}
}
