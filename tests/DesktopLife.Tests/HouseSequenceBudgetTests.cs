using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class HouseSequenceBudgetTests
{
    [Fact]
    public void DefaultApproachKeepsTheExistingTwelveSecondSafetyBudget()
    {
        var sequence=Observe();EnterApproach(sequence);
        Assert.Equal(12,sequence.ApproachBudgetSeconds);
        for(var tick=0;tick<121;tick++)sequence.Step(.1,new());
        Assert.Equal(BehaviorInterruptReason.Safety,sequence.InterruptedBy);
        Assert.True(sequence.ApproachTimedOut);
        Assert.Equal(BehaviorPhase.Recover,sequence.Phase);
    }

    [Fact]
    public void ExplicitHouseRouteBudgetKeepsALongValidApproachUntilItsGroundedArrival()
    {
        var sequence=Observe(120);EnterApproach(sequence);
        for(var tick=0;tick<950;tick++)sequence.Step(.1,new());
        Assert.Equal(BehaviorPhase.Approach,sequence.Phase);
        Assert.Null(sequence.InterruptedBy);
        Assert.False(sequence.ApproachTimedOut);
        Assert.InRange(sequence.PhaseAge,94.9,95.1);
        sequence.Step(.016,new(Reached:true,Grounded:true));
        Assert.Equal(BehaviorPhase.Inspect,sequence.Phase);
        Assert.Null(sequence.InterruptedBy);
    }

    [Fact]
    public void ExplicitHouseRouteBudgetStillInterruptsAnApproachThatOutlivesItsEstimatedTime()
    {
        var sequence=Observe(90);EnterApproach(sequence);
        for(var tick=0;tick<899;tick++)sequence.Step(.1,new());
        Assert.Equal(BehaviorPhase.Approach,sequence.Phase);
        for(var tick=0;tick<3;tick++)sequence.Step(.1,new());
        Assert.Equal(BehaviorInterruptReason.Safety,sequence.InterruptedBy);
        Assert.Equal(BehaviorPhase.Recover,sequence.Phase);
    }

    [Fact]
    public void SleepingStillFallsBackWhenItsExplicitApproachBudgetExpires()
    {
        var sequence=new BehaviorSequence(SequenceKind.Sleep,new(),15,"bed",approachBudgetSeconds:45);
        EnterApproach(sequence);
        for(var tick=0;tick<451;tick++)sequence.Step(.1,new());
        Assert.Null(sequence.TargetId);
        Assert.True(sequence.ApproachTimedOut);
        Assert.Equal(BehaviorPhase.Inspect,sequence.Phase);
        Assert.Null(sequence.InterruptedBy);
    }

    [Fact]
    public void MissingTargetInterruptsEvenWhenTheEstimatedHouseTravelBudgetIsLong()
    {
        var sequence=Observe(600);EnterApproach(sequence);
        sequence.Step(.016,new(TargetExists:false));
        Assert.Equal(BehaviorInterruptReason.TargetLost,sequence.InterruptedBy);
        Assert.False(sequence.ApproachTimedOut);
        Assert.Equal(BehaviorPhase.Recover,sequence.Phase);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(11.999)]
    [InlineData(600.001)]
    public void InvalidBudgetsAreRejected(double seconds)=>
        Assert.Throws<ArgumentOutOfRangeException>(()=>Observe(seconds));

    [Theory]
    [InlineData(12)]
    [InlineData(600)]
    public void InclusiveBudgetLimitsAreAccepted(double seconds)=>
        Assert.Equal(seconds,Observe(seconds).ApproachBudgetSeconds);

    private static BehaviorSequence Observe(double budget=12)=>new(SequenceKind.Observe,new(),15,"house-desk",approachBudgetSeconds:budget);
    private static void EnterApproach(BehaviorSequence sequence)
    {
        for(var tick=0;tick<100&&sequence.Phase!=BehaviorPhase.Approach;tick++)sequence.Step(.016,new());
        Assert.Equal(BehaviorPhase.Approach,sequence.Phase);
    }
}
