using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class RenderPositionSamplerTests
{
    [Fact] public void RenderingInterpolatesBetweenCommittedSamplesAndNeverExtrapolates()
    {
        var sampler = new RenderPositionSampler(); sampler.Snap(new(0, 20), 0);
        sampler.Record(new(10, 20), .016, true);
        Assert.Equal(new(0, 20), sampler.Sample(.016)); Assert.Equal(5, sampler.Sample(.024).X, 8);
        Assert.Equal(new(10, 20), sampler.Sample(.032)); Assert.Equal(new(10, 20), sampler.Sample(600));
        Assert.True(sampler.Pending(.024)); Assert.False(sampler.Pending(.033));
        Assert.Equal(new(10, 20), sampler.Latest);
    }
    [Fact] public void StopSameTimeGeometryChangeAndResumeDoNotReuseAnOldTrajectory()
    {
        var sampler = new RenderPositionSampler(); sampler.Snap(new(0, 0), 0);
        sampler.Record(new(8, 0), .016, true); sampler.Record(new(8, 0), .02, false);
        Assert.False(sampler.Interpolating); Assert.Equal(new(8, 0), sampler.Sample(.02));
        sampler.Record(new(9, 0), .02, true); Assert.Equal(new(9, 0), sampler.Sample(.02));
        sampler.Record(new(10, 0), .036, true, geometryToken: 1); Assert.False(sampler.Interpolating);
        Assert.Equal(new(10, 0), sampler.Sample(.036));
        sampler.Record(new(11, 0), .052, true, geometryToken: 1); Assert.True(sampler.Interpolating);
        sampler.Snap(new(-100, 80), .06, 2); Assert.Equal(new(-100, 80), sampler.Sample(.061));
    }
    [Fact] public void LongGapsTeleportsAndNewVerticalFloorSupportSnapImmediately()
    {
        var sampler = new RenderPositionSampler(); sampler.Snap(new(0, 0), 0);
        sampler.Record(new(5, 0), 600, true); Assert.Equal(new(5, 0), sampler.Sample(600)); Assert.False(sampler.Interpolating);
        sampler.Record(new(100, 0), 600.016, true); Assert.False(sampler.Interpolating);
        sampler.Record(new(101, -22), 600.032, true); Assert.False(sampler.Interpolating);
        sampler.Record(new(102, -24), 600.048, true, allowVerticalMotion: true); Assert.True(sampler.Interpolating);
        Assert.InRange(sampler.Sample(600.056).Y, -24, -22);
    }
    [Theory] [InlineData(.35)] [InlineData(1)] [InlineData(1.8)]
    public void SceneScaleAndNegativeMonitorOriginKeepTheSameBoundedTrajectory(double scale)
    {
        var sampler = new RenderPositionSampler(); sampler.Snap(new(-1200, -80), 0);
        sampler.Record(new(-1200 + 10 * scale, -80), .016, true, scale);
        Assert.Equal(-1200 + 5 * scale, sampler.Sample(.024).X, 8);
        sampler.Record(new(-1200 + 100 * scale, -80), .032, true, scale); Assert.False(sampler.Interpolating);
    }
    [Fact] public void BackwardClockAndDuplicateIdleUpdatesCannotDivideByZeroOrMoveTheModel()
    {
        var sampler = new RenderPositionSampler(); var model = new RoomPoint(100, 200); sampler.Snap(model, 1);
        sampler.Record(model, 1.016, true); Assert.False(sampler.Interpolating);
        sampler.Record(new(101, 200), .9, true); Assert.Equal(new(101, 200), sampler.Sample(.9));
        for (var i = 0; i < 1000; i++) sampler.Sample(i);
        Assert.Equal(new(100, 200), model); Assert.Equal(new(101, 200), sampler.Latest);
    }
    [Fact] public void ScaleChangeSnapsEvenIfTheCallerForgetsToChangeItsGeometryToken()
    {
        var sampler = new RenderPositionSampler(); sampler.Snap(new(0, 0), 0);
        sampler.Record(new(10, 0), .016, true, 1); Assert.True(sampler.Interpolating);
        sampler.Record(new(11, 0), .032, true, .5); Assert.False(sampler.Interpolating);
        Assert.Equal(new(11, 0), sampler.Sample(.032));
    }
}
