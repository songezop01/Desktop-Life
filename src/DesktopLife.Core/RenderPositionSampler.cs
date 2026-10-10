namespace DesktopLife.Core;

/// <summary>Two simulation samples for presentation only. Sampling never advances the model.</summary>
public sealed class RenderPositionSampler
{
    // One ordinary simulation tick of latency avoids extrapolating through walls.
    // Cap that latency at 50 ms; a gap over 200 ms retires the old trajectory.
    public const double MaximumDelaySeconds = .05;
    public const double MaximumSampleGapSeconds = .2;
    public const double MaximumCanonicalDistance = 80;
    private RoomPoint previous = new(0, 0), latest = new(0, 0);
    private double previousTime, latestTime, delay;
    private long continuityToken;
    private double? latestCanonicalScale;
    private bool initialized;
    public bool Interpolating { get; private set; }
    public RoomPoint Latest => latest;

    public void Snap(RoomPoint position, double timeSeconds, long geometryToken = 0)
    {
        Validate(position, timeSeconds);
        previous = latest = position; previousTime = latestTime = timeSeconds;
        continuityToken = geometryToken; delay = 0; initialized = true; Interpolating = false; latestCanonicalScale = null;
    }

    public void Record(RoomPoint position, double timeSeconds, bool continuousMotion,
        double canonicalScale = 1, long geometryToken = 0, bool allowVerticalMotion = false)
    {
        Validate(position, timeSeconds);
        if (!double.IsFinite(canonicalScale) || canonicalScale <= 0) throw new ArgumentOutOfRangeException(nameof(canonicalScale));
        var elapsed = timeSeconds - latestTime;
        var dx = (position.X - latest.X) / canonicalScale;
        var dy = (position.Y - latest.Y) / canonicalScale;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var scaleChanged = latestCanonicalScale is {} priorScale && priorScale != canonicalScale;
        if (!initialized || !continuousMotion || geometryToken != continuityToken || elapsed <= 0 || elapsed > MaximumSampleGapSeconds
            || scaleChanged || !double.IsFinite(distance) || distance >= MaximumCanonicalDistance || distance <= .000001
            || (!allowVerticalMotion && Math.Abs(dy) > .5))
        { Snap(position, timeSeconds, geometryToken); latestCanonicalScale = canonicalScale; return; }
        previous = latest; previousTime = latestTime; latest = position; latestTime = timeSeconds;
        delay = Math.Min(elapsed, MaximumDelaySeconds); Interpolating = true; latestCanonicalScale = canonicalScale;
    }

    public RoomPoint Sample(double timeSeconds)
    {
        if (!double.IsFinite(timeSeconds)) throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        if (!Interpolating) return latest;
        var amount = Math.Clamp((timeSeconds - delay - previousTime) / (latestTime - previousTime), 0, 1);
        return new(previous.X + (latest.X - previous.X) * amount, previous.Y + (latest.Y - previous.Y) * amount);
    }

    public bool Pending(double timeSeconds) => Interpolating && double.IsFinite(timeSeconds) && timeSeconds < latestTime + delay;

    private static void Validate(RoomPoint position, double timeSeconds)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(timeSeconds) || timeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(position));
    }
}
