using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class ObservedPlayExposureTests
{
    [Fact]
    public void AContactBetweenLifeTicksIsNotLostOrPromotedToAWholeSecond()
    {
        var initial = new PetState { Hunger = 25, Mood = 50, Boredom = 70, Energy = 70 };
        var life = new HomeostasisSession(initial, 8); var observed = new ObservedPlayExposure();
        Assert.True(observed.Record(PetAppearance.Cat, 1, .016));
        var result = ObservedPlaySettlement.Advance(life, observed, PetAppearance.Cat, TimeSpan.FromSeconds(1), BodyAction.Sit, true, false);
        var expected = CompanionCare.Step(CompanionCare.Step(initial, TimeSpan.FromSeconds(.984), BodyAction.Sit, true), TimeSpan.FromSeconds(.016), BodyAction.PlayToy, true);
        Assert.Equal(expected, life.State); Assert.Equal(1, result.Contacts); Assert.Equal(.016, result.Seconds, 8); Assert.Equal(9, life.TotalRuntimeSeconds, 8);
        var wholeSecond = CompanionCare.Step(initial, TimeSpan.FromSeconds(1), BodyAction.PlayToy, true);
        Assert.True(life.State.Boredom > wholeSecond.Boredom); Assert.Equal(default, observed.Peek(PetAppearance.Cat));
    }
    [Fact]
    public void SamplingAtContactDoesNotAlsoApplyAWholeSecondOfPlay()
    {
        var initial = new PetState(); var atContact = new HomeostasisSession(initial); var afterContact = new HomeostasisSession(initial);
        var a = new ObservedPlayExposure(); var b = new ObservedPlayExposure(); a.Record(PetAppearance.Cat, 1, .033); b.Record(PetAppearance.Cat, 1, .033);
        ObservedPlaySettlement.Advance(atContact, a, PetAppearance.Cat, TimeSpan.FromSeconds(1), BodyAction.PlayToy, true, true);
        ObservedPlaySettlement.Advance(afterContact, b, PetAppearance.Cat, TimeSpan.FromSeconds(1), BodyAction.Sit, true, false);
        Assert.Equal(afterContact.State, atContact.State); Assert.Equal(1, atContact.TotalRuntimeSeconds, 8);
    }
    [Fact]
    public void TrueCompletedContactsSurviveCancellationHiddenOrPausedButCannotBeReplayed()
    {
        var initial = new PetState(); var life = new HomeostasisSession(initial); var observed = new ObservedPlayExposure();
        observed.Record(PetAppearance.BorderCollie, 10, .02);
        var first = ObservedPlaySettlement.Advance(life, observed, PetAppearance.BorderCollie, TimeSpan.FromSeconds(1), BodyAction.Idle, false, false);
        var before = life.State; Assert.False(observed.Record(PetAppearance.BorderCollie, 10, .02));
        var second = ObservedPlaySettlement.Advance(life, observed, PetAppearance.BorderCollie, TimeSpan.FromSeconds(1), BodyAction.Idle, false, false);
        Assert.Equal(1, first.Contacts); Assert.Equal(0, second.Contacts); Assert.Equal(CompanionCare.Step(before, TimeSpan.FromSeconds(1), BodyAction.Idle, false), life.State);
    }
    [Fact]
    public void EvidenceCannotCrossResidentsOrWindowLedgers()
    {
        var observed = new ObservedPlayExposure(); observed.Record(PetAppearance.Cat, 1, .016); var initial = new PetState(); var dog = new HomeostasisSession(initial);
        var result = ObservedPlaySettlement.Advance(dog, observed, PetAppearance.BorderCollie, TimeSpan.FromSeconds(1), BodyAction.Idle, false, false);
        Assert.Equal(0, result.Contacts); Assert.Equal(CompanionCare.Step(initial, TimeSpan.FromSeconds(1), BodyAction.Idle, false), dog.State);
        Assert.Equal(1, observed.Peek(PetAppearance.Cat).Contacts); Assert.Equal(default, new ObservedPlayExposure().Peek(PetAppearance.Cat));
    }
    [Fact]
    public void PartialIntervalsRetainUnsettledSecondsAndIssueOneReceipt()
    {
        var observed = new ObservedPlayExposure(); observed.Record(PetAppearance.Cat, 1, .016); var life = new HomeostasisSession(new());
        var first = ObservedPlaySettlement.Advance(life, observed, PetAppearance.Cat, TimeSpan.FromSeconds(.006), BodyAction.Sit, true, false);
        var second = ObservedPlaySettlement.Advance(life, observed, PetAppearance.Cat, TimeSpan.FromSeconds(.01), BodyAction.Sit, true, false);
        Assert.Equal(.006, first.Seconds, 8); Assert.Equal(0, first.Contacts); Assert.Equal(.01, second.Seconds, 8); Assert.Equal(1, second.Contacts);
        Assert.Equal(.016, life.TotalRuntimeSeconds, 8); Assert.Equal(default, observed.Peek(PetAppearance.Cat));
    }
    [Fact]
    public void OfflineGapAppliesOfflineOnceAndOnlyAddsMeasuredActivityDifference()
    {
        var initial = new PetState { Energy = 60, Boredom = 60, Mood = 50, Hunger = 25 }; var expected = new HomeostasisSession(initial, 9);
        expected.AdvanceCompanion(TimeSpan.FromMinutes(30), BodyAction.Idle, false);
        var before = expected.State; var life = new HomeostasisSession(initial, 9); var observed = new ObservedPlayExposure(); observed.Record(PetAppearance.Cat, 1, .033);
        var result = ObservedPlaySettlement.Advance(life, observed, PetAppearance.Cat, TimeSpan.FromMinutes(30), BodyAction.Idle, false, false);
        Assert.Equal(1, result.Contacts); Assert.Equal(9, life.TotalRuntimeSeconds); Assert.Equal(before.Hunger, life.State.Hunger); Assert.Equal(before.Fatigue, life.State.Fatigue); Assert.Equal(before.Loneliness, life.State.Loneliness);
        Assert.True(life.State.Boredom < before.Boredom); Assert.True(life.State.Mood > before.Mood);
        var after = life.State; ObservedPlaySettlement.Advance(life, observed, PetAppearance.Cat, TimeSpan.Zero, BodyAction.Idle, false, false);
        Assert.Equal(after, life.State);
    }
    [Fact]
    public void IntentWithoutImpulseCannotGenerateExposure()
    {
        var initial = new PetState(); var life = new HomeostasisSession(initial); var observed = new ObservedPlayExposure();
        var result = ObservedPlaySettlement.Advance(life, observed, PetAppearance.Cat, TimeSpan.FromSeconds(1), BodyAction.PlayToy, true, true);
        Assert.Equal(default, result); Assert.Equal(CompanionCare.Step(initial, TimeSpan.FromSeconds(1), BodyAction.Sit, true), life.State);
        Assert.Throws<ArgumentOutOfRangeException>(() => observed.Record(PetAppearance.Girl, 1, .016));
        Assert.Throws<ArgumentOutOfRangeException>(() => observed.Record(PetAppearance.Cat, 1, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => observed.Record(PetAppearance.Cat, 1, .101));
    }
}
