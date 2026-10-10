using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class HangingToySilhouetteTests
{
    private static HangingToySilhouette Pose(int x,int y)
    {
        var alpha=new byte[116*144];alpha[y*116+x]=255;
        return HangingToySilhouette.FromAlpha(alpha,116,144,1);
    }
    [Fact]
    public void EmptyCenterDoesNotMakeAnOverlappingSphereVisible()
    {
        var silhouette=Pose(10,110);
        Assert.False(silhouette.IsSphereClear(new(0,110),12));
        Assert.True(silhouette.IsSphereClear(new(-8,110),12));
    }
    [Fact]
    public void BothFacingEndpointsAndIntermediateFlipRemainCovered()
    {
        var silhouette=Pose(100,110);
        Assert.False(silhouette.IsSphereClear(new(16,110),1));
        Assert.False(silhouette.IsSphereClear(new(58,110),1));
        Assert.False(silhouette.IsSphereClear(new(100,110),1));
        Assert.True(silhouette.IsSphereClear(new(-8,110),8));
    }
    [Fact]
    public void GroundedBreathingAndPoseEaseCannotRevealFalseClearance()
    {
        var silhouette=Pose(58,50);
        Assert.False(silhouette.IsSphereClear(new(58,53),1));
        Assert.True(silhouette.IsSphereClear(new(58,65),1));
    }
    [Fact]
    public void TransparentRasterDoesNotInventAnOccluder()
    {
        var silhouette=HangingToySilhouette.FromAlpha(new byte[20*20],20,20,1);
        Assert.True(silhouette.IsSphereClear(new(0,0),20));
        Assert.Throws<ArgumentOutOfRangeException>(()=>silhouette.IsSphereClear(new(0,0),double.NaN));
    }
}
