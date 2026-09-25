using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class HomeSequenceTests
{
    [Theory][InlineData(SequenceKind.Box)][InlineData(SequenceKind.Scratch)][InlineData(SequenceKind.Observe)]
    public void HomeCommitmentIgnoresCasualStimulusButAcceptsCare(SequenceKind kind)
    {var s=new BehaviorSequence(kind,new(),50,"home");for(var i=0;i<60;i++)s.Step(.05,new(Reached:true));Assert.False(s.Interrupt(BehaviorInterruptReason.Stimulus));Assert.True(s.Interrupt(BehaviorInterruptReason.Care));}
    [Theory][InlineData(SequenceKind.Box)][InlineData(SequenceKind.Scratch)][InlineData(SequenceKind.Observe)]
    public void UnreachableHomeCannotLoopForever(SequenceKind kind)
    {var s=new BehaviorSequence(kind,new(),50,"home");for(var i=0;i<800;i++)s.Step(.05,new());Assert.True(s.Finished);Assert.Equal(BehaviorInterruptReason.Safety,s.InterruptedBy);}
    [Fact]public void TimidNewFurnitureInspectionIsLongerButBounded()
    {var shy=new BehaviorSequence(SequenceKind.Box,new(){Timidity=1},50,"box");var bold=new BehaviorSequence(SequenceKind.Box,new(){Timidity=0},50,"box");Assert.True(shy.InspectionDuration>bold.InspectionDuration);Assert.InRange(shy.InspectionDuration,.4,2);}
    [Fact]public void BedUsesSleepSequenceWithSettleAndKnead()
    {var s=new BehaviorSequence(SequenceKind.Sleep,new(),50,"bed",FurnitureKind.PetBed);var seen=new HashSet<BehaviorPhase>();for(var i=0;i<1800;i++){seen.Add(s.Phase);s.Step(.05,new(Reached:true,Rested:true));}Assert.True(s.Finished);Assert.Contains(BehaviorPhase.Settle,seen);Assert.Contains(BehaviorPhase.Knead,seen);Assert.Contains(BehaviorPhase.Sleep,seen);Assert.Contains(BehaviorPhase.Wake,seen);}
    [Fact]public void FamiliarFurnitureNeedsLessInspection()
    {var fresh=new BehaviorSequence(SequenceKind.Box,new(),50,"box",FurnitureKind.Box,0);var known=new BehaviorSequence(SequenceKind.Box,new(),50,"box",FurnitureKind.Box,1);Assert.True(fresh.InspectionDuration>known.InspectionDuration);Assert.InRange(known.InspectionDuration,.4,1);}
    [Fact]public void ScratchIsFiniteAndRecovers()
    {var s=new BehaviorSequence(SequenceKind.Scratch,new(),50,"scratch");var seen=new HashSet<BehaviorPhase>();for(var i=0;i<800;i++){seen.Add(s.Phase);s.Step(.05,new(Reached:true));}Assert.True(s.Finished);Assert.Contains(BehaviorPhase.Scratch,seen);Assert.Contains(BehaviorPhase.Stretch,seen);Assert.Contains(BehaviorPhase.Recover,seen);}
    [Fact]public void LowPlatformIsNotMistakenForGround()
    {var route=RoomNavigation.Plan([new(200,140,894,894)],new(0,0,1200,900),260,900,260,894);Assert.NotEmpty(route!);}
    [Fact]public void BoxInspectsHidesPeeksRestsAndExits()
    {
        var s=new BehaviorSequence(SequenceKind.Box,new(),50,"box");var seen=new HashSet<BehaviorPhase>();
        for(var i=0;i<800;i++){seen.Add(s.Phase);s.Step(.05,new(Reached:true));}
        foreach(var phase in new[]{BehaviorPhase.Inspect,BehaviorPhase.Enter,BehaviorPhase.Hide,BehaviorPhase.Peek,BehaviorPhase.Rest,BehaviorPhase.Exit,BehaviorPhase.Recover})Assert.Contains(phase,seen);
        Assert.True(s.Finished);
    }
    [Fact]public void BoxRemovalRecoversOnlyAfterGrounding()
    {var s=new BehaviorSequence(SequenceKind.Box,new(),50,"box");s.Step(.1,new(TargetExists:false,Grounded:false));for(var i=0;i<100;i++)s.Step(.1,new(Grounded:false));Assert.False(s.Finished);s.Step(.1,new());Assert.True(s.Finished);}
}
