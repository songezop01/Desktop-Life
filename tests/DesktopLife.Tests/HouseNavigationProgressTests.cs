using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class HouseNavigationProgressTests
{
    [Fact]
    public void ContinuousWalkAcrossSeveralLongSegmentsDoesNotBecomeStalledAfterSixtySeconds()
    {
        var progress=new NavigationProgress();var x=0d;var y=900d;
        // Deliberately retain one progress observer across segment boundaries, as the production house controller does.
        for(var tick=0;tick<4800;tick++)
        {
            if(tick<1600)x+=.55;
            else if(tick<3200){x-=.4;y-=.32;}
            else{x+=.4;y+=.32;}
            Assert.False(progress.Stalled(x,y,.016,true));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void SlowButContinuousVerticalStairProgressCountsInEitherDirection(int direction)
    {
        var progress=new NavigationProgress();var y=700d;
        for(var tick=0;tick<4500;tick++)
        {
            y+=direction*.15;
            Assert.False(progress.Stalled(300,y,.016,true));
        }
    }

    [Fact]
    public void ALongMovingRouteStillDetectsAnActualThreeSecondStopAndCanResume()
    {
        var progress=new NavigationProgress();var x=100d;
        for(var tick=0;tick<4500;tick++){x+=.6;Assert.False(progress.Stalled(x,700,.016,true));}
        var stalled=false;
        for(var tick=0;tick<190;tick++)stalled|=progress.Stalled(x,700,.016,true);
        Assert.True(stalled);
        progress.Reset();
        for(var tick=0;tick<4500;tick++){x-=.6;Assert.False(progress.Stalled(x,700,.016,true));}
        for(var tick=0;tick<190;tick++)stalled=progress.Stalled(x,700,.016,true);
        Assert.True(stalled);
    }

    [Fact]
    public void WaitingForAStairReservationDoesNotConsumeTheMovementStallBudget()
    {
        var progress=new NavigationProgress();
        for(var tick=0;tick<180;tick++)Assert.False(progress.Stalled(300,700,.016,true));
        for(var tick=0;tick<5000;tick++)Assert.False(progress.Stalled(300,700,.016,false));
        for(var tick=0;tick<180;tick++)Assert.False(progress.Stalled(300,700,.016,true));
        for(var tick=0;tick<20;tick++)progress.Stalled(300,700,.016,true);
        Assert.True(progress.Stalled(300,700,.016,true));
    }
}
