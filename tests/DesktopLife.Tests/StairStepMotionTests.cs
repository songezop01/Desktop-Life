using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class StairStepMotionTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var area in new[] { new BodyBounds(0, 0, 1920, 1040), new BodyBounds(-1080, -100, 1080, 1920), new BodyBounds(0, 0, 360, 640) })
            foreach (var descending in new[] { false, true }) yield return [area, descending];
    }
    [Theory] [MemberData(nameof(Cases))]
    public void BothZigzagStairsInBothDirectionsUseEveryRealTreadAndExactLandings(BodyBounds area, bool descending)
    {
        foreach (var stair in HouseLayout.Create(3, area).Stairs)
        {
            var motion = new StairStepMotion(stair, descending); Assert.Equal(16, motion.StepCount); Assert.Equal(17, motion.Anchors.Count);
            var anchors = descending ? motion.Anchors.Reverse().ToArray() : motion.Anchors.ToArray();
            Assert.Equal(stair.LowerLanding, anchors[0]); Assert.Equal(stair.UpperLanding, anchors[^1]);
            for (var i = 1; i <= 16; i++)
            {
                var tread = stair.Steps[i - 1]; Assert.InRange(anchors[i].X, tread.X - 1e-7, tread.X + tread.Width + 1e-7);
                Assert.Equal(tread.Y, anchors[i].Y, 7);
            }
            Assert.Equal(motion.Anchors[0], motion.SampleProgress(0).RootFeet);
            var end = motion.SampleProgress(1); Assert.True(end.Complete); Assert.True(end.LandingSettle); Assert.Equal(7.875, end.WalkCycle);
            Assert.Equal(motion.Anchors[^1], end.RootFeet); Assert.Equal(end.RootFeet, end.LeftFoot); Assert.Equal(end.RootFeet, end.RightFoot);
        }
    }
    [Theory] [MemberData(nameof(Cases))]
    public void RootAndNamedFeetRemainContinuousAtEveryStepBoundary(BodyBounds area, bool descending)
    {
        foreach (var stair in HouseLayout.Create(3, area).Stairs)
        {
            var motion = new StairStepMotion(stair, descending);
            for (var i = 1; i < 16; i++)
            {
                var before = motion.SampleProgress(i / 16d - 1e-9); var at = motion.SampleProgress(i / 16d); var after = motion.SampleProgress(i / 16d + 1e-9);
                Assert.True(Distance(before.RootFeet, after.RootFeet) < 1e-5);
                Assert.True(Distance(before.LeftFoot, after.LeftFoot) < 1e-5); Assert.True(Distance(before.RightFoot, after.RightFoot) < 1e-5);
                Assert.Equal(motion.Anchors[i], at.RootFeet); Assert.Equal(before.WalkCycle, at.WalkCycle); Assert.Equal(at.WalkCycle, after.WalkCycle);
            }
        }
    }
    [Theory] [MemberData(nameof(Cases))]
    public void PaintedStanceFrameRemainsIdenticalAcrossTreadBoundaryAndChangesOnlyDuringSwing(BodyBounds area, bool descending)
    {
        foreach (var stair in HouseLayout.Create(3, area).Stairs)
        {
            var motion = new StairStepMotion(stair, descending);
            var previous = motion.SampleProgress(0);
            for (var sample = 1; sample <= 16000; sample++)
            {
                var current = motion.SampleProgress(sample / 16000d);
                if (Frame(current.WalkCycle) != Frame(previous.WalkCycle))
                    Assert.Equal(0, current.PlantWeight);
                previous = current;
            }
            for (var boundary = 1; boundary < motion.StepCount; boundary++)
            {
                var before = motion.SampleProgress((boundary - .01) / motion.StepCount);
                var after = motion.SampleProgress((boundary + .01) / motion.StepCount);
                Assert.Equal(1, before.PlantWeight); Assert.Equal(1, after.PlantWeight);
                Assert.Equal(before.PlantSide, after.PlantSide);
                Assert.Equal(before.PlantPoint, after.PlantPoint);
                Assert.Equal(Frame(before.WalkCycle), Frame(after.WalkCycle));
            }
        }
        static int Frame(double cycle) => (int)Math.Floor((cycle - Math.Floor(cycle)) * 8 + .000001) % 8;
    }
    [Theory] [MemberData(nameof(Cases))]
    public void FullContactWindowsPinOneNamedFootAndTransferLiftsAcrossRisers(BodyBounds area, bool descending)
    {
        foreach (var stair in HouseLayout.Create(3, area).Stairs)
        {
            var motion = new StairStepMotion(stair, descending);
            for (var index = 0; index < 16; index++)
            {
                var early = motion.SampleProgress((index + .05) / 16); var late = motion.SampleProgress((index + .95) / 16);
                Assert.Equal(1, early.PlantWeight); Assert.Equal(1, late.PlantWeight);
                Assert.Equal(early.PlantPoint, early.PlantSide == StairFootSide.Left ? early.LeftFoot : early.RightFoot);
                Assert.Equal(late.PlantPoint, late.PlantSide == StairFootSide.Left ? late.LeftFoot : late.RightFoot);
                var transfer = motion.SampleProgress((index + .5) / 16); Assert.Equal(0, transfer.PlantWeight);
                var swinging = index % 2 == 0 ? transfer.LeftFoot : transfer.RightFoot;
                var from = motion.Anchors[Math.Max(0, index - 1)]; var target = motion.Anchors[index + 1];
                Assert.True(swinging.Y < Math.Min(from.Y, target.Y));
            }
        }
    }
    [Fact] public void SamplingContinuousPhysicsDoesNotAdvanceRouteOrManufactureOffStairContact()
    {
        var house = HouseLayout.Create(2, new(0, 0, 1200, 900)); var stair = house.Stairs.Single(); var motion = new StairStepMotion(stair, false);
        var travel = new HouseTravelWaypoint(HouseTravelKind.Stair, stair.LowerLanding, stair.UpperLanding, 0, 1, stair.Id);
        var physical = HouseTraversal.Advance(travel, travel.From, .016, 100); var original = physical.Feet;
        Assert.True(motion.TrySample(physical.Feet, out var pose)); Assert.NotNull(pose);
        for (var i = 0; i < 1000; i++) motion.TrySample(original, out _);
        Assert.Equal(original, physical.Feet); Assert.False(physical.Reached);
        Assert.False(motion.TrySample(new(original.X, original.Y - 20), out _));
        Assert.False(motion.TrySample(new(double.NaN, 0), out _));
        Assert.False(motion.TrySample(new(travel.From.X - 100, travel.From.Y), out _));
    }
    [Fact] public void GeometryMustMeetEveryRiserAndTheFinalLanding()
    {
        var stair = HouseLayout.Create(2, new(0, 0, 1200, 900)).Stairs.Single();
        var broken = stair.Steps.ToArray(); broken[3] = broken[3] with { Y = broken[3].Y + 4 };
        Assert.Throws<ArgumentException>(() => new StairStepMotion(stair with { Steps = broken }, false));
        Assert.Throws<ArgumentException>(() => new StairStepMotion(stair with { UpperLanding = stair.UpperLanding with { X = stair.UpperLanding.X + 1 } }, false));
        var motion = new StairStepMotion(stair, true);
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.SampleProgress(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.SampleProgress(1.01));
    }
    [Fact] public void RasterRootTransferHoldsContactsAndHasBoundedSpeedThroughTheWholeSwing()
    {
        Assert.Equal(0, StairStepMotion.TransferFraction(.12));
        Assert.Equal(1, StairStepMotion.TransferFraction(.9));
        var previous = StairStepMotion.TransferFraction(0);
        for (var sample = 1; sample <= 10000; sample++)
        {
            var current = StairStepMotion.TransferFraction(sample / 10000d);
            Assert.InRange(current - previous, 0, .00014);
            previous = current;
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => StairStepMotion.TransferFraction(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => StairStepMotion.TransferFraction(1.001));
    }
    private static double Distance(RoomPoint a, RoomPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
