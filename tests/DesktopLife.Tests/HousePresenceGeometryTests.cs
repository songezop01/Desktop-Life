using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class HousePresenceGeometryTests
{
    [Theory]
    [InlineData(256,80)]
    [InlineData(256,104)]
    [InlineData(104,80)]
    public void FullSizeCharacterWidthsUseGroundAlignedOverlapThreshold(double firstHeight,double secondHeight)
    {
        var overlap=(firstHeight+secondHeight)*116/144/2+8;
        // The caller supplies foot Y rather than different-height top-left Y values.
        Assert.Equal(OtherPresenceReaction.AvoidOverlap,OtherPresence.Decide(300,700,300+overlap-1,700,false,.5,1,overlap));
        Assert.NotEqual(OtherPresenceReaction.AvoidOverlap,OtherPresence.Decide(300,700,300+overlap+1,700,false,.5,1,overlap));
    }

    [Fact]
    public void DifferentFloorsAndCommittedActivitiesStillSuppressAvoidance()
    {
        Assert.Equal(OtherPresenceReaction.None,OtherPresence.Decide(300,700,301,348,false,.5,0,150));
        Assert.Equal(OtherPresenceReaction.None,OtherPresence.Decide(300,700,301,700,true,.5,0,150));
    }
}
