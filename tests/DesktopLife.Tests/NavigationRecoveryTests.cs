using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class NavigationRecoveryTests
{
    [Theory][InlineData(0,1150)][InlineData(-1200,-50)]
    public void DeskAtRightWallEscapesLeft(double origin,double center)
    {var bounds=new BodyBounds(origin,0,1200,800);var table=new RoomPlatform(origin+970,220,662,662);var exit=RoomNavigation.EscapeX([table],bounds,center,800);Assert.True(exit<table.X-58);Assert.InRange(exit,origin+58,origin+1142);}
    [Fact]public void NoProgressIsBoundedButWalkingDoesNotTrip()
    {var guard=new NavigationProgress();var tripped=false;for(var i=0;i<40;i++)tripped|=guard.Stalled(50,600,.1,true);Assert.True(tripped);guard.Reset();for(var i=0;i<100;i++)Assert.False(guard.Stalled(50+i*3,600,.1,true));}
    [Fact]public void UnreachableEdgeTakeoffDoesNotCreateInfiniteApproach()
    {var b=new BodyBounds(0,0,1200,900);Assert.Null(RoomNavigation.Plan([new(0,30,780,780)],b,70,900,20,780));}
}
