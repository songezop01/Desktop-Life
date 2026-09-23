using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class RoomCoordinateTests
{
    [Theory][InlineData(1)][InlineData(1.25)][InlineData(1.5)][InlineData(2)]
    public void PixelDipRoundTripSupportsNegativeMonitorOrigin(double scale)
    {
        var bounds=RoomCoordinates.FromPixels(-1920,-300,1920,1080,scale);
        Assert.Equal(-1920,bounds.Left*scale,6);Assert.Equal(1080,bounds.Height*scale,6);
    }
    [Fact]public void RemovedMonitorRehomesWholeObjectInsideRemainingWorkArea()
    {
        var point=RoomCoordinates.Rehome(new(-20,700),new(-1920,0,1920,1080),new(0,0,1280,720),180,230);
        Assert.InRange(point.X,0,1100);Assert.InRange(point.Y,0,490);
    }
    [Fact]public void UnchangedDisplayDoesNotMoveFurniture()
    {
        var bounds=new BodyBounds(-1000,200,1500,900);var point=new RoomPoint(-800,450);
        Assert.Equal(point,RoomCoordinates.Rehome(point,bounds,bounds,180,230));
    }
}
