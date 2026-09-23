using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;
public class HomeostasisTests
{
    private static readonly DateTimeOffset Now = new(2026,9,11,0,0,0,TimeSpan.Zero);
    private static EnvironmentState Environment(double load, double idle = 0)
    {
        var v = SensorValue.Available;
        return new(Now,v(load*100),v(load*100),v(load*100),v(load*20*1024*1024),v(load*20*1024*1024),
            v(load*2*1024*1024),v(load*2*1024*1024),v(idle),v(load*400),1);
    }
    private static PetState Simulate(EnvironmentFeeding feeding, int seconds, bool resting = false, PetState? initial = null)
    {
        var state = initial ?? new PetState();
        for (var i=0;i<seconds;i++) state = Homeostasis.Step(state,feeding,TimeSpan.FromSeconds(1),resting);
        return state;
    }
    [Fact] public void SustainedActivityFeedsAndImprovesMood()
    {
        var before = new PetState(); var state = Simulate(EnvironmentFeedingEncoder.Encode(Environment(1),Now),600);
        Assert.True(state.Energy > before.Energy); Assert.True(state.Hunger < before.Hunger);
        Assert.True(state.Mood > before.Mood); Assert.True(state.Excitement > before.Excitement);
        Assert.True(state.Boredom < before.Boredom); Assert.True(state.Loneliness < before.Loneliness);
    }
    [Fact] public void QuietUnattendedComputerIncreasesNeeds()
    {
        var before = new PetState(); var state = Simulate(EnvironmentFeedingEncoder.Encode(Environment(0,3600),Now),600);
        Assert.True(state.Energy < before.Energy); Assert.True(state.Hunger > before.Hunger);
        Assert.True(state.Mood < before.Mood); Assert.True(state.Boredom > before.Boredom); Assert.True(state.Loneliness > before.Loneliness);
    }
    [Fact] public void RestRecoversFatigueAndEnergyRelativeToAwake()
    {
        var feed = new EnvironmentFeeding(.5,.5,"test");
        var resting = Simulate(feed,600,true); var awake = Simulate(feed,600);
        Assert.True(resting.Fatigue < awake.Fatigue); Assert.True(resting.Energy > awake.Energy);
    }
    [Fact] public void MissingGpuDoesNotMeanZeroLoad()
    {
        var all = EnvironmentFeedingEncoder.Encode(Environment(1),Now);
        var partial = EnvironmentFeedingEncoder.Encode(Environment(1) with { GpuPercent = SensorValue.Missing("unsupported") },Now);
        Assert.Equal(all.Level!.Value,partial.Level!.Value,10);
        Assert.Contains("部分",partial.Status);
    }
    [Fact] public void MissingAndStaleSnapshotsDoNotPunish()
    {
        var state = new PetState();
        Assert.Equal(state,Homeostasis.Step(state,EnvironmentFeedingEncoder.Encode(null,Now),TimeSpan.FromSeconds(1),false));
        Assert.Equal(state,Homeostasis.Step(state,EnvironmentFeedingEncoder.Encode(Environment(0),Now.AddSeconds(4)),TimeSpan.FromSeconds(1),false));
        Assert.Null(EnvironmentFeedingEncoder.Encode(Environment(1),Now.AddSeconds(-5)).Level);
    }
    [Fact] public void MissingIdleDoesNotIncreaseLoneliness()
    {
        var feed = EnvironmentFeedingEncoder.Encode(Environment(0) with { IdleSeconds = SensorValue.Missing("locked") },Now);
        Assert.Equal(20,Simulate(feed,60).Loneliness);
    }
    [Fact] public void CompletelyUnavailableDataFreezesState()
    {
        var missing = SensorValue.Missing("unavailable");
        var environment = new EnvironmentState(Now,missing,missing,missing,missing,missing,missing,missing,missing,missing,0);
        Assert.Null(EnvironmentFeedingEncoder.Encode(environment,Now).Level);
    }
    [Fact] public void MalformedSensorValuesCannotContaminateState()
    {
        var feed = EnvironmentFeedingEncoder.Encode(Environment(1) with { CpuPercent = new(double.NaN,"bad"), IdleSeconds = new(-1,"bad") },Now);
        var state = Simulate(feed,60); state.Validate(); Assert.True(state.Energy > 65);
    }
    [Fact] public void EquivalentTimestepsProduceSameEnergy()
    {
        var feed = new EnvironmentFeeding(.6,.4,"test");
        var seconds = Simulate(feed,60); var tens = new PetState();
        for(var i=0;i<6;i++) tens = Homeostasis.Step(tens,feed,TimeSpan.FromSeconds(10),false);
        Assert.Equal(seconds.Energy,tens.Energy,9); Assert.Equal(seconds.Excitement,tens.Excitement,9);
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void ManyHoursNeverEscapeValidRange(double feeding)
    {
        var state = new PetState(); var feed = new EnvironmentFeeding(feeding,feeding,"test");
        for(var i=0;i<20000;i++) { state = Homeostasis.Step(state,feed,TimeSpan.FromSeconds(10),false); state.Validate(); }
    }
    [Fact] public void OneWeekOfflineHasSameCapAsEightHours()
    {
        var state = new PetState();
        Assert.Equal(Homeostasis.ApplyOffline(state,TimeSpan.FromHours(8)),Homeostasis.ApplyOffline(state,TimeSpan.FromDays(7)));
        Assert.Equal(state,Homeostasis.ApplyOffline(state,TimeSpan.FromHours(-1)));
    }
    [Fact] public void WorstStateRemainsRecoverable()
    {
        var low = new PetState { Energy=0,Hunger=100,Mood=0,Boredom=100,Loneliness=100,Fatigue=100 };
        var state = Simulate(new(1,1,"test"),600,true,low);
        Assert.True(state.Energy > 0); Assert.True(state.Hunger < 100); Assert.True(state.Mood > 0); Assert.True(state.Fatigue < 100);
    }
    [Fact] public void LongGapIsOfflineNotCurrentActivityMultiplied()
    {
        var session = new HomeostasisSession(new PetState());
        session.Advance(Environment(1),Now,TimeSpan.FromDays(7),false);
        Assert.Equal(Homeostasis.ApplyOffline(new(),TimeSpan.FromHours(8)),session.State);
        Assert.Equal(0,session.TotalRuntimeSeconds);
    }
    [Fact] public void ResumeCanContinueFromCappedOfflineState()
    {
        var session = new HomeostasisSession(new()); session.ApplyOffline(TimeSpan.FromDays(7)); var energy=session.State.Energy;
        session.Advance(Environment(1),Now,TimeSpan.FromSeconds(1),false);
        Assert.True(session.State.Energy > energy); Assert.Equal(1,session.TotalRuntimeSeconds);
    }
    [Fact] public void NegativeElapsedAndInvalidStatesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Homeostasis.Step(new(),new(1,1,"test"),TimeSpan.FromSeconds(-1),false));
        Assert.Throws<InvalidDataException>(() => new PetState { Energy=double.NaN }.Validate());
        Assert.Throws<InvalidDataException>(() => new PetState { Hunger=101 }.Validate());
    }
}
