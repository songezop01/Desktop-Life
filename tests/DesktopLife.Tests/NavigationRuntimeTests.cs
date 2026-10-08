using DesktopLife.App;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class NavigationRuntimeTests
{
    [Theory]
    [InlineData(.016)]
    [InlineData(.033)]
    [InlineData(.1)]
    public void PresenceSuspensionRetainsUnfinishedSafetyEscape(double frame)
    {
        var bounds=new BodyBounds(0,0,1200,900);
        var shelf=new RoomPlatform(975,220,762,762);
        var pet=new PetWindow(bounds,[shelf],1040,900);
        for(var i=0;i<150&&pet.MovementPhase!=NavigationPhase.Airborne;i++)pet.MoveTo(1040,762,frame);
        Assert.Equal(NavigationPhase.Airborne,pet.MovementPhase);
        pet.ReplacePlatforms([]);pet.MoveTo(1040,762,frame);
        Assert.Equal(NavigationPhase.Recovering,pet.MovementPhase);
        var position=pet.Position;
        pet.SuspendNavigationPresence();
        Assert.Equal(position,pet.Position);
        Assert.Equal(NavigationPhase.Recovering,pet.MovementPhase);
        Assert.Equal(0,pet.NavigationRecoveriesCompleted);
        for(var i=0;i<500&&pet.MovementPhase==NavigationPhase.Recovering;i++)pet.MoveTo(1040,900,frame);
        Assert.True(pet.Grounded);
        Assert.Equal(1,pet.NavigationFailures);
        Assert.Equal(1,pet.NavigationRecoveriesCompleted);
        Assert.Equal(0,pet.NavigationRecoveryTimeouts);
        Assert.True(Assert.Single(pet.NavigationRecoveryDetails).Grounded);
    }

    [Theory]
    [InlineData(.016)]
    [InlineData(.033)]
    [InlineData(.1)]
    public void ShallowOverlappingSupportTransitionDoesNotStartDistantDrop(double frame)
    {
        var bounds=new BodyBounds(0,0,1536,816);
        RoomPlatform[] platforms=[new(416.4,140,547.2,547.2),new(277.4,140,539.2,539.2),new(283.6,140,796.8,796.8),new(357,240,549.2,549.2)];
        var pet=new PetWindow(bounds,platforms,532,549.2);
        for(var i=0;i<2500;i++)
        {
            pet.MoveTo(295.6,796.8,frame);
            if(pet.Grounded&&pet.MovementPhase==NavigationPhase.Idle&&Math.Abs(pet.Position.X-295.6)<3&&Math.Abs(pet.Position.Y+144-796.8)<1)break;
        }
        Assert.Equal(0,pet.NavigationFailures);
        Assert.True(pet.Grounded);
        Assert.Equal(NavigationPhase.Idle,pet.MovementPhase);
        Assert.InRange(Math.Abs(pet.Position.X-295.6),0,3);
        Assert.Equal(796.8,pet.Position.Y+144,6);
    }

    [Theory]
    [InlineData(0,0,1535.6,816,.016)]
    [InlineData(-1535.6,-120,1535.6,816,.033)]
    [InlineData(0,0,1200,900,.008)]
    public void WalkOffDeskEdgeCompletesDropAndResumesFloorTravel(double left,double top,double width,double height,double frame)
    {
        var bounds=new BodyBounds(left,top,width,height);
        var deskX=left+width-230;
        var shelf=new RoomPlatform(deskX+5,220,top+height-138,top+height-138);
        var pet=new PetWindow(bounds,[shelf],deskX+70,shelf.Y);
        var goalX=deskX-140;var floor=top+height;
        var phases=new HashSet<NavigationPhase>();
        var completed=false;var sawUnsupportedApproach=false;
        for(var i=0;i<1600;i++)
        {
            var previous=pet.Position;
            var wasFalling=!pet.Grounded;
            pet.MoveTo(goalX,floor,frame);
            phases.Add(pet.MovementPhase);
            Assert.InRange(Math.Abs(pet.Position.X-previous.X),0,85*frame+.01);
            Assert.InRange(Math.Abs(pet.Position.Y-previous.Y),0,25);
            if(wasFalling&&pet.MovementPhase==NavigationPhase.Approaching)sawUnsupportedApproach=true;
            if(Math.Abs(pet.Position.X-goalX)<3&&Math.Abs(pet.Position.Y+144-floor)<3&&!pet.FinishingMotion)
            {completed=true;break;}
        }
        Assert.True(completed,$"Desk descent stalled at {pet.Position}, {pet.MovementPhase}");
        Assert.False(sawUnsupportedApproach);
        Assert.Contains(NavigationPhase.Airborne,phases);
        Assert.Contains(NavigationPhase.Landing,phases);
        Assert.Equal(0,pet.NavigationFailures);
        Assert.True(pet.Grounded);
    }

    [Fact]
    public void RemovedJumpSupportRecoversThroughGravityWithoutTeleporting()
    {
        var bounds=new BodyBounds(0,0,1200,900);
        var shelf=new RoomPlatform(975,220,762,762);
        var pet=new PetWindow(bounds,[shelf],1040,900);
        for(var i=0;i<100&&pet.MovementPhase!=NavigationPhase.Airborne;i++)pet.MoveTo(1040,shelf.Y);
        Assert.Equal(NavigationPhase.Airborne,pet.MovementPhase);
        Assert.False(pet.Grounded);
        pet.ReplacePlatforms([]);
        var previous=pet.Position;
        pet.MoveTo(1040,shelf.Y);
        Assert.Equal(NavigationPhase.Recovering,pet.MovementPhase);
        Assert.Equal(1,pet.NavigationFailureReasons.GetValueOrDefault("geometry-changed-in-flight"));
        Assert.InRange(Math.Abs(pet.Position.Y-previous.Y),0,12);
        for(var i=0;i<450&&pet.MovementPhase==NavigationPhase.Recovering;i++)
        {
            previous=pet.Position;pet.MoveTo(1040,900);
            Assert.InRange(Math.Abs(pet.Position.X-previous.X),0,12);
            Assert.InRange(Math.Abs(pet.Position.Y-previous.Y),0,12);
        }
        Assert.True(pet.Grounded);
        Assert.InRange(Math.Abs(pet.Position.Y+144-900),0,.01);
        Assert.NotEqual(NavigationPhase.Recovering,pet.MovementPhase);
        Assert.Equal(1,pet.NavigationRecoveriesCompleted);
        Assert.Equal(0,pet.NavigationRecoveryTimeouts);
        Assert.Equal(1,pet.NavigationRecoveryCompletionReasons.GetValueOrDefault("geometry-changed-in-flight"));
        var failure=Assert.Single(pet.NavigationFailureDetails);
        Assert.NotNull(failure.Waypoint);
        Assert.NotNull(failure.LaunchPoint);
        var recovery=Assert.Single(pet.NavigationRecoveryDetails);
        Assert.True(recovery.Grounded&&recovery.ReachedEscape);
        Assert.False(recovery.TimedOut);
        Assert.Equal(recovery.ElapsedSeconds,pet.NavigationRecoveryCompletionMaxSeconds);
        Assert.InRange(pet.NavigationRecoveryCompletionMaxSeconds,0,5);
    }

    [Theory]
    [InlineData(.016,65,false)]
    [InlineData(.033,85,false)]
    [InlineData(.1,115,false)]
    [InlineData(.033,85,true)]
    public void UpperBoxRoutesThroughFloorInsteadOfMissingNarrowLowerBed(double frame,double speed,bool lowerBoxMoved)
    {
        // Actual geometry from the isolated 60-second stress failure. The old
        // diagonal drop missed the bed by 3.67 px and landed on the floor.
        var bounds=new BodyBounds(0,0,1536,816);
        RoomPlatform[] platforms=[new(48.4,124,559.6,559.6),new(lowerBoxMoved?70.4:48.4,124,810,810),new(232.6,140,788.8,788.8),new(40.4,90,625.6,625.6)];
        var pet=new PetWindow(bounds,platforms,52.4,559.6);
        var route=RoomNavigation.Plan(platforms,bounds,110.4,559.6,255.4,788.8);
        Assert.NotNull(route);
        Assert.True(route!.Count>=2);
        Assert.All(route.Where(step=>step.Drops),step=>Assert.Equal(step.TakeoffX,step.LandingX));
        var landedOnFloor=false;var landedOnLowerBox=false;var reachedBed=false;
        for(var i=0;i<2500;i++)
        {
            var previous=pet.Position;pet.MoveTo(197.4,788.8,frame,speed);
            landedOnFloor|=pet.Grounded&&Math.Abs(pet.Position.Y+144-816)<1;
            landedOnLowerBox|=pet.Grounded&&Math.Abs(pet.Position.Y+144-810)<1;
            Assert.InRange(Math.Abs(pet.Position.X-previous.X),0,30);
            Assert.InRange(Math.Abs(pet.Position.Y-previous.Y),0,80);
            if(Math.Abs(pet.Position.X-197.4)<3&&Math.Abs(pet.Position.Y+144-788.8)<1&&!pet.FinishingMotion){reachedBed=true;break;}
        }
        Assert.True(reachedBed,$"Narrow-bed route failed: {pet.Position}, {pet.MovementPhase}, {string.Join(',',pet.NavigationFailureReasons.Keys)}");
        Assert.True(landedOnFloor||lowerBoxMoved&&landedOnLowerBox);
        Assert.Equal(0,pet.NavigationFailures);
    }

    [Fact]
    public void BoxExitKeepsExplicitFloorGoalUnderLowBed()
    {
        var bounds=new BodyBounds(0,0,1536,816);
        RoomPlatform[] platforms=[new(48.4,124,559.6,559.6),new(48.4,124,810,810),new(232.6,140,788.8,788.8)];
        var pet=new PetWindow(bounds,platforms,52.4,559.6);
        for(var i=0;i<2000;i++)
        {
            pet.MoveTo(197.4,816,.033,85);
            if(Math.Abs(pet.Position.X-197.4)<3&&!pet.FinishingMotion&&pet.Grounded)break;
        }
        Assert.InRange(Math.Abs(pet.Position.X-197.4),0,3);
        Assert.Equal(816,pet.Position.Y+144,6);
        Assert.Equal(NavigationPhase.Idle,pet.MovementPhase);
        Assert.Equal(0,pet.NavigationFailures);
    }

    [Fact]
    public void JumpRoutesThroughInterceptingUpperSupportBeforeLowerDestination()
    {
        var bounds=new BodyBounds(0,0,1200,900);
        RoomPlatform[] platforms=[new(200,180,700,700),new(390,60,685,685),new(400,180,700,700)];
        var route=RoomNavigation.Plan(platforms,bounds,260,700,450,700);
        Assert.NotNull(route);Assert.Equal(2,route!.Count);
        Assert.Equal(685,route[0].LandingY);Assert.True(route[1].Drops);
        var pet=new PetWindow(bounds,platforms,202,700);
        for(var i=0;i<2000;i++)
        {
            pet.MoveTo(392,700,.033,85);
            if(Math.Abs(pet.Position.X-392)<3&&Math.Abs(pet.Position.Y+144-700)<1&&pet.MovementPhase==NavigationPhase.Idle&&pet.Grounded)break;
        }
        Assert.InRange(Math.Abs(pet.Position.X-392),0,3);
        Assert.Equal(700,pet.Position.Y+144,6);
        Assert.Equal(0,pet.NavigationFailures);
    }

    [Theory]
    [InlineData(.016)]
    [InlineData(.033)]
    [InlineData(.1)]
    public void DescendingContactWindowRoutesViaOverlappingBedBeforeTable(double frame)
    {
        // Exact Standard-run collision: the arc crosses the bed plane before
        // its left edge, but gravity's five-pixel grace still catches it later.
        var bounds=new BodyBounds(0,0,1536,816);
        RoomPlatform[] platforms=[new(40.4,90,625.6,625.6),new(222.6,240,553.2,553.2),new(48.4,124,559.6,559.6),new(232.6,140,539.2,539.2)];
        var route=RoomNavigation.Plan(platforms,bounds,120.4,625.6,242.6,553.2);
        Assert.NotNull(route);
        Assert.NotEqual(553.2,route![0].LandingY);
        Assert.Contains(route,step=>Math.Abs(step.LandingY-539.2)<.01);
        var pet=new PetWindow(bounds,platforms,62.4,625.6);
        for(var i=0;i<2500;i++)
        {
            pet.MoveTo(184.6,553.2,frame);
            if(pet.MovementPhase==NavigationPhase.Idle&&pet.Grounded&&Math.Abs(pet.Position.X-184.6)<3&&Math.Abs(pet.Position.Y+144-553.2)<1)break;
        }
        Assert.Equal(NavigationPhase.Idle,pet.MovementPhase);
        Assert.InRange(Math.Abs(pet.Position.X-184.6),0,3);
        Assert.Equal(553.2,pet.Position.Y+144,6);
        Assert.Equal(0,pet.NavigationFailures);
    }

    [Theory]
    [InlineData(.016)]
    [InlineData(.033)]
    [InlineData(.1)]
    public void SixPixelBoxDescentWalksOffInsteadOfJumpingBackOntoBox(double frame)
    {
        var bounds=new BodyBounds(0,0,1536,816);
        RoomPlatform[] platforms=[new(70,124,810,810)];
        var route=RoomNavigation.Plan(platforms,bounds,100,810,85.6,816);
        var drop=Assert.Single(route!);Assert.True(drop.Drops);Assert.Equal(816,drop.LandingY);
        var pet=new PetWindow(bounds,platforms,42,810);
        for(var i=0;i<1000;i++)
        {
            pet.MoveTo(27.6,816,frame);
            if(pet.MovementPhase==NavigationPhase.Idle&&pet.Grounded&&Math.Abs(pet.Position.X-27.6)<3&&Math.Abs(pet.Position.Y+144-816)<1)break;
        }
        Assert.Equal(NavigationPhase.Idle,pet.MovementPhase);
        Assert.InRange(Math.Abs(pet.Position.X-27.6),0,3);
        Assert.Equal(816,pet.Position.Y+144,6);
        Assert.Equal(0,pet.NavigationFailures);
    }

    [Fact]
    public void RecoveryBeyondFiveSecondsCountsTimeoutInsteadOfCompletion()
    {
        var pet=new PetWindow(new(0,0,1536,816),[],400,816);
        for(var i=0;i<32&&pet.MovementPhase!=NavigationPhase.Recovering;i++)pet.MoveTo(900,816,.1,0);
        Assert.Equal(NavigationPhase.Recovering,pet.MovementPhase);
        for(var i=0;i<7&&pet.MovementPhase==NavigationPhase.Recovering;i++)pet.MoveTo(900,816,1);
        Assert.Equal(0,pet.NavigationRecoveriesCompleted);
        Assert.Equal(0,pet.NavigationRecoveryCompletionMaxSeconds);
        Assert.Equal(1,pet.NavigationRecoveryTimeouts);
        Assert.Equal(1,pet.NavigationRecoveryTimeoutReasons.GetValueOrDefault("no-progress"));
        Assert.True(Assert.Single(pet.NavigationRecoveryDetails).TimedOut);
    }
}
