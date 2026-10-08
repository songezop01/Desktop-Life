using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class MovingToyPursuitTests
{
    [Fact]
    public void InitialApproachUsesTheCurrentPhysicalEstimateAfterWatchingTheMovingToy()
    {
        var sequence=Play();EnterApproach(sequence);
        Assert.Equal(PlayApproachUpdate.Planned,sequence.RevisePlayApproach(new(100,200),0,240,30));
        Assert.Equal(30,sequence.ApproachBudgetSeconds);
        Advance(sequence,20);
        Assert.Equal(BehaviorPhase.Approach,sequence.Phase);
        Assert.False(sequence.ApproachTimedOut);
    }

    [Fact]
    public void AMeaningfulLateKickAddsOnlyThePhysicalRemainingEstimateWithoutResettingElapsedTime()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,14);
        Advance(sequence,11);var elapsed=sequence.PhaseAge;
        Assert.Equal(PlayApproachUpdate.Replanned,sequence.RevisePlayApproach(new(330,200),0,240,12));
        Assert.Equal(elapsed,sequence.PhaseAge);
        Assert.Equal(elapsed+12,sequence.ApproachBudgetSeconds,8);
        Advance(sequence,4);
        Assert.Equal(BehaviorPhase.Approach,sequence.Phase);
        sequence.Step(.016,new(Reached:true));
        Assert.Equal(BehaviorPhase.Prepare,sequence.Phase);
        Assert.False(sequence.ApproachTimedOut);
        Assert.Equal(1,sequence.PlayTargetRevisions);
    }

    [Fact]
    public void StationaryJitterAndStanceChangesCannotRenewAnApproach()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,12);
        for(var frame=0;frame<130;frame++)
        {
            var target=new RoomPoint(100+frame%2*20,200);
            Assert.False(sequence.NeedsPlayTargetRevision(target,0,frame%2==0?240:280));
            Assert.Equal(PlayApproachUpdate.Unchanged,sequence.RevisePlayApproach(target,0,280,50));
            sequence.Step(.1,new());
        }
        Assert.True(sequence.ApproachTimedOut);
        Assert.Equal(BehaviorInterruptReason.Safety,sequence.InterruptedBy);
        Assert.Equal(0,sequence.PlayTargetRevisions);
        Assert.Null(sequence.PlayApproachStopped);
    }

    [Fact]
    public void AirborneYMotionOnTheSameProjectedFloorCannotRenewTheBudget()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,500,12);Advance(sequence,2);
        Assert.False(sequence.NeedsPlayTargetRevision(new(100,100),0,500));
        Assert.Equal(PlayApproachUpdate.Unchanged,sequence.RevisePlayApproach(new(100,100),0,500,30));
        Assert.Equal(12,sequence.ApproachBudgetSeconds);
    }

    [Fact]
    public void ANewPhysicalShelfOnTheSameFloorCanReviseTheApproach()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,12);Advance(sequence,2);
        Assert.True(sequence.NeedsPlayTargetRevision(new(100,160),0,200));
        Assert.Equal(PlayApproachUpdate.Replanned,sequence.RevisePlayApproach(new(100,160),0,200,15));
        Assert.Equal(17,sequence.ApproachBudgetSeconds,8);
    }

    [Fact]
    public void FloorChangesReviseEvenWhenTheToyKeepsItsHorizontalPosition()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,30);Advance(sequence,2);
        Assert.Equal(PlayApproachUpdate.Replanned,sequence.RevisePlayApproach(new(100,200),1,150,20));
        Assert.Equal(1,sequence.PlayTargetRevisions);
    }

    [Fact]
    public void MovingTargetsAreDebouncedAndCannotReviseEveryFrame()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,30);
        Assert.Equal(PlayApproachUpdate.Unchanged,sequence.RevisePlayApproach(new(300,200),0,240,30));
        Advance(sequence,1.1);
        Assert.Equal(PlayApproachUpdate.Replanned,sequence.RevisePlayApproach(new(300,200),0,240,30));
        Assert.Equal(1,sequence.PlayTargetRevisions);
    }

    [Fact]
    public void RepeatedEscapesEndInBoundedWatchingWithNoContactOrTimeout()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,30);
        for(var revision=1;revision<=BehaviorSequence.MaximumPlayTargetRevisions;revision++)
        {
            Advance(sequence,1.1);
            Assert.Equal(PlayApproachUpdate.Replanned,sequence.RevisePlayApproach(new(100+100*revision,200),0,240,12));
        }
        Advance(sequence,1.1);
        Assert.Equal(PlayApproachUpdate.WatchingEscapingTarget,sequence.RevisePlayApproach(new(900,200),0,240,12));
        Assert.Equal(PlayApproachStoppedReason.EscapingTarget,sequence.PlayApproachStopped);
        Assert.Equal(BehaviorPhase.WatchBall,sequence.Phase);
        Advance(sequence,7);
        Assert.True(sequence.Finished);
        Assert.Equal(0,sequence.Contacts);
        Assert.False(sequence.ApproachTimedOut);
    }

    [Fact]
    public void OneDisplacementCannotExtendPursuitBeyondTheFinitePhysicalLimit()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,12);Advance(sequence,5);
        Assert.Equal(PlayApproachUpdate.WatchingEscapingTarget,sequence.RevisePlayApproach(new(900,200),0,240,40));
        Assert.Equal(PlayApproachStoppedReason.EscapingTarget,sequence.PlayApproachStopped);
        Assert.False(sequence.ApproachTimedOut);
    }

    [Fact]
    public void UnavailableSettledToyHasAnExplicitUnsuccessfulWatchingOutcome()
    {
        var sequence=Play();EnterApproach(sequence);
        Assert.True(sequence.WatchUnavailableToy());
        Assert.False(sequence.WatchUnavailableToy());
        Advance(sequence,7);
        Assert.True(sequence.Finished);
        Assert.Equal(PlayApproachStoppedReason.UnavailableSupport,sequence.PlayApproachStopped);
        Assert.Equal(0,sequence.Contacts);
        Assert.False(sequence.ApproachTimedOut);
    }

    [Fact]
    public void StationaryNavigationFailureStillInterruptsInsteadOfBecomingWatchingSuccess()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,30);
        sequence.Step(.016,new(NavigationFailed:true));
        Assert.Equal(BehaviorInterruptReason.Safety,sequence.InterruptedBy);
        Assert.Equal(BehaviorPhase.Recover,sequence.Phase);
        Assert.Null(sequence.PlayApproachStopped);
        Assert.Equal(0,sequence.Contacts);
    }

    [Fact]
    public void HeldToyTargetLossDoesNotWaitForAnApproachTimeout()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,30);
        sequence.Step(.016,new(TargetExists:false));Advance(sequence,1);
        Assert.True(sequence.Finished);
        Assert.Equal(BehaviorInterruptReason.TargetLost,sequence.InterruptedBy);
        Assert.False(sequence.ApproachTimedOut);
    }

    [Fact]
    public void ASecondApproachStartsWithFreshPlanningStateAndRetainsCumulativeRevisions()
    {
        var sequence=Play();EnterApproach(sequence);
        sequence.RevisePlayApproach(new(100,200),0,240,30);Advance(sequence,2);
        sequence.RevisePlayApproach(new(300,200),0,240,20);
        sequence.Step(.016,new(Reached:true));
        for(var frame=0;frame<100&&sequence.Phase!=BehaviorPhase.Contact;frame++)sequence.Step(.1,new());
        Assert.Equal(BehaviorPhase.Contact,sequence.Phase);
        sequence.Step(.1,new(Contact:true));
        for(var frame=0;frame<100&&sequence.Phase!=BehaviorPhase.Approach;frame++)sequence.Step(.1,new(TargetSpeed:100));
        Assert.Equal(BehaviorPhase.Approach,sequence.Phase);
        Assert.True(sequence.NeedsPlayTargetRevision(new(900,200),0,240));
        Assert.Equal(PlayApproachUpdate.Planned,sequence.RevisePlayApproach(new(900,200),0,240,12));
        Assert.Equal(12,sequence.ApproachBudgetSeconds);
        Assert.Equal(1,sequence.PlayTargetRevisions);
        Advance(sequence,1.1);
        Assert.Equal(PlayApproachUpdate.Replanned,sequence.RevisePlayApproach(new(1100,200),0,240,12));
        Assert.Equal(2,sequence.PlayTargetRevisions);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(11.9)]
    [InlineData(600.1)]
    public void InvalidRevisionBudgetsAreRejected(double budget)
    {
        var sequence=Play();EnterApproach(sequence);
        Assert.Throws<ArgumentOutOfRangeException>(()=>sequence.RevisePlayApproach(new(100,200),0,240,budget));
    }

    private static BehaviorSequence Play()=>new(SequenceKind.Play,new(),50,"ball",character:PetAppearance.BorderCollie);
    private static void Advance(BehaviorSequence sequence,double seconds)
    {for(var frame=0;frame<(int)Math.Round(seconds/.1);frame++)sequence.Step(.1,new());}
    private static void EnterApproach(BehaviorSequence sequence)
    {
        for(var frame=0;frame<100&&sequence.Phase!=BehaviorPhase.Approach;frame++)sequence.Step(.1,new());
        Assert.Equal(BehaviorPhase.Approach,sequence.Phase);
    }
}
