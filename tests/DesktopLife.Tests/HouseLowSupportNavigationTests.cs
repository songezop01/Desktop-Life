using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class HouseLowSupportNavigationTests
{
    // The painted Box's interior is 100 - 98.05825242718446 DIP above its base.
    // A shared four-pixel support lookup must not mistake that interior for the floor.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(.75, -1200)]
    [InlineData(.5, 0)]
    [InlineData(.35, -1200)]
    public void LowBoxInteriorDropsOntoExactFloorEvenWhenGoalIsInsideBox(double scale, double origin)
    {
        var bounds = new BodyBounds(origin, 0, 1200, 900);
        var floor = new RoomPlatform(origin, 1200, 888, 888);
        var box = new RoomPlatform(origin + 400, 99.5145631067961 * scale,
            floor.Y - 1.94174757281554 * scale, floor.Y - 1.94174757281554 * scale);
        var center = box.X + box.Width / 2;
        var route = RoomNavigation.Plan([box, floor], bounds, center, box.Y, center, floor.Y,
            halfWidth: 20 * scale, bodyHeight: 80 * scale);

        var drop = Assert.Single(route!);
        Assert.True(drop.Drops);
        Assert.Equal(floor.Y, drop.LandingY);
        Assert.Equal(box.Y, drop.SourceY);
        Assert.True(drop.TakeoffX < box.X || drop.TakeoffX > box.X + box.Width);
        Assert.Equal(drop.TakeoffX, drop.LandingX);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactSupportWinsOverNearbySupportRegardlessOfInputOrder(bool boxFirst)
    {
        var bounds = new BodyBounds(0, 0, 1200, 900);
        var floor = new RoomPlatform(0, 1200, 888, 888);
        var box = new RoomPlatform(400, 100, 887.2, 887.2);
        var platforms = boxFirst ? new[] { box, floor } : new[] { floor, box };

        var exit = Assert.Single(RoomNavigation.Plan(platforms, bounds, 450, box.Y, 450, floor.Y)!);
        Assert.True(exit.Drops);
        var entry = Assert.Single(RoomNavigation.Plan(platforms, bounds, 450, floor.Y, 450, box.Y)!);
        Assert.False(entry.Drops);
        Assert.Equal(box.Y, entry.LandingY);
    }

    [Fact]
    public void RuntimeActuallyLeavesLowBoxBeforeRequestingFloor()
    {
        var bounds = new BodyBounds(0, 0, 1200, 900);
        var box = new RoomPlatform(400, 100, 898.0582524271845, 898.0582524271845);
        var actor = new DesktopLife.App.PetWindow(bounds, [box], 392, box.Y);
        var reachedFloor = false;
        for (var tick = 0; tick < 1000; tick++)
        {
            actor.MoveTo(392, 900, speed: 120);
            if (actor.Grounded && Math.Abs(actor.Position.Y + DesktopBody.Height - 900) < .001)
            { reachedFloor = true; break; }
        }
        Assert.True(reachedFloor, "A floor request must physically leave the low Box support.");
        Assert.Equal(0, actor.NavigationFailures);
        Assert.Equal(0, actor.UnreachableTargets);
    }
}
