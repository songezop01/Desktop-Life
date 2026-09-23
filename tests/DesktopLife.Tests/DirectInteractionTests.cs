using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class DirectInteractionTests
{
    [Fact] public void ClickJitterDoesNotBecomeDrag()
    {
        var g=new DragGesture();g.Begin(50,50,200,100);Assert.Null(g.Move(52,51));Assert.False(g.End());Assert.Null(g.Move(100,100));
    }
    [Fact] public void DragPreservesGrabOffsetAndNeverBecomesClickAgain()
    {
        var g=new DragGesture();g.Begin(50,50,200,100);Assert.Equal((220d,110d),g.Move(70,60));
        Assert.Equal((200d,100d),g.Move(50,50));Assert.True(g.End());
    }
    [Fact] public void ToyPushMovesAndHeldToyDoesNotMove()
    {
        var bounds=new BodyBounds(0,0,1000,800);var toy=new InteractiveToy(100,100);
        toy.Kick(100,0);toy.Step(.1,bounds);Assert.True(toy.X>100);Assert.InRange(toy.VelocityX,0,99);
        toy.Held=true;var x=toy.X;toy.Kick(500,0);toy.Step(.1,bounds);Assert.Equal(x,toy.X);
        toy.Place(400,300,bounds);toy.Held=false;toy.Step(.1,bounds);Assert.Equal(400,toy.X);
    }
    [Fact] public void ToyBouncesAndLongFrameCannotTeleport()
    {
        var bounds=new BodyBounds(0,0,1000,800);var toy=new InteractiveToy(955,100);toy.Kick(200,0);toy.Step(60,bounds);
        Assert.InRange(toy.X,0,1000-InteractiveToy.Size);Assert.True(toy.VelocityX<0);
        var body=new DesktopBody();body.Place(100,100,bounds);body.MoveToward(900,700,60,bounds,80);
        Assert.InRange(Math.Sqrt(Math.Pow(body.X-100,2)+Math.Pow(body.Y-100,2)),0,8.001);
    }
    [Fact] public void HidingUsesVisibleCrouchPose()
    {Assert.Equal(PetPose.At(BodyAction.Sit,1),PetPose.At(BodyAction.Hide,1));}
}
