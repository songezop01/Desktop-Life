namespace DesktopLife.Core;

public readonly record struct PlayExposure(double Seconds, long Contacts);

/// <summary>Runtime-only evidence from successful impulses, never inferred from an intent or a pose.</summary>
public sealed class ObservedPlayExposure
{
    private sealed class Pending
    {
        internal long LastSerial, Contacts;
        internal double Seconds;
    }
    private readonly Dictionary<PetAppearance, Pending> residents = [];

    public bool Record(PetAppearance resident, long contactSerial, double actualSeconds)
    {
        if (!HangingToyInteraction.Allowed(resident) || contactSerial < 1 || !double.IsFinite(actualSeconds) || actualSeconds < 0 || actualSeconds > .1)
            throw new ArgumentOutOfRangeException(nameof(actualSeconds));
        if (!residents.TryGetValue(resident, out var pending)) residents[resident] = pending = new();
        if (contactSerial <= pending.LastSerial) return false;
        pending.LastSerial = contactSerial; pending.Contacts = checked(pending.Contacts + 1); pending.Seconds += actualSeconds;
        return true;
    }
    public PlayExposure Peek(PetAppearance resident) => residents.TryGetValue(resident, out var pending) ? new(pending.Seconds, pending.Contacts) : default;

    public PlayExposure Take(PetAppearance resident, double secondsBudget)
    {
        if (!Enum.IsDefined(resident) || double.IsNaN(secondsBudget) || secondsBudget < 0)
            throw new ArgumentOutOfRangeException(nameof(secondsBudget));
        if (!residents.TryGetValue(resident, out var pending)) return default;
        var seconds = Math.Min(pending.Seconds, secondsBudget);
        pending.Seconds -= seconds;
        // A very short consumer interval can settle part of a measured contact.
        // Its contact receipt is issued once the remaining seconds are settled.
        var contacts = pending.Seconds <= .000000000001 ? pending.Contacts : 0;
        if (contacts != 0) { pending.Seconds = 0; pending.Contacts = 0; }
        return new(seconds, contacts);
    }
}

public static class ObservedPlaySettlement
{
    public static PlayExposure Advance(HomeostasisSession life, ObservedPlayExposure evidence, PetAppearance resident,
        TimeSpan elapsed, BodyAction sampledAction, bool present, bool sampledTeaserContact)
    {
        ArgumentNullException.ThrowIfNull(life); ArgumentNullException.ThrowIfNull(evidence);
        if (elapsed < TimeSpan.Zero || !Enum.IsDefined(resident) || !Enum.IsDefined(sampledAction))
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        var ordinary = sampledTeaserContact && sampledAction == BodyAction.PlayToy ? BodyAction.Sit : sampledAction;
        if (elapsed <= TimeSpan.FromSeconds(10))
        {
            var observed = evidence.Take(resident, elapsed.TotalSeconds);
            var active = TimeSpan.FromSeconds(observed.Seconds);
            life.AdvanceCompanion(elapsed - active, ordinary, present);
            if (active > TimeSpan.Zero) life.AdvanceCompanion(active, BodyAction.PlayToy, present);
            return observed;
        }
        // Keep the existing offline update and runtime counter contract. Only the
        // difference earned by contacts observed before the gap is added afterward;
        // hunger, presence, fatigue and offline time are not counted a second time.
        life.AdvanceCompanion(elapsed, ordinary, present);
        var pending = evidence.Take(resident, double.PositiveInfinity);
        var remaining = pending.Seconds;
        while (remaining > 0)
        {
            var seconds = Math.Min(remaining, 10); remaining -= seconds;
            var before = life.State;
            var play = CompanionCare.Step(before, TimeSpan.FromSeconds(seconds), BodyAction.PlayToy, present);
            var idle = CompanionCare.Step(before, TimeSpan.FromSeconds(seconds), BodyAction.Sit, present);
            static double Delta(double current, double played, double resting) => Math.Clamp(current + played - resting, 0, 100);
            life.ApplyCare(before with
            {
                Energy = Delta(before.Energy, play.Energy, idle.Energy),
                Boredom = Delta(before.Boredom, play.Boredom, idle.Boredom),
                Mood = Delta(before.Mood, play.Mood, idle.Mood),
                Excitement = Delta(before.Excitement, play.Excitement, idle.Excitement)
            });
        }
        return pending;
    }
}
