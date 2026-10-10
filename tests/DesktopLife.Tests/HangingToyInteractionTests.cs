using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class HangingToyInteractionTests
{
    [Fact]
    public void DogUsesFloorAndCannotGainCatShelfPermissions()
    {
        var stance=HangingToyInteraction.Plan(PetAppearance.BorderCollie,new(140,570),new(165,110,612,612),new(0,1000,730,730),new(0,0,1000,800),104d*116/144,104,1)!;
        Assert.NotNull(stance);Assert.False(stance.UsesShelf);Assert.Equal(730,stance.Y+104,6);
        Assert.InRange(stance.RopeLength,140,155);
        Assert.True(570+stance.RopeLength+HangingToyInteraction.ContactRadius<730);
        Assert.True(FurnitureCompatibility.CanUse(PetAppearance.BorderCollie,FurnitureKind.CatTree,FurnitureUse.Play));
        Assert.False(FurnitureCompatibility.CanUse(PetAppearance.BorderCollie,FurnitureKind.CatTree,FurnitureUse.Platform));
        Assert.False(FurnitureCompatibility.CanUse(PetAppearance.BorderCollie,FurnitureKind.CatTree,FurnitureUse.Rest));
        Assert.False(FurnitureCompatibility.CanUse(PetAppearance.BorderCollie,FurnitureKind.CatTree,FurnitureUse.Play|FurnitureUse.Platform));
    }
    [Fact]
    public void CatUsesLowerShelfWithOriginalStringLength()
    {
        // The lower shelf supports the paws with the resting ball above them,
        // matching the reachable cat-tree layout rather than below its feet.
        var shelf=new RoomPlatform(155,110,632,632);
        var stance=HangingToyInteraction.Plan(PetAppearance.Cat,new(140,570),shelf,new(0,1000,730,730),new(0,0,1000,800),80d*116/144,80,1)!;
        Assert.NotNull(stance);Assert.True(stance.UsesShelf);Assert.Equal(shelf,stance.Support);Assert.Equal(632,stance.Y+80,6);Assert.Equal(HangingToy.Length,stance.RopeLength);
        Assert.InRange(stance.X+80d*116/144/2,shelf.X,shelf.X+shelf.Width);
        Assert.True((140-stance.X)/(80d/144)<0,"A resting sphere must sit beside the viewport, not over the future play face.");
    }
    [Fact]
    public void ContactIsOnBallCircumferenceAndCannotReachOverHead()
    {
        var ball=new RoomPoint(34*104/144,100*104/144);
        var contact=HangingToyInteraction.ContactPoint(PetAppearance.BorderCollie,ball,0,0,104,1)!;
        Assert.NotNull(contact);Assert.Equal(9,Math.Sqrt(Math.Pow(contact.X-ball.X,2)+Math.Pow(contact.Y-ball.Y,2)),6);
        Assert.Null(HangingToyInteraction.ContactPoint(PetAppearance.BorderCollie,ball with{Y=-30},0,0,104,1));
        Assert.Null(HangingToyInteraction.ContactPoint(PetAppearance.Girl,ball,0,0,256,1));
    }
    [Theory]
    [InlineData(PetAppearance.Cat,80,-13,109)]
    [InlineData(PetAppearance.BorderCollie,104,-10,126.72)]
    public void OutsideSphereHasAVisibleReachableTipInsideViewport(PetAppearance kind,double height,double x,double y)
    {
        var scale=height/144;var ball=new RoomPoint(x*scale,y*scale);
        var contact=HangingToyInteraction.ContactPoint(kind,ball,0,0,height,1);
        Assert.NotNull(contact);Assert.InRange(contact!.X/scale,1,8);
        Assert.InRange(contact.Y/scale,76,140);
        Assert.Equal(9,Math.Sqrt(Math.Pow(contact.X-ball.X,2)+Math.Pow(contact.Y-ball.Y,2)),6);
    }
    [Theory]
    [InlineData(.4)] [InlineData(.65)] [InlineData(1)] [InlineData(1.8)]
    public void SceneScaleKeepsFloorContactAndPawReach(double scale)
    {
        var stance=HangingToyInteraction.Plan(PetAppearance.BorderCollie,new(140*scale,570*scale),new(165*scale,110*scale,612*scale,612*scale),new(0,1000*scale,730*scale,730*scale),new(0,0,1000*scale,800*scale),104d*116/144*scale,104*scale,scale)!;
        Assert.NotNull(stance);Assert.Equal(730*scale,stance.Y+104*scale,6);
        Assert.NotNull(HangingToyInteraction.ContactPoint(PetAppearance.BorderCollie,new(140*scale,570*scale+stance.RopeLength*scale),stance.X,stance.Y,104*scale,scale));
        var ballX=(140*scale-stance.X)/(104d/144*scale);
        Assert.True(ballX<0||ballX>116);
    }
    [Fact]
    public void UnsupportedOrOutOfBoundsTargetsCannotBecomePlay()
    {
        Assert.Null(HangingToyInteraction.Plan(PetAppearance.Girl,new(140,570),new(155,110,612,612),new(0,1000,730,730),new(0,0,1000,800),100,256,1));
        Assert.Null(HangingToyInteraction.Plan(PetAppearance.Cat,new(140,10),new(155,110,50,50),new(0,1000,730,730),new(0,0,1000,800),64,80,1));
        Assert.Null(HangingToyInteraction.Plan(PetAppearance.BorderCollie,new(140,10),new(155,110,50,50),new(0,1000,730,730),new(0,0,1000,800),84,104,1));
    }
    [Fact]
    public void CompactUpperFloorStringPlansAroundTheActualContactSilhouette()
    {
        const double scene=.758364312267658,height=78.86988847583643,feet=539.9553903345725;
        var anchor=new RoomPoint(1008.5947955390335,522.2602230483271-160*scene);
        var shelf=new RoomPlatform(1020,80,480,480);var floor=new RoomPlatform(15.16728624535316,1505.6654275092937,feet,feet);
        var bounds=new BodyBounds(0,0,1536,816);var width=height*116/144;
        // A painted edge at the failed mixed-scene height. The native regression
        // supplies the complete, filtered Frame 7 illustration independently.
        var alpha=new byte[116*144];alpha[112*116]=255;
        var silhouette=HangingToySilhouette.FromAlpha(alpha,116,144,1);
        var previous=HangingToyInteraction.Plan(PetAppearance.BorderCollie,anchor,shelf,floor,bounds,width,height,scene)!;
        var radius=HangingToyInteraction.ContactRadius*scene/(height/144);
        Assert.Equal(160,previous.RopeLength);
        Assert.False(silhouette.IsSphereClear(new((anchor.X-previous.X)/(height/144),
            (anchor.Y+previous.RopeLength*scene-previous.Y)/(height/144)),radius));
        var planned=HangingToyInteraction.Plan(PetAppearance.BorderCollie,anchor,shelf,floor,bounds,width,height,scene,silhouette);
        Assert.NotNull(planned);Assert.False(planned!.UsesShelf);Assert.Equal(floor,planned.Support);
        Assert.Equal(feet,planned.Y+height,6);Assert.InRange(planned.RopeLength,HangingToy.Length,158);
        var ball=new RoomPoint(anchor.X,anchor.Y+planned.RopeLength*scene);
        Assert.True(silhouette.IsSphereClear(new((ball.X-planned.X)/(height/144),(ball.Y-planned.Y)/(height/144)),radius));
        Assert.NotNull(HangingToyInteraction.ContactPoint(PetAppearance.BorderCollie,ball,planned.X,planned.Y,height,scene));
    }
    [Fact]
    public void ACompletelyOccludedReachableStringCannotInventAVisibleStance()
    {
        var alpha=Enumerable.Repeat((byte)255,116*144).ToArray();
        var silhouette=HangingToySilhouette.FromAlpha(alpha,116,144,1);
        Assert.Null(HangingToyInteraction.Plan(PetAppearance.BorderCollie,new(140,570),new(165,110,612,612),
            new(0,1000,730,730),new(0,0,1000,800),104d*116/144,104,1,silhouette));
    }
    [Fact]
    public void ExtendRetractNeverTeleportsOrEscapesFurnitureWidth()
    {
        var toy=new HangingToy();toy.SetLength(140);toy.Bat(1000);
        for(var i=0;i<160;i++){var previous=toy.RopeLength;toy.Step(.016);Assert.InRange(toy.RopeLength-previous,0,1.121);Assert.InRange(toy.X,-35.000001,35.000001);Assert.Equal(toy.RopeLength,Math.Sqrt(toy.X*toy.X+toy.Y*toy.Y),6);}
        Assert.Equal(140,toy.RopeLength,6);toy.SetLength(HangingToy.Length);
        for(var i=0;i<160;i++){var previous=toy.RopeLength;toy.Step(.016);Assert.InRange(previous-toy.RopeLength,0,1.121);Assert.InRange(toy.X,-35.000001,35.000001);}
        Assert.Equal(HangingToy.Length,toy.RopeLength,6);
    }
    [Fact]
    public void RestingStringStillExtendsAndReturnsWithoutImpulse()
    {
        var toy=new HangingToy();for(var i=0;i<4000;i++)toy.Step(.01);Assert.True(toy.IsResting);
        toy.SetLength(120);Assert.False(toy.IsResting);for(var i=0;i<200;i++)toy.Step(.01);
        Assert.Equal(120,toy.RopeLength);Assert.True(toy.IsResting);Assert.Equal(0,toy.AngularVelocity);
        Assert.Throws<ArgumentOutOfRangeException>(()=>toy.SetLength(double.NaN));Assert.Throws<ArgumentOutOfRangeException>(()=>toy.SetLength(161));
    }
}
