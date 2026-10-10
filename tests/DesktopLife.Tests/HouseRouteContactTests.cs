using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class HouseRouteContactTests
{
    [Theory]
    [InlineData(1536, 816, 0, 80)]
    [InlineData(1536, 816, 0, 104)]
    [InlineData(1536, 816, 0, 256)]
    [InlineData(224, 816, -1200, 80)]
    [InlineData(224, 816, -1200, 104)]
    [InlineData(224, 816, -1200, 256)]
    public void WaitingFloorRouteRetainsRealFloorInsteadOfBeingLiftedOntoLowBox(
        double width, double height, double left, double designHeight)
    {
        var bounds = new BodyBounds(left, 0, width, height);
        var layout = HouseLayout.Create(3, bounds);
        var floor = layout.Floors[0];
        var feet = new RoomPoint(floor.Left + floor.Width * .4, floor.Y);
        var bodyHeight = designHeight * layout.SceneScale;
        var bodyWidth = bodyHeight * 116 / 144;
        var box = new RoomPlatform(feet.X - 10 * layout.SceneScale, 80 * layout.SceneScale,
            floor.Y - 1.94174757281554 * layout.SceneScale, floor.Y - 1.94174757281554 * layout.SceneScale);
        var step = HouseTraversal.Plan(layout, feet.X, feet.Y, 1, feet.X, bodyWidth / 2)![0];
        Assert.Equal(HouseTravelKind.FloorWalk, step.Kind);

        // This is the original failure: ordinary gravity prefers the nearby
        // Box interior even though the route was planted on the actual floor.
        var ordinary = new GravityBody { X = feet.X - bodyWidth / 2, Y = feet.Y - bodyHeight };
        ordinary.PlaceSupported(ordinary.X, ordinary.Y, bodyWidth, bodyHeight, floor.Platform);
        ordinary.Step(.016, bounds, bodyWidth, bodyHeight, 0, [box, floor.Platform]);
        Assert.Equal(box.Y, ordinary.Y + bodyHeight, 6);
        Assert.Null(HouseTraversal.Plan(layout, feet.X, ordinary.Y + bodyHeight, 1, feet.X, bodyWidth / 2));
        if (floor.Y - box.Y > .75)
            Assert.False(HouseTraversal.Advance(step, new(feet.X, ordinary.Y + bodyHeight), .016, 120).Supported);

        var waiting = new GravityBody { X = feet.X - bodyWidth / 2, Y = feet.Y - bodyHeight };
        for (var tick = 0; tick < 12; tick++)
        {
            var support = HouseTraversal.ContactSupport(layout, step, new(waiting.X + bodyWidth / 2, waiting.Y + bodyHeight));
            Assert.Equal(floor.Platform, support);
            waiting.PlaceSupported(waiting.X, waiting.Y, bodyWidth, bodyHeight, support!.Value);
            Assert.True(waiting.Grounded);
            Assert.Equal(feet.Y, waiting.Y + bodyHeight, 6);
        }
        var resumed = HouseTraversal.Advance(step, new(waiting.X + bodyWidth / 2, waiting.Y + bodyHeight), .016, 120);
        Assert.True(resumed.Supported);
        Assert.True(Distance(feet, resumed.Feet) <= 120 * .016 + .000001);
    }

    [Fact]
    public void OriginalFailureFurnitureFeetAreRejectedBeforeAnyHousePlan()
    {
        var layout = HouseLayout.Create(3, new(0, 0, 1536, 816));
        var feet = new RoomPoint(1184.834816689994 + 48.872366790582404 / 2, 805.4270761901325);
        Assert.Equal(806.8996282527881, layout.Floors[0].Y, 9);
        Assert.True(layout.FloorContactTolerance < .75);
        Assert.Null(layout.FindFloor(feet.X, feet.Y, layout.FloorContactTolerance));
        Assert.Null(HouseTraversal.Plan(layout, feet.X, feet.Y, 1, 1014.0718711276332));
        var step = HouseTraversal.Plan(layout, feet.X, layout.Floors[0].Y, 1, 1014.0718711276332)![0];
        Assert.Null(HouseTraversal.ContactSupport(layout, step, feet));
        Assert.False(HouseTraversal.Advance(step, feet, .016, 120).Supported);
    }

    [Fact]
    public void MissingRouteAndWrongFeetFallNaturallyWithoutClaimingSupport()
    {
        var bounds = new BodyBounds(0, 0, 1536, 816);
        var layout = HouseLayout.Create(3, bounds);
        var floor = layout.Floors[0];
        var start = new RoomPoint(500, floor.Y);
        var step = HouseTraversal.Plan(layout, start.X, start.Y, 1, 700)![0];
        Assert.Null(HouseTraversal.ContactSupport(layout, null, start));
        var above = start with { Y = start.Y - 40 };
        Assert.Null(HouseTraversal.ContactSupport(layout, step, above));
        Assert.False(HouseTraversal.Advance(step, above, .016, 120).Supported);
        var body = new GravityBody { X = above.X - 20, Y = above.Y - 80 };
        body.Step(.016, bounds, 40, 80, 0, layout.Platforms);
        Assert.False(body.Grounded);
        Assert.True(body.Y + 80 > above.Y);
        Assert.True(body.Y + 80 < floor.Y);
        Assert.Null(HouseTraversal.ContactSupport(layout, step, new(floor.Left - 10, floor.Y)));
    }

    [Fact]
    public void ReplacementFloorAndConnectorGeometryCannotKeepAnOldRouteSupported()
    {
        var oldLayout = HouseLayout.Create(3, new(0, 0, 1536, 816));
        var replacement = HouseLayout.Create(3, new(0, 0, 1536, 900));
        var narrowReplacement = HouseLayout.Create(3, new(0, 0, 1120, 816));
        var route = HouseTraversal.Plan(oldLayout, 500, oldLayout.Floors[0].Y, 1, 700)!;
        Assert.Null(HouseTraversal.ContactSupport(replacement, route[0], new(500, replacement.Floors[0].Y)));
        Assert.Equal(oldLayout.Floors[0].Y, narrowReplacement.Floors[0].Y);
        Assert.Null(HouseTraversal.ContactSupport(narrowReplacement, route[0], route[0].From));
        var stair = route.Single(s => s.Kind == HouseTravelKind.Stair);
        Assert.Null(HouseTraversal.ContactSupport(replacement, stair, replacement.Stairs[0].LowerLanding));
        Assert.Null(HouseTraversal.ContactSupport(oldLayout, route[0] with { SourceFloor = 9 }, route[0].From));
        Assert.Null(HouseTraversal.ContactSupport(oldLayout, stair with { ConnectorId = "removed" }, stair.From));
    }

    [Fact]
    public void RealStairContactIsPreservedWithoutChangingPausedPosition()
    {
        var layout = HouseLayout.Create(3, new(0, 0, 1536, 816));
        var step = HouseTraversal.Plan(layout, 500, layout.Floors[0].Y, 1, 700)!.Single(s => s.Kind == HouseTravelKind.Stair);
        var current = HouseTraversal.Advance(step, step.From, .08, 120).Feet;
        Assert.Equal(layout.Stairs[0].Support, HouseTraversal.ContactSupport(layout, step, current));
        Assert.Equal(current, HouseTraversal.Advance(step, current, 0, 120).Feet);
        Assert.Null(HouseTraversal.ContactSupport(layout, step, current with { Y = current.Y - 40 }));
    }

    private static double Distance(RoomPoint a, RoomPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
