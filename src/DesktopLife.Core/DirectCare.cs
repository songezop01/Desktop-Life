namespace DesktopLife.Core;

public sealed record DirectCareContact(Guid SessionId, Guid ContactId, PetAppearance Resident, CareKind Kind);
public enum DirectCareFailure { None, StaleSession, Duplicate, SessionFull, InvalidContact, UnknownResident, WrongResident, WrongKind, NoContact, Cooldown, InvalidState, Blocked }
public sealed record DirectCareResult(PetState Pet, CompanionState Companion, bool Accepted,
    DirectCareFailure Failure, double CooldownRemainingSeconds = 0);

// Owned by the scene's single thread. Requests and replay guards are transient;
// only the returned PetState + CompanionState belong in the organism save.
public sealed class DirectCareSession
{
    // A few KiB of replay IDs bounds a long desktop session. Rotation retires the
    // entire session; no individual ID is evicted and later accepted again.
    public const int MaximumContacts = 256;
    public const double PetCooldownSeconds = 8;
    public const double GroomCooldownSeconds = 20;
    private readonly HashSet<Guid> attempted = [];
    public Guid SessionId { get; private set; } = Guid.NewGuid();
    public int AttemptedContacts => attempted.Count;

    public DirectCareContact CreateContact(PetAppearance resident, CareKind kind)
    {
        if (attempted.Count >= MaximumContacts) Reset();
        return new(SessionId, Guid.NewGuid(), resident, kind);
    }

    // Call on hide, pause, editing, cancellation or a replaced scene. Pending
    // callbacks from the old session cannot turn a cancelled stroke into care.
    public void Reset() { SessionId = Guid.NewGuid(); attempted.Clear(); }

    public DirectCareResult Apply(DirectCareContact contact, PetAppearance expectedResident, CareKind expectedKind,
        PetState pet, CompanionState companion, DateTimeOffset now, bool physicalContact, DirectCareBlock blocked = DirectCareBlock.None)
    {
        DirectCareResult Reject(DirectCareFailure failure, double remaining = 0) => new(pet, companion, false, failure, remaining);
        if (contact.SessionId != SessionId) return Reject(DirectCareFailure.StaleSession);
        if (contact.ContactId == Guid.Empty || now == default) return Reject(DirectCareFailure.InvalidContact);
        if (attempted.Contains(contact.ContactId)) return Reject(DirectCareFailure.Duplicate);
        if (attempted.Count >= MaximumContacts) return Reject(DirectCareFailure.SessionFull);
        // A finalized event is terminal even when rejected: waiting out a cooldown
        // must not make the same physical stroke eligible for a later reward.
        attempted.Add(contact.ContactId);
        if (!Enum.IsDefined(contact.Resident) || !Enum.IsDefined(expectedResident)) return Reject(DirectCareFailure.UnknownResident);
        if (contact.Resident != expectedResident) return Reject(DirectCareFailure.WrongResident);
        if (contact.Kind is not (CareKind.Pet or CareKind.Groom) || expectedKind is not (CareKind.Pet or CareKind.Groom)
            || contact.Kind != expectedKind) return Reject(DirectCareFailure.WrongKind);
        try { pet.Validate(); companion.Validate(); }
        catch (InvalidDataException) { return Reject(DirectCareFailure.InvalidState); }
        if (companion.CareCount == int.MaxValue) return Reject(DirectCareFailure.InvalidState);
        // Contact evidence alone cannot authorize a stroke while the scene says
        // the resident is sleeping, being held, hidden or otherwise unavailable.
        if (blocked != DirectCareBlock.None) return Reject(DirectCareFailure.Blocked);
        if (!physicalContact) return Reject(DirectCareFailure.NoContact);
        var cooldown = contact.Kind == CareKind.Pet ? PetCooldownSeconds : GroomCooldownSeconds;
        if (companion.LastCare.TryGetValue(contact.Kind, out var last))
        {
            var remaining = cooldown - (now - last).TotalSeconds;
            if (remaining > 0) return Reject(DirectCareFailure.Cooldown, remaining);
        }
        static double C(double value) => Math.Clamp(value, 0, 100);
        var nextPet = contact.Kind == CareKind.Pet
            ? pet with { Loneliness = C(pet.Loneliness - 12), Mood = C(pet.Mood + 5) }
            : pet with { Loneliness = C(pet.Loneliness - 8), Mood = C(pet.Mood + 6) };
        var nextCompanion = new CompanionState { Name = companion.Name, Bond = C(companion.Bond + (contact.Kind == CareKind.Pet ? .25 : .6)),
            CareCount = companion.CareCount + 1, Memories = companion.Memories.ToList(), LastCare = new(companion.LastCare) };
        nextCompanion.LastCare[contact.Kind] = now;
        // Retain the existing quiet-memory cadence; repeated real strokes update
        // care evidence without crowding every ordinary memory out of the save.
        if (!nextCompanion.Memories.Any(memory => memory.Kind?.StartsWith("milestone:", StringComparison.Ordinal) != true
                && now - memory.At < TimeSpan.FromMinutes(30)))
        {
            var text = (contact.Resident == PetAppearance.Girl, contact.Kind) switch
            {
                (true, CareKind.Pet) => "你溫柔地摸了摸她的頭。",
                (true, _) => "你幫她整理了頭髮。",
                (false, CareKind.Pet) => "你溫柔地摸了摸牠。",
                _ => "你細心幫牠梳理了毛毛。"
            };
            nextCompanion.Remember(now, text, contact.Kind == CareKind.Pet ? "direct-care:pet" : "direct-care:groom");
        }
        nextPet.Validate(); nextCompanion.Validate();
        return new(nextPet, nextCompanion, true, DirectCareFailure.None);
    }
}

[Flags]
public enum DirectCareBlock { None = 0, Dragging = 1, Sleeping = 2, Stairs = 4, Occluded = 8, Paused = 16, Hidden = 32, Editing = 64 }

public sealed class HoverStrokeGate
{
    // 250 ms avoids granting care during an ordinary cursor pass. A 4 DIP
    // excursion is deliberate small movement, rather than accumulated jitter.
    public const double DefaultDwellSeconds = .25;
    public const double DefaultMotionDip = 4;
    private readonly ContactMotionGate motion;
    public HoverStrokeGate(double dwellSeconds = DefaultDwellSeconds, double motionDip = DefaultMotionDip)
        => motion = new(dwellSeconds, motionDip);
    public bool Update(PetAppearance resident, double x, double y, double elapsedSeconds, bool near, DirectCareBlock blocked = DirectCareBlock.None)
        => motion.Update(resident, x, y, elapsedSeconds, near, blocked);
    public void Reset() => motion.Reset();
}

public sealed class CombStrokeGate
{
    // A brush needs a larger 8 DIP excursion and 150 ms of continuous contact:
    // touching once or parking the tool on fur is not a brushing stroke.
    public const double DefaultContactSeconds = .15;
    public const double DefaultMotionDip = 8;
    private readonly ContactMotionGate motion;
    public CombStrokeGate(double contactSeconds = DefaultContactSeconds, double motionDip = DefaultMotionDip)
        => motion = new(contactSeconds, motionDip);
    public bool Update(PetAppearance resident, double x, double y, double elapsedSeconds, bool touching, DirectCareBlock blocked = DirectCareBlock.None)
        => motion.Update(resident, x, y, elapsedSeconds, touching, blocked);
    public void Reset() => motion.Reset();
}

internal sealed class ContactMotionGate
{
    // Coordinates are scene DIP. More than 32 DIP in one sample is treated as a
    // warp; a gap over 200 ms cannot prove continuous, intentional contact.
    private const double MaximumSampleMovementDip = 32;
    private const double MaximumSampleGapSeconds = .2;
    private readonly double requiredSeconds, requiredMotion;
    private PetAppearance? target;
    private double age, originX, originY, previousX, previousY, excursion;
    private bool fired;
    internal ContactMotionGate(double requiredSeconds, double requiredMotion)
    {
        if (!double.IsFinite(requiredSeconds) || requiredSeconds <= 0 || requiredSeconds > 2
            || !double.IsFinite(requiredMotion) || requiredMotion <= 0 || requiredMotion > MaximumSampleMovementDip)
            throw new ArgumentOutOfRangeException(nameof(requiredSeconds));
        this.requiredSeconds = requiredSeconds; this.requiredMotion = requiredMotion;
    }
    internal bool Update(PetAppearance resident, double x, double y, double elapsedSeconds, bool touching, DirectCareBlock blocked)
    {
        if (!touching || blocked != DirectCareBlock.None || !Enum.IsDefined(resident) || !double.IsFinite(x) || !double.IsFinite(y)
            || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 || elapsedSeconds > MaximumSampleGapSeconds)
        { Reset(); return false; }
        if (target != resident)
        {
            Reset(); target = resident; originX = previousX = x; originY = previousY = y; return false;
        }
        var dx = x - previousX; var dy = y - previousY;
        if (dx * dx + dy * dy > MaximumSampleMovementDip * MaximumSampleMovementDip)
        { Reset(); target = resident; originX = previousX = x; originY = previousY = y; return false; }
        previousX = x; previousY = y;
        age += elapsedSeconds;
        dx = x - originX; dy = y - originY;
        excursion = Math.Max(excursion, Math.Sqrt(dx * dx + dy * dy));
        if (fired || age + 1e-9 < requiredSeconds || excursion + 1e-9 < requiredMotion) return false;
        fired = true; return true;
    }
    internal void Reset() { target = null; age = excursion = 0; fired = false; originX = originY = previousX = previousY = 0; }
}
