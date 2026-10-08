using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class RoomNavigationTests
{
    private readonly BodyBounds bounds=new(0,0,1200,900);
    [Theory][InlineData(190,203,false)][InlineData(190,192,true)][InlineData(390,377,false)][InlineData(390,388,true)]
    public void DropDoesNotStartWhileStillOnShelf(double exit,double center,bool expected)
    {Assert.Equal(expected,RoomNavigation.ReachedDropExit(new(exit,250,900,740,true),center));}
    [Fact] public void ReachedExitActuallyFallsUnderProductionGravity()
    {
        RoomPlatform[] platforms=[new(200,180,740,740)];
        var route=RoomNavigation.Plan(platforms,bounds,260,740,250,900)!;
        var step=route[0];var body=new GravityBody{X=step.TakeoffX-58,Y=740-144};
        Assert.True(RoomNavigation.ReachedDropExit(step,body.X+58));
        body.Step(.1,bounds,116,144,0,platforms);
        Assert.False(body.Grounded);Assert.True(body.Y+144>740);
    }
    [Fact]public void HighShelfRequiresIntermediatePlatform()
    {
        RoomPlatform[] platforms=[new(200,180,780,780),new(200,180,660,660),new(200,180,540,540)];
        var route=RoomNavigation.Plan(platforms,bounds,260,900,260,540);
        Assert.NotNull(route);Assert.Equal(3,route.Count);Assert.All(route,p=>Assert.InRange(p.SourceY-p.LandingY,0,175));
    }
    [Fact]public void UnsupportedDestinationDoesNotBecomeTeleport()
    {Assert.Null(RoomNavigation.Plan([new(300,180,500,500)],bounds,300,900,350,500));}
    [Fact]public void DescentLeavesShelfBeforeHeadingToLanding()
    {
        var route=RoomNavigation.Plan([new(200,180,740,740)],bounds,260,740,250,900);
        var step=Assert.Single(route!);Assert.True(step.Drops);Assert.True(step.TakeoffX<200||step.TakeoffX>380);
    }
    [Fact]public void NegativeMonitorOriginDoesNotChangeReachability()
    {
        var b=new BodyBounds(-1600,-200,1200,900);
        var route=RoomNavigation.Plan([new(-1400,180,580,580),new(-1400,180,460,460)],b,-1340,700,-1340,460);
        Assert.Equal(2,route!.Count);
    }
    [Fact]public void WallAdjacentShelfUsesReachableExit()
    {
        var route=RoomNavigation.Plan([new(0,180,740,740)],bounds,70,740,60,900);
        Assert.Equal(190,Assert.Single(route!).TakeoffX);
    }
}
