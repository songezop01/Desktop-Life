using System.Text.Json;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class DirectCareTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
    private static DirectCareResult Apply(DirectCareSession session, DirectCareContact contact, PetState pet, CompanionState companion,
        DateTimeOffset? now = null, bool physical = true, PetAppearance expected = PetAppearance.Cat, CareKind kind = CareKind.Pet)
        => session.Apply(contact, expected, kind, pet, companion, now ?? Epoch, physical);
    private static void Unchanged(DirectCareResult result, PetState pet, CompanionState companion, string companionBefore, DirectCareFailure reason)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Failure); Assert.Same(pet, result.Pet); Assert.Same(companion, result.Companion);
        Assert.Equal(companionBefore, JsonSerializer.Serialize(companion));
    }

    [Fact] public void OnlyVerifiedContactReturnsASeparatePetAndCompanionPair()
    {
        var session = new DirectCareSession(); var pet = new PetState { Mood = 60, Loneliness = 30 }; var companion = new CompanionState();
        var before = JsonSerializer.Serialize(companion);
        Unchanged(Apply(session, session.CreateContact(PetAppearance.Cat, CareKind.Pet), pet, companion, physical: false), pet, companion, before, DirectCareFailure.NoContact);
        var accepted = Apply(session, session.CreateContact(PetAppearance.Cat, CareKind.Pet), pet, companion);
        Assert.True(accepted.Accepted); Assert.Equal(65, accepted.Pet.Mood); Assert.Equal(18, accepted.Pet.Loneliness);
        Assert.Equal(15.25, accepted.Companion.Bond); Assert.Equal(1, accepted.Companion.CareCount); Assert.Equal(Epoch, accepted.Companion.LastCare[CareKind.Pet]);
        Assert.Single(accepted.Companion.Memories); Assert.NotSame(companion.Memories, accepted.Companion.Memories); Assert.NotSame(companion.LastCare, accepted.Companion.LastCare);
        Assert.Equal(before, JsonSerializer.Serialize(companion)); Assert.Equal(60, pet.Mood); Assert.Equal(30, pet.Loneliness);
        accepted.Companion.Memories.Clear(); accepted.Companion.LastCare.Clear(); Assert.Equal(before, JsonSerializer.Serialize(companion));
    }

    [Fact] public void DuplicateContactCannotBeRewardedAfterCooldown()
    {
        var session = new DirectCareSession(); var contact = session.CreateContact(PetAppearance.Cat, CareKind.Pet);
        var first = Apply(session, contact, new(), new()); Assert.True(first.Accepted);
        var before = JsonSerializer.Serialize(first.Companion);
        Unchanged(Apply(session, contact, first.Pet, first.Companion, Epoch.AddHours(1)), first.Pet, first.Companion, before, DirectCareFailure.Duplicate);
    }

    [Theory] [InlineData(CareKind.Pet, 8)] [InlineData(CareKind.Groom, 20)]
    public void ExactCooldownBoundaryRequiresANewPhysicalEvent(CareKind kind, int seconds)
    {
        var session = new DirectCareSession(); var first = Apply(session, session.CreateContact(PetAppearance.Cat, kind), new(), new(), kind: kind);
        Assert.True(first.Accepted); var before = JsonSerializer.Serialize(first.Companion);
        var tooSoon = session.CreateContact(PetAppearance.Cat, kind);
        var rejected = Apply(session, tooSoon, first.Pet, first.Companion, Epoch.AddSeconds(seconds).AddMilliseconds(-1), kind: kind);
        Unchanged(rejected, first.Pet, first.Companion, before, DirectCareFailure.Cooldown); Assert.InRange(rejected.CooldownRemainingSeconds, .0009, .0011);
        Unchanged(Apply(session, tooSoon, first.Pet, first.Companion, Epoch.AddMinutes(1), kind: kind), first.Pet, first.Companion, before, DirectCareFailure.Duplicate);
        var next = Apply(session, session.CreateContact(PetAppearance.Cat, kind), first.Pet, first.Companion, Epoch.AddSeconds(seconds), kind: kind);
        Assert.True(next.Accepted); Assert.Equal(2, next.Companion.CareCount); Assert.Single(next.Companion.Memories);
    }

    [Fact] public void ResetRetiresQueuedEventsButDoesNotErasePersistentCooldown()
    {
        var session = new DirectCareSession(); var accepted = Apply(session, session.CreateContact(PetAppearance.Cat, CareKind.Pet), new(), new());
        var pending = session.CreateContact(PetAppearance.Cat, CareKind.Pet); var oldSession = session.SessionId; session.Reset();
        Assert.NotEqual(oldSession, session.SessionId); Assert.Equal(0, session.AttemptedContacts); var before = JsonSerializer.Serialize(accepted.Companion);
        Unchanged(Apply(session, pending, accepted.Pet, accepted.Companion, Epoch.AddMinutes(1)), accepted.Pet, accepted.Companion, before, DirectCareFailure.StaleSession);
        Unchanged(Apply(session, session.CreateContact(PetAppearance.Cat, CareKind.Pet), accepted.Pet, accepted.Companion, Epoch.AddSeconds(1)), accepted.Pet, accepted.Companion, before, DirectCareFailure.Cooldown);
    }

    [Fact] public void BoundedSessionRotatesWithoutRevivingRetiredContactIds()
    {
        var session = new DirectCareSession(); var pet = new PetState(); var companion = new CompanionState();
        var first = session.CreateContact(PetAppearance.Cat, CareKind.Pet);
        for (var i = 0; i < DirectCareSession.MaximumContacts; i++)
            Assert.False(Apply(session, i == 0 ? first : session.CreateContact(PetAppearance.Cat, CareKind.Pet), pet, companion, physical: false).Accepted);
        Assert.Equal(DirectCareSession.MaximumContacts, session.AttemptedContacts); var retired = session.SessionId;
        var next = session.CreateContact(PetAppearance.Cat, CareKind.Pet); Assert.NotEqual(retired, next.SessionId); Assert.Equal(0, session.AttemptedContacts);
        var before = JsonSerializer.Serialize(companion);
        Unchanged(Apply(session, first, pet, companion), pet, companion, before, DirectCareFailure.StaleSession);
        Assert.True(Apply(session, next, pet, companion).Accepted);
    }

    [Theory]
    [InlineData(PetAppearance.Cat, CareKind.Groom, PetAppearance.Cat, CareKind.Pet, DirectCareFailure.WrongKind)]
    [InlineData(PetAppearance.Cat, CareKind.Feed, PetAppearance.Cat, CareKind.Feed, DirectCareFailure.WrongKind)]
    [InlineData(PetAppearance.Cat, CareKind.Rest, PetAppearance.Cat, CareKind.Pet, DirectCareFailure.WrongKind)]
    [InlineData(PetAppearance.Girl, CareKind.Pet, PetAppearance.Cat, CareKind.Pet, DirectCareFailure.WrongResident)]
    [InlineData((PetAppearance)999, CareKind.Pet, PetAppearance.Cat, CareKind.Pet, DirectCareFailure.UnknownResident)]
    [InlineData(PetAppearance.Cat, (CareKind)999, PetAppearance.Cat, CareKind.Pet, DirectCareFailure.WrongKind)]
    public void WrongActorOrToolCannotMutateAnyCareEvidence(PetAppearance requested, CareKind requestedKind, PetAppearance actor, CareKind tool, DirectCareFailure failure)
    {
        var session = new DirectCareSession(); var pet = new PetState(); var companion = new CompanionState(); var before = JsonSerializer.Serialize(companion);
        Unchanged(Apply(session, session.CreateContact(requested, requestedKind), pet, companion, expected: actor, kind: tool), pet, companion, before, failure);
    }

    [Fact] public void InvalidStateRejectsBeforeCareCountBondMemoryOrNeedsChange()
    {
        var session = new DirectCareSession(); var invalidPet = new PetState { Mood = double.NaN }; var companion = new CompanionState(); var before = JsonSerializer.Serialize(companion);
        Unchanged(Apply(session, session.CreateContact(PetAppearance.Cat, CareKind.Pet), invalidPet, companion), invalidPet, companion, before, DirectCareFailure.InvalidState);
        var pet = new PetState(); var invalidCompanion = new CompanionState { CareCount = int.MaxValue }; before = JsonSerializer.Serialize(invalidCompanion);
        Unchanged(Apply(session, session.CreateContact(PetAppearance.Cat, CareKind.Pet), pet, invalidCompanion), pet, invalidCompanion, before, DirectCareFailure.InvalidState);
        invalidCompanion = new() { Bond = 101 }; before = JsonSerializer.Serialize(invalidCompanion);
        Unchanged(Apply(session, session.CreateContact(PetAppearance.Cat, CareKind.Pet), pet, invalidCompanion), pet, invalidCompanion, before, DirectCareFailure.InvalidState);
    }

    [Theory]
    [InlineData(DirectCareBlock.Dragging)] [InlineData(DirectCareBlock.Sleeping)] [InlineData(DirectCareBlock.Stairs)]
    [InlineData(DirectCareBlock.Occluded)] [InlineData(DirectCareBlock.Paused)] [InlineData(DirectCareBlock.Hidden)] [InlineData(DirectCareBlock.Editing)]
    public void BlockedPhysicalContactIsTerminalAndCannotChangeCareEvidence(DirectCareBlock blocked)
    {
        var session = new DirectCareSession(); var pet = new PetState(); var companion = new CompanionState();
        var contact = session.CreateContact(PetAppearance.Cat, CareKind.Groom); var before = JsonSerializer.Serialize(companion);
        Unchanged(session.Apply(contact, PetAppearance.Cat, CareKind.Groom, pet, companion, Epoch, physicalContact: true, blocked: blocked),
            pet, companion, before, DirectCareFailure.Blocked);
        Unchanged(session.Apply(contact, PetAppearance.Cat, CareKind.Groom, pet, companion, Epoch.AddMinutes(1), physicalContact: true),
            pet, companion, before, DirectCareFailure.Duplicate);
        Assert.True(session.Apply(session.CreateContact(PetAppearance.Cat, CareKind.Groom), PetAppearance.Cat, CareKind.Groom,
            pet, companion, Epoch.AddMinutes(1), physicalContact: true).Accepted);
    }

    [Fact] public void ThreeResidentsKeepTheirOwnCooldownAndGroomDoesNotUsePetCooldown()
    {
        var session = new DirectCareSession();
        foreach (var actor in Enum.GetValues<PetAppearance>())
        {
            var pet = new PetState { Mood = 98, Loneliness = 3 }; var companion = new CompanionState { Bond = 99.8 };
            var first = Apply(session, session.CreateContact(actor, CareKind.Pet), pet, companion, expected: actor); Assert.True(first.Accepted);
            var groom = Apply(session, session.CreateContact(actor, CareKind.Groom), first.Pet, first.Companion, expected: actor, kind: CareKind.Groom);
            Assert.True(groom.Accepted); Assert.Equal(100, groom.Pet.Mood); Assert.Equal(0, groom.Pet.Loneliness); Assert.Equal(100, groom.Companion.Bond);
            Assert.Equal(2, groom.Companion.CareCount); groom.Pet.Validate(); groom.Companion.Validate();
        }
    }

    [Fact] public void HoverPassOrStationaryJitterNeverBecomesAStroke()
    {
        var gate = new HoverStrokeGate(); Assert.False(gate.Update(PetAppearance.Cat, 0, 0, .016, true));
        Assert.False(gate.Update(PetAppearance.Cat, 10, 0, .05, true)); Assert.False(gate.Update(PetAppearance.Cat, 20, 0, .05, false));
        for (var i = 0; i < 100; i++) Assert.False(gate.Update(PetAppearance.Cat, i % 2 == 0 ? -.5 : .5, 0, .05, true));
    }

    [Fact] public void IntentionalHoverStrokeFiresOnceUntilLeavingOrResetting()
    {
        var gate = new HoverStrokeGate(); Assert.False(gate.Update(PetAppearance.Cat, 0, 0, .05, true));
        Assert.False(gate.Update(PetAppearance.Cat, 5, 0, .1, true)); Assert.False(gate.Update(PetAppearance.Cat, 5, 0, .1, true));
        Assert.True(gate.Update(PetAppearance.Cat, 5, 0, .05, true));
        for (var i = 0; i < 100; i++) Assert.False(gate.Update(PetAppearance.Cat, i % 2 * 5, 0, .1, true));
        Assert.False(gate.Update(PetAppearance.Cat, 0, 0, .05, false)); Assert.False(gate.Update(PetAppearance.Cat, 0, 0, .1, true));
        Assert.False(gate.Update(PetAppearance.Cat, 5, 0, .15, true)); Assert.True(gate.Update(PetAppearance.Cat, 5, 0, .1, true));
        gate.Reset(); Assert.False(gate.Update(PetAppearance.Cat, 5, 0, .1, true));
    }

    [Theory]
    [InlineData(DirectCareBlock.Dragging)] [InlineData(DirectCareBlock.Sleeping)] [InlineData(DirectCareBlock.Stairs)]
    [InlineData(DirectCareBlock.Occluded)] [InlineData(DirectCareBlock.Paused)] [InlineData(DirectCareBlock.Hidden)] [InlineData(DirectCareBlock.Editing)]
    public void EveryBlockCancelsAccumulatedContactInsteadOfResumingIt(DirectCareBlock blocked)
    {
        var gate = new HoverStrokeGate(); gate.Update(PetAppearance.Cat, 0, 0, .05, true); gate.Update(PetAppearance.Cat, 5, 0, .2, true);
        Assert.False(gate.Update(PetAppearance.Cat, 5, 0, .05, true, blocked)); Assert.False(gate.Update(PetAppearance.Cat, 5, 0, .05, true));
        for (var i = 0; i < 10; i++) Assert.False(gate.Update(PetAppearance.Cat, 5, 0, .05, true));
        Assert.True(gate.Update(PetAppearance.Cat, 10, 0, .05, true));
    }

    [Fact] public void TargetSwitchPointerWarpAndMissingSamplesCannotBorrowOldDwell()
    {
        var gate = new HoverStrokeGate(); gate.Update(PetAppearance.Cat, 0, 0, .1, true); gate.Update(PetAppearance.Cat, 5, 0, .2, true);
        Assert.False(gate.Update(PetAppearance.Girl, 5, 0, .05, true)); Assert.False(gate.Update(PetAppearance.Girl, 10, 0, .2, true));
        Assert.False(gate.Update(PetAppearance.Girl, 100, 0, .05, true)); Assert.False(gate.Update(PetAppearance.Girl, 105, 0, .2, true));
        Assert.False(gate.Update(PetAppearance.Girl, 105, 0, 1, true)); Assert.False(gate.Update(PetAppearance.Girl, 105, 0, .1, true));
        Assert.False(gate.Update(PetAppearance.Girl, double.NaN, 0, .1, true)); Assert.False(gate.Update(PetAppearance.Girl, 110, 0, .1, true));
        Assert.False(gate.Update(PetAppearance.Girl, 115, 0, .2, true)); Assert.True(gate.Update(PetAppearance.Girl, 115, 0, .05, true));
    }

    [Fact] public void CombNeedsContinuousMotionAndCannotRewardAParkedOrDetachedTool()
    {
        var gate = new CombStrokeGate();
        for (var i = 0; i < 50; i++) Assert.False(gate.Update(PetAppearance.BorderCollie, 0, 0, .05, true));
        Assert.True(gate.Update(PetAppearance.BorderCollie, 9, 0, .05, true));
        for (var i = 0; i < 50; i++) Assert.False(gate.Update(PetAppearance.BorderCollie, i % 2 * 9, 0, .05, true));
        Assert.False(gate.Update(PetAppearance.BorderCollie, 0, 0, .05, false)); Assert.False(gate.Update(PetAppearance.BorderCollie, 0, 0, .05, true));
        Assert.False(gate.Update(PetAppearance.BorderCollie, 9, 0, .1, true)); Assert.False(gate.Update(PetAppearance.BorderCollie, 9, 0, .05, false));
        Assert.False(gate.Update(PetAppearance.BorderCollie, 9, 0, .05, true)); Assert.False(gate.Update(PetAppearance.BorderCollie, 18, 0, .1, true));
        Assert.True(gate.Update(PetAppearance.BorderCollie, 18, 0, .05, true));
    }
}
