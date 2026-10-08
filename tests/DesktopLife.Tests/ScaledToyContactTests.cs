using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class ScaledToyContactTests
{
    [Theory]
    [InlineData(80,1)][InlineData(104,1)][InlineData(256,1)]
    [InlineData(80,.53)][InlineData(104,.53)][InlineData(256,.53)]
    [InlineData(80,.4)][InlineData(104,.4)]
    public void ApproachingToyReachesThePawWithoutKickingItBeforeContact(double height,double sceneScale)
    {
        var bounds=new BodyBounds(0,0,1371,569);var minimumY=height==104?96:72;height*=sceneScale;
        var width=height*116/144;var scale=height/144;
        foreach(var toyX in new[]{0d,500d,bounds.Width-40})
        foreach(var startX in new[]{0d,bounds.Width-width})
        {
            var toy=new InteractiveToy(toyX,bounds.Height-40);
            var approach=ToyContactPhysics.Approach(toy.X,toy.Y,startX,bounds,width,height);
            var localX=(toy.X+20-approach.X)/scale;
            Assert.InRange(localX,6,110);
            var contact=ToyContactPhysics.ReachableContactPoint(toy.X,toy.Y,approach.X,approach.Y,height,minimumY);
            Assert.NotNull(contact);
            Assert.InRange((contact.X-approach.X)/scale,6-.000001,110+.000001);
            Assert.InRange((contact.Y-approach.Y)/scale,minimumY-.000001,140+.000001);
            Assert.Equal(20,Math.Sqrt(Math.Pow(contact.X-toy.X-20,2)+Math.Pow(contact.Y-toy.Y-20,2)),6);
            Assert.Equal(bounds.Height,approach.Y+height,6);
            ToyContactPhysics.Separate(toy,approach.X,approach.Y,bounds,width,height);
            Assert.Equal(toyX,toy.X,6);Assert.Equal(0,toy.VelocityX);Assert.Equal(0,toy.VelocityY);
        }
    }

    [Theory]
    [InlineData(104,569d/1076,96)]
    [InlineData(80,.4,72)]
    public void LargeToyExposesAReachableLowerSurfaceEvenWhenItsCentreIsAboveThePaw(double height,double sceneScale,double minimumY)
    {
        height*=sceneScale;var scale=height/144;var bounds=new BodyBounds(0,0,1371,569);
        var approach=ToyContactPhysics.Approach(600,529,400,bounds,height*116/144,height);
        Assert.True((549-approach.Y)/scale<minimumY);
        var contact=ToyContactPhysics.ReachableContactPoint(600,529,approach.X,approach.Y,height,minimumY);
        Assert.NotNull(contact);
        Assert.InRange((contact.Y-approach.Y)/scale,minimumY-.000001,140+.000001);
    }

    [Fact]
    public void AContactMustIntersectTheActualCircleAndReachArea()
    {
        var tangent=ToyContactPhysics.ReachableContactPoint(110,92,0,0,144,96);
        Assert.NotNull(tangent);Assert.Equal(110,tangent.X,6);Assert.Equal(112,tangent.Y,6);
        Assert.Null(ToyContactPhysics.ReachableContactPoint(110.001,92,0,0,144,96));
        Assert.Null(ToyContactPhysics.ReachableContactPoint(500,529,0,425,144,96));
        Assert.Null(ToyContactPhysics.ReachableContactPoint(0,0,19,19,1,96));
    }

    [Theory]
    [InlineData(56,96)]
    [InlineData(140,140)]
    public void TopAndBottomTangentContactsStayOnThePhysicalReachBoundary(double toyY,double expectedY)
    {
        var contact=ToyContactPhysics.ReachableContactPoint(62,toyY,0,0,144,96);
        Assert.NotNull(contact);Assert.Equal(82,contact.X,6);Assert.Equal(expectedY,contact.Y,6);
        Assert.Null(ToyContactPhysics.ReachableContactPoint(62,toyY+(toyY<100?-.001:.001),0,0,144,96));
    }

    [Theory]
    [InlineData(double.NaN,144,96)]
    [InlineData(0,0,96)]
    [InlineData(0,144,141)]
    public void NonFiniteOrInvalidReachGeometryIsRejected(double x,double height,double minimumY)
        =>Assert.Throws<ArgumentOutOfRangeException>(()=>ToyContactPhysics.ReachableContactPoint(x,0,0,0,height,minimumY));
}
