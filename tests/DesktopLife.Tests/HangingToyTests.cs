using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class HangingToyTests
{
    [Fact]
    public void BatSwingsInContactDirectionAndKeepsStringLength()
    {
        var left = new HangingToy(); var right = new HangingToy();
        left.Bat(-180); right.Bat(180);
        left.Step(.1); right.Step(.1);
        Assert.True(left.X < 0); Assert.True(right.X > 0);
        Assert.Equal(HangingToy.Length, Math.Sqrt(left.X * left.X + left.Y * left.Y), 6);
    }
    [Fact]
    public void SwingDampsAndLongFrameDoesNotExplode()
    {
        var toy = new HangingToy(); toy.Bat(10000); toy.Step(40);
        Assert.InRange(toy.Angle, -HangingToy.MaximumAngle, HangingToy.MaximumAngle);
        Assert.True(45 + toy.X - 9 >= 0);
        for (var i = 0; i < 3000; i++) toy.Step(.01);
        Assert.InRange(Math.Abs(toy.Angle) + Math.Abs(toy.AngularVelocity), 0, .001);
    }
    [Fact]
    public void GroundedPlatformContactRemainsStableAtSmallFrameIntervals()
    {
        var body = new GravityBody { X = 100, Y = 20 };
        var bounds = new BodyBounds(0, 0, 1000, 800);
        RoomPlatform[] platforms = [new(90, 180, 300, 300)];
        for (var i = 0; i < 600; i++) body.Step(1d / 120, bounds, 116, 144, 0, platforms);
        Assert.True(body.Grounded); Assert.Equal(300, body.Y + 144, 6);
        body.X = 300; body.Step(.05, bounds, 116, 144, 0, platforms);
        Assert.False(body.Grounded); Assert.True(body.Y > 156);
    }
}
