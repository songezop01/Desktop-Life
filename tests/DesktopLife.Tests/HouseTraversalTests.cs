using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class HouseTraversalTests
{
    [Theory]
    [MemberData(nameof(HouseLayoutTests.WorkAreas), MemberType = typeof(HouseLayoutTests))]
    public void EveryFloorPairIsReachableWithContinuousSupportedTravel(int count, BodyBounds area)
    {
        var layout = HouseLayout.Create(count, area);
        var halfWidth = 116d / 144 * HouseLayout.GirlDesignHeight * layout.SceneScale / 2;
        foreach (var source in layout.Floors)
            foreach (var destination in layout.Floors)
            {
                var start = new RoomPoint((source.Left + source.Right) / 2, source.Y);
                var goalX = destination.Left + destination.Width * .6;
                var route = HouseTraversal.Plan(layout, start.X, start.Y, destination.Index, goalX, halfWidth);
                Assert.NotNull(route);
                Assert.Equal(Math.Abs(destination.Index - source.Index), route.Count(s => s.Kind == HouseTravelKind.Stair));
                var point = start;
                foreach (var step in route)
                {
                    Assert.Equal(point, step.From);
                    var reached = false;
                    var ticks = 0;
                    while (!reached && ticks++ < 3000)
                    {
                        var previous = point;
                        var progress = HouseTraversal.Advance(step, point, .016, 100 * layout.SceneScale);
                        Assert.True(progress.Supported);
                        point = progress.Feet;
                        reached = progress.Reached;
                        Assert.True(Distance(previous, point) <= 1.600001 * layout.SceneScale);
                        if (step.Kind == HouseTravelKind.Stair)
                        {
                            var stair = layout.Stairs.Single(s => s.Id == step.ConnectorId);
                            Assert.Equal(stair.Support.HeightAt(point.X), point.Y, 6);
                        }
                        else Assert.Equal(layout.Floors[step.SourceFloor].Y, point.Y, 6);
                    }
                    Assert.True(reached, "Stair traversal must reach its landing without waiting for an impossible jump.");
                    Assert.Equal(step.To, point);
                }
                Assert.Equal(layout.SafeFootX(destination.Index, goalX, halfWidth), point.X, 6);
                Assert.Equal(destination.Y, point.Y, 6);
            }
    }

    [Fact]
    public void UpwardAndDownwardJourneysReserveTheSameConnector()
    {
        var layout = HouseLayout.Create(3, new(0, 0, 1920, 1040));
        var up = HouseTraversal.Plan(layout, 700, layout.Floors[0].Y, 2, 700)!;
        var down = HouseTraversal.Plan(layout, 700, layout.Floors[2].Y, 0, 700)!;
        Assert.Equal(up.Where(s => s.Kind == HouseTravelKind.Stair).Select(s => s.ConnectorId),
            down.Where(s => s.Kind == HouseTravelKind.Stair).Select(s => s.ConnectorId).Reverse());
    }

    [Fact]
    public void MidairOrFurnitureSourceDoesNotTeleportIntoAHouseRoute()
    {
        var layout = HouseLayout.Create(3, new(0, 0, 1200, 900));
        Assert.Null(HouseTraversal.Plan(layout, 500, layout.Floors[0].Y - 120, 1, 500));
        Assert.Null(HouseTraversal.Plan(layout, 500, layout.Floors[1].Y + 20, 2, 500));
    }

    [Fact]
    public void PausedTickKeepsStairFeetInExactlyTheSamePlace()
    {
        var stair = HouseLayout.Create(2, new(0, 0, 1200, 900)).Stairs.Single();
        var step = new HouseTravelWaypoint(HouseTravelKind.Stair, stair.LowerLanding, stair.UpperLanding, 0, 1, stair.Id);
        var first = HouseTraversal.Advance(step, stair.LowerLanding, .08, 100).Feet;
        var paused = HouseTraversal.Advance(step, first, 0, 100);
        Assert.Equal(first, paused.Feet);
        Assert.True(paused.Supported);
        Assert.False(paused.Reached);
    }

    [Fact]
    public void LongSuspendCannotSkipAnEntireStairway()
    {
        var stair = HouseLayout.Create(2, new(0, 0, 1200, 900)).Stairs.Single();
        var step = new HouseTravelWaypoint(HouseTravelKind.Stair, stair.LowerLanding, stair.UpperLanding, 0, 1, stair.Id);
        var progress = HouseTraversal.Advance(step, stair.LowerLanding, 600, 100);
        Assert.True(progress.Supported);
        Assert.False(progress.Reached);
        Assert.Equal(10, Distance(stair.LowerLanding, progress.Feet), 6);
    }

    [Fact]
    public void DraggedCharacterOrRemovedSupportIsReportedInsteadOfSnappedBack()
    {
        var stair = HouseLayout.Create(2, new(0, 0, 1200, 900)).Stairs.Single();
        var step = new HouseTravelWaypoint(HouseTravelKind.Stair, stair.LowerLanding, stair.UpperLanding, 0, 1, stair.Id);
        var displaced = new RoomPoint(stair.LowerLanding.X, stair.LowerLanding.Y - 40);
        var progress = HouseTraversal.Advance(step, displaced, .016, 100);
        Assert.Equal(displaced, progress.Feet);
        Assert.False(progress.Supported);
        Assert.False(progress.Reached);
    }

    [Fact]
    public void BodyTooWideForTheDisplayCannotReceiveAnUnsafeRoute()
    {
        var layout = HouseLayout.Create(2, new(0, 0, 320, 700));
        Assert.Null(HouseTraversal.Plan(layout, 160, layout.Floors[0].Y, 1, 160, 160));
    }

    [Fact]
    public void SameFloorRouteClampsGoalToFullBodyClearance()
    {
        var layout = HouseLayout.Create(1, new(-1280, -100, 1280, 700));
        var floor = layout.Floors.Single();
        var route = HouseTraversal.Plan(layout, -600, floor.Y, 0, 10000, 103.12)!;
        Assert.Equal(floor.Right - 103.12, Assert.Single(route).To.X, 6);
    }

    private static double Distance(RoomPoint a, RoomPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
