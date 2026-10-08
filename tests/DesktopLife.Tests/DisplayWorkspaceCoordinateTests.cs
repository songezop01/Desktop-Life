using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class DisplayWorkspaceCoordinateTests
{
    [Theory][InlineData(116,144)][InlineData(180,220)][InlineData(280,110)][InlineData(64,64)]
    public void RehomeToActualSpacedeskMixedDpiPreservesBottomSupport(double width,double height)
    {
        var laptop=RoomCoordinates.FromPixels(0,0,1920,1020,1.25);
        var phone=RoomCoordinates.FromPixels(1920,0,2400,996,1.75);
        var position=RoomCoordinates.Rehome(new(laptop.Left+laptop.Width-width,laptop.Top+laptop.Height-height),laptop,phone,width,height);
        Assert.Equal(4320,(position.X+width)*1.75,6);
        Assert.Equal(996,(position.Y+height)*1.75,6);
    }
    [Theory][InlineData(1.25)][InlineData(1.75)][InlineData(3)]
    public void PortraitDisplayToLeftRetainsObjectsInsideNegativePixelWorkarea(double scale)
    {
        var from=RoomCoordinates.FromPixels(0,0,1920,1020,1.25);
        var portrait=RoomCoordinates.FromPixels(-1440,-480,1440,2960,scale);
        foreach(var (width,height) in new[]{(116d,144d),(280d,110d),(180d,220d)})
        {
            var point=RoomCoordinates.Rehome(new(from.Width-width,from.Height-height),from,portrait,width,height);
            Assert.InRange(point.X*scale,-1440,0-width*scale);
            Assert.InRange(point.Y*scale,-480,2480-height*scale);
            Assert.Equal(2480,(point.Y+height)*scale,6);
        }
    }
}
