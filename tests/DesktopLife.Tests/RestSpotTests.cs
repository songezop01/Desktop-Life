using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class RestSpotTests
{
    [Fact]public void CushionPreferredAndEmptyRoomFallsBack()
    {var p=new RestSpotPreference();Assert.Equal("bed",p.Choose([new("desk",FurnitureKind.Desk,300,600),new("bed",FurnitureKind.Cushion,300,700)],new(),20,300,null)!.Id);Assert.Null(p.Choose([],new(),20,0,null));}
    [Fact]public void BondAndRecentUseInfluenceEquivalentSpots()
    {var p=new RestSpotPreference();RestSpot[] spots=[new("a",FurnitureKind.Cushion,200,600),new("b",FurnitureKind.Cushion,800,600)];Assert.Equal("b",p.Choose(spots,new(){Social=1,Laziness=0},100,500,800)!.Id);p.Used("a");p.Used("a");Assert.Equal("a",p.Choose(spots,new(),0,500,null)!.Id);}
    [Fact]public void RepeatedCriticalSignalsDoNotRestartRecovery()
    {var s=new BehaviorSequence(SequenceKind.Play,new(),15,"a");for(var i=0;i<30;i++){s.Interrupt(BehaviorInterruptReason.CriticalNeed);s.Step(.05,new());}Assert.True(s.Finished);}
}
