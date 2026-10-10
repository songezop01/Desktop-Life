namespace DesktopLife.Core;

public enum StairFootSide { Left, Right }

/// <summary>Presentation anchors, not collision contacts or successful navigation evidence.</summary>
public sealed record StairStepPose(RoomPoint RootFeet, RoomPoint LeftFoot, RoomPoint RightFoot,
    StairFootSide PlantSide, RoomPoint PlantPoint, double PlantWeight, double WalkCycle,
    int StepIndex, double StepProgress, bool Complete, bool LandingSettle);

/// <summary>Samples the actual tread geometry without changing continuous stair physics.</summary>
public sealed class StairStepMotion
{
    private readonly RoomPoint[] anchors;
    private readonly RoomPoint from, to;
    private readonly double lift;
    public int StepCount => anchors.Length - 1;
    public string ConnectorId { get; }
    public bool Descending { get; }
    public IReadOnlyList<RoomPoint> Anchors { get; }

    public StairStepMotion(HouseStair stair, bool descending)
    {
        ArgumentNullException.ThrowIfNull(stair);
        if (string.IsNullOrWhiteSpace(stair.Id) || !Finite(stair.LowerLanding) || !Finite(stair.UpperLanding)
            || stair.Steps is null || stair.Steps.Count is < 1 or > 64 || stair.LowerLanding.Y <= stair.UpperLanding.Y
            || Math.Abs(stair.LowerLanding.X - stair.UpperLanding.X) < .000001)
            throw new ArgumentException("Invalid stair presentation geometry.", nameof(stair));
        var upward = new RoomPoint[stair.Steps.Count + 1]; upward[0] = stair.LowerLanding;
        var direction = Math.Sign(stair.UpperLanding.X - stair.LowerLanding.X);
        var previousY = stair.LowerLanding.Y; var previousEdge = stair.LowerLanding.X;
        for (var i = 0; i < stair.Steps.Count; i++)
        {
            var step = stair.Steps[i];
            var entrance = direction > 0 ? step.X : step.X + step.Width;
            var exit = direction > 0 ? step.X + step.Width : step.X;
            if (!double.IsFinite(step.X) || !double.IsFinite(step.Width) || step.Width <= 0 || !double.IsFinite(step.Y)
                || !double.IsFinite(step.Rise) || step.Rise <= 0 || step.Y >= previousY
                || Math.Abs(previousY - step.Y - step.Rise) > .000001 || Math.Abs(entrance - previousEdge) > .000001)
                throw new ArgumentException("Stair treads must be continuous and ordered from bottom to top.", nameof(stair));
            upward[i + 1] = new(step.X + step.Width / 2, step.Y);
            previousY = step.Y; previousEdge = exit;
        }
        if (Math.Abs(previousY - stair.UpperLanding.Y) > .000001 || Math.Abs(previousEdge - stair.UpperLanding.X) > .000001)
            throw new ArgumentException("The last tread must meet its upper landing.", nameof(stair));
        // Replace the final center with the landing; never append a seventeenth half-step.
        upward[^1] = stair.UpperLanding;
        anchors = descending ? upward.Reverse().ToArray() : upward;
        Anchors = Array.AsReadOnly(anchors); from = anchors[0]; to = anchors[^1];
        ConnectorId = stair.ReservationKey; Descending = descending;
        lift = stair.Steps.Max(step => Math.Max(step.Rise, step.Width)) * .3;
    }

    public bool TrySample(RoomPoint continuousFeet, out StairStepPose? pose, double supportTolerance = .75)
    {
        pose = null;
        if (!Finite(continuousFeet) || !double.IsFinite(supportTolerance) || supportTolerance < 0) return false;
        var dx = to.X - from.X; var dy = to.Y - from.Y; var lengthSquared = dx * dx + dy * dy;
        if (!double.IsFinite(lengthSquared) || lengthSquared <= .000000000001) return false;
        var fraction = ((continuousFeet.X - from.X) * dx + (continuousFeet.Y - from.Y) * dy) / lengthSquared;
        var projected = new RoomPoint(from.X + fraction * dx, from.Y + fraction * dy);
        var length = Math.Sqrt(lengthSquared);
        if (fraction < -supportTolerance / length || fraction > 1 + supportTolerance / length
            || Distance(projected, continuousFeet) > supportTolerance) return false;
        pose = SampleProgress(Math.Clamp(fraction, 0, 1)); return true;
    }

    public StairStepPose SampleProgress(double progress)
    {
        if (!double.IsFinite(progress) || progress < 0 || progress > 1) throw new ArgumentOutOfRangeException(nameof(progress));
        var stepPosition = progress * StepCount;
        var index = Math.Min(StepCount - 1, (int)Math.Floor(stepPosition));
        var phase = progress == 1 ? 1 : stepPosition - index;
        var destination = index + 1;
        var moving = destination % 2 == 1 ? StairFootSide.Left : StairFootSide.Right;
        var standing = moving == StairFootSide.Left ? StairFootSide.Right : StairFootSide.Left;
        var oldPlant = anchors[index]; var target = anchors[destination];
        var swingFrom = anchors[Math.Max(0, destination - 2)];
        var swingPhase = Math.Clamp((phase - .1) / .8, 0, 1);
        var swing = Swing(swingFrom, target, swingPhase);
        var left = moving == StairFootSide.Left ? swing : oldPlant;
        var right = moving == StairFootSide.Right ? swing : oldPlant;
        var root = Lerp(oldPlant, target, TransferFraction(phase));
        // Whole-raster poses cannot keep one foot planted throughout a full
        // riser transfer. Calibrate the real raster foot during the contact
        // windows; the transfer uses the continuous root curve, not fake feet.
        var newPlant = phase >= .5;
        var weight = phase <= .12 ? 1 : phase < .32 ? 1 - Smooth((phase - .12) / .2)
            : phase <= .75 ? 0 : phase < .9 ? Smooth((phase - .75) / .15) : 1;
        var landingSettle = destination == StepCount && phase >= .9;
        if (landingSettle)
        {
            // Collect the trailing foot at the landing before the idle pose.
            var trailing = Swing(oldPlant, target, (phase - .9) / .1);
            if (moving == StairFootSide.Left) right = trailing; else left = trailing;
        }
        if (progress == 1) left = right = to;
        // Keep the same painted stance frame across a tread boundary. Advancing
        // 3 -> 4 (or 7 -> 0) while its foot is planted changes the calibrated
        // local foot point discontinuously and teleports the whole window.
        // All four frame changes happen during the unplanted transfer instead.
        var frameTransfer = Smooth(Math.Clamp((phase - .32) / .43, 0, 1));
        var walkCycle = (index * 4 - 1 + frameTransfer * 4) / 8;
        return new(root, left, right, newPlant ? moving : standing, newPlant ? target : oldPlant, weight,
            walkCycle, index, phase, progress == 1, landingSettle);
    }

    /// <summary>A bounded transfer between two real raster stance origins; contact windows stay stationary.</summary>
    public static double TransferFraction(double phase)
    {
        if (!double.IsFinite(phase) || phase < 0 || phase > 1) throw new ArgumentOutOfRangeException(nameof(phase));
        var t = Math.Clamp((phase - .12) / .78, 0, 1);
        // Short acceleration/deceleration shoulders preserve smooth velocity
        // without the 1.5x acceleration peak of a full smoothstep. The maximum
        // slope is 1 / (.78 * .92), below 1.4 per physical step phase.
        const double shoulder = .08;
        if (t < shoulder) return t * t / (2 * shoulder * (1 - shoulder));
        if (t <= 1 - shoulder) return (t - shoulder / 2) / (1 - shoulder);
        return 1 - (1 - t) * (1 - t) / (2 * shoulder * (1 - shoulder));
    }

    private RoomPoint Swing(RoomPoint start, RoomPoint end, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        // Lift before translating across the riser, then lower onto the tread.
        var raisedY = Math.Min(start.Y, end.Y) - lift;
        if (progress <= .3) return new(start.X, start.Y + (raisedY - start.Y) * Smooth(progress / .3));
        if (progress <= .75) return new(start.X + (end.X - start.X) * Smooth((progress - .3) / .45), raisedY);
        return new(end.X, raisedY + (end.Y - raisedY) * Smooth((progress - .75) / .25));
    }
    private static RoomPoint Lerp(RoomPoint a, RoomPoint b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    private static double Smooth(double t) => t * t * (3 - 2 * t);
    private static double Distance(RoomPoint a, RoomPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static bool Finite(RoomPoint? point) => point is not null && double.IsFinite(point.X) && double.IsFinite(point.Y);
}
