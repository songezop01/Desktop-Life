using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class WalkDistanceSamplerTests
{
    [Theory]
    [InlineData(3, 4)]
    [InlineData(-3, -4)]
    [InlineData(3, -4)]
    [InlineData(-3, 4)]
    public void SupportedStairsCountActualPathInEitherDirection(double dx, double dy)
    {
        Assert.Equal(5, WalkDistanceSampler.Measure(new(100, 200, true), new(100 + dx, 200 + dy, true),
            1, WalkDistanceKind.Stair), 9);
    }

    [Fact]
    public void FloorStrideIgnoresSmallVerticalSupportCorrection()
    {
        Assert.Equal(3, WalkDistanceSampler.Measure(new(100, 200, true), new(103, 200.5, true),
            1, WalkDistanceKind.Floor), 9);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AirborneLaunchFlightAndLandingDoNotAdvanceWalk(bool groundedBefore, bool groundedAfter)
    {
        Assert.Equal(0, WalkDistanceSampler.Measure(new(100, 200, groundedBefore), new(103, 204, groundedAfter),
            1, WalkDistanceKind.Stair));
    }

    [Theory]
    [InlineData(WalkDistanceKind.Floor)]
    [InlineData(WalkDistanceKind.Stair)]
    public void DragAndRelocationSamplesNeverAdvanceWalk(WalkDistanceKind kind)
    {
        // Even a small placement on the same support is not locomotion.
        Assert.Equal(0, WalkDistanceSampler.Measure(new(100, 200, true), new(102, 200, true),
            1, kind, continuousMotion: false));
    }

    [Fact]
    public void LargeRelocationIsRejectedEvenWhenBothEndpointsAreGrounded()
    {
        Assert.Equal(0, WalkDistanceSampler.Measure(new(100, 200, true), new(300, 200, true), 1, WalkDistanceKind.Floor));
        Assert.Equal(0, WalkDistanceSampler.Measure(new(100, 200, true), new(101, 300, true), 1, WalkDistanceKind.Floor));
    }

    [Fact]
    public void StationaryAndNonWalkingUpdatesHaveNoStride()
    {
        Assert.Equal(0, WalkDistanceSampler.Measure(new(100, 200, true), new(100, 200, true), 1, WalkDistanceKind.Stair));
        Assert.Equal(0, WalkDistanceSampler.Measure(new(100, 200, true), new(103, 200, true), 1, WalkDistanceKind.None));
    }

    [Theory]
    [InlineData(.4)]
    [InlineData(1)]
    [InlineData(1.6)]
    public void CanonicalStrideIsIdenticalAtEverySceneAndDisplayScale(double scale)
    {
        Assert.Equal(5, WalkDistanceSampler.Measure(new(-100 * scale, 200 * scale, true),
            new(-97 * scale, 196 * scale, true), scale, WalkDistanceKind.Stair), 9);
        Assert.Equal(3, WalkDistanceSampler.Measure(new(-100 * scale, 200 * scale, true),
            new(-97 * scale, 200 * scale, true), scale, WalkDistanceKind.Floor), 9);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealHouseTraversalIncludesLastAdvanceAndMatchesWholeStairLength(bool downward)
    {
        var layout = HouseLayout.Create(2, new(0, 0, 1200, 900));
        var stair = layout.Stairs.Single();
        var step = new HouseTravelWaypoint(HouseTravelKind.Stair,
            downward ? stair.UpperLanding : stair.LowerLanding,
            downward ? stair.LowerLanding : stair.UpperLanding,
            downward ? 1 : 0, downward ? 0 : 1, stair.Id);
        var point = step.From;
        var distance = 0d;
        var reached = false;
        var lastDistance = 0d;
        var bodyScale = HouseLayout.GirlDesignHeight * layout.SceneScale / DesktopBody.Height;
        for (var tick = 0; tick < 2000 && !reached; tick++)
        {
            var advance = HouseTraversal.Advance(step, point, .016, 80 * layout.SceneScale);
            Assert.True(advance.Supported);
            lastDistance = WalkDistanceSampler.Measure(new(point.X, point.Y, true),
                new(advance.Feet.X, advance.Feet.Y, true), bodyScale, WalkDistanceKind.Stair);
            distance += lastDistance;
            point = advance.Feet;
            reached = advance.Reached;
        }
        Assert.True(reached);
        Assert.True(lastDistance > 0, "The landing advance must not be discarded when its waypoint finishes.");
        Assert.Equal(step.Length / bodyScale, distance, 7);
        Assert.True(distance > Math.Abs(step.To.X - step.From.X) / bodyScale);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidScaleIsRejected(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WalkDistanceSampler.Measure(new(0, 0, true),
            new(1, 0, true), scale, WalkDistanceKind.Floor));
    }

    [Fact]
    public void InvalidPositionCannotPoisonWalkPhase()
    {
        Assert.Equal(0, WalkDistanceSampler.Measure(new(double.NaN, 0, true), new(1, 0, true), 1, WalkDistanceKind.Floor));
        Assert.Equal(0, WalkDistanceSampler.Measure(new(0, 0, true), new(1, double.PositiveInfinity, true), 1, WalkDistanceKind.Stair));
    }
}
