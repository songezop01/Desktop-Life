using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class RoomPhysicsTests
{
    private static readonly BodyBounds Bounds=new(0,0,1000,800);
    [Fact]public void ReleasedToyFallsBouncesAndSettlesOnFloor()
    {
        var toy=new InteractiveToy(100,100);var bounced=false;
        for(var i=0;i<2000;i++){toy.Step(.016,Bounds);bounced|=toy.VelocityY<0;}
        Assert.True(bounced);Assert.Equal(760,toy.Y,3);Assert.Equal(0,toy.VelocityY);
    }
    [Theory][InlineData(0,20,1,0)][InlineData(40,20,-1,0)][InlineData(20,0,0,1)][InlineData(20,40,0,-1)]
    public void ClickDirectionComesFromContactPoint(double x,double y,int signX,int signY)
    {var impulse=GravityBody.ClickImpulse(x,y,40);Assert.Equal(signX,Math.Sign(impulse.X));Assert.Equal(signY,Math.Sign(impulse.Y));}
    [Fact]public void PetLandsOnDeskAndFallsWhenSupportRemoved()
    {
        var body=new GravityBody{X=100,Y=10};RoomPlatform[] desk=[new(80,220,350,350)];
        for(var i=0;i<200;i++)body.Step(.016,Bounds,116,144,0,desk);
        Assert.Equal(206,body.Y,3);Assert.True(body.Grounded);
        for(var i=0;i<200;i++)body.Step(.016,Bounds,116,144,0);
        Assert.Equal(656,body.Y,3);
    }
    [Fact]public void SlideConvertsGravityToSidewaysMotion()
    {
        var body=new GravityBody{X=105,Y=120};RoomPlatform[] slide=[new(100,250,180,380)];
        for(var i=0;i<80;i++)body.Step(.016,Bounds,40,40,0,slide);
        Assert.True(body.X>130);Assert.True(body.Y>140);
    }
    [Fact]public void ToyCollisionTransfersMotionInsteadOfOverlapping()
    {
        var a=new InteractiveToy(100,200);var b=new InteractiveToy(130,200);a.Kick(300,0);
        InteractiveToy.Collide(a,b);Assert.True(b.VelocityX>200);Assert.True(a.VelocityX<100);Assert.Equal(40,b.X-a.X,3);
    }
    [Fact]public void RoomSurvivesOldCheckpointAndRejectsInvalidGeometry()
    {
        var old=JsonSerializer.SerializeToNode(new LearningState())!.AsObject();old.Remove("Room");
        var loaded=JsonSerializer.Deserialize<LearningState>(old.ToJsonString())!;Assert.Empty(loaded.Room.Items);
        loaded.Room.Items.Add(new(Guid.NewGuid(),FurnitureKind.CatTree,100,300));
        var copy=JsonSerializer.Deserialize<LearningState>(JsonSerializer.Serialize(loaded))!;copy.Validate();Assert.Equal(loaded.Room.Items[0],copy.Room.Items[0]);
        Assert.Throws<InvalidDataException>(()=>new RoomState{Items=[new(Guid.NewGuid(),FurnitureKind.Desk,double.NaN,0)]}.Validate());
    }
    [Fact]public void ClosePreferenceDefaultsToTrayAndRoundTrips()
    {var settings=new AppSettings();Assert.Equal(CloseBehavior.Tray,settings.CloseBehavior);Assert.Equal(CloseBehavior.Exit,JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings with{CloseBehavior=CloseBehavior.Exit}))!.CloseBehavior);}
}
