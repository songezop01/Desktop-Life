using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class DesktopBodyTests
{
    [Theory] [InlineData(0)] [InlineData(-1920)] public void MovementRemainsInWorkArea(double left)
    {
        var b = new BodyBounds(left,0,1920,1040); var body = new DesktopBody(); body.Reset(b); body.Action = BodyAction.Walk;
        for (var i=0;i<10000;i++) { body.Update(0.1,b); Assert.InRange(body.X,left,left+1920-DesktopBody.Width); Assert.InRange(body.Y,0,1040-DesktopBody.Height); }
    }
    [Theory] [InlineData(BodyAction.Idle)] [InlineData(BodyAction.Sit)] [InlineData(BodyAction.Sleep)] public void RestActionsDoNotMove(BodyAction action)
    { var b = new BodyBounds(0,0,1000,800); var body = new DesktopBody(); body.Reset(b); var x=body.X; body.Action=action; body.Update(1,b); Assert.Equal(x,body.X); }
    [Fact] public void DisplayShrinkRecoversPosition() { var body = new DesktopBody(); body.Reset(new(0,0,3840,2160)); body.Update(0,new(0,0,800,600)); Assert.InRange(body.X,0,684); Assert.InRange(body.Y,0,456); }
    [Fact] public void ResumeGapIsCapped() { var b = new BodyBounds(0,0,1000,800); var body = new DesktopBody(); body.Reset(b); var x=body.X; body.Action=BodyAction.Walk; body.Update(3600,b); Assert.Equal(6,body.X-x,6); }
}
